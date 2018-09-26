using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Threading;
using DURF.Interfaces;

namespace DURF.Collections
{
    /// <summary>
    /// An ordered collection that ensures it is always on the correct thread for collectionchanged and propertychanged events.
    /// 
    /// This collection is also thread-safe (concurrency is ok). Merely supply the IDispatcher to utilize this class w/ UI elements.
    /// 
    /// All methods are synchronous in nature because most UI controls expect the bound collection to remain unchanged throughout
    /// the CollectionChanged event cycle.
    ///
    /// This collection can act like a List<typeparam name="TType"></typeparam>, a Queue<typeparam name="TType"></typeparam>, or a Stack<typeparam name="TType"></typeparam>
    /// depending on your desired behavior.
    /// </summary>
    /// <typeparam name="TType"></typeparam>
    [Serializable]
    [DebuggerDisplay(@"Count = {Count}")]
    public class TrackableCollection<TType> : IList<TType>, IList, IReadOnlyList<TType>, IConvertible, ISerializable, INotifyCollectionChanged, INotifyPropertyChanged, IQueue<TType>, IStack<TType>
    {
        private readonly List<TType> _collection = new List<TType>();

        public TrackableCollection(IEnumerable<TType> collection) : this()
        {

            _collection = collection == null ? new List<TType>() : new List<TType>(collection.ToList()); // added ToList()  - Joe (08 Apr 2016)
        }

        public TrackableCollection()
        {
            UseDispatcherForCollectionChanged = true;
        }

        public bool TrackChanges { get; set; } = true;

        [Serializable]
        private class SimpleMonitor : IDisposable
        {
            private int _busyCount;

            public bool Busy => this._busyCount > 0;

            public void Enter()
            {
                this._busyCount += 1;
            }

            public void Dispose()
            {
                this._busyCount -= 1;
            }
        }

        private SimpleMonitor _monitor = new SimpleMonitor();

        protected void CheckReentrancy()
        {
            if (this._monitor.Busy && this.CollectionChanged != null && this.CollectionChanged.GetInvocationList().Length > 1)
            {
                // this could be bad... Brent (27 Jan 2016)
                //throw new InvalidOperationException(@"Reentrancy not allowed.");
            }
        }

        protected IDisposable BlockReentrancy()
        {
            this._monitor.Enter();
            return this._monitor;
        }

        #region Thread-safe methods

        /// <summary>
        /// Moves an item from oldIndex to newIndex. 
        /// </summary>
        /// <param name="oldIndex"></param>
        /// <param name="newIndex"></param>
        public void Move(int oldIndex, int newIndex)
        {
            if (oldIndex == newIndex)
                return;

            var oldItem = this[oldIndex];
            PushToUiThreadSync(() =>
            {
                lock (SyncRoot)
                {
                    CheckReentrancy();
                    _collection.RemoveAt(oldIndex);

                    /*
                     * Handles this scenario:
                     * 
                     * var a = new { "1","2","3","4","5"}
                     * a.Move(a.IndexOf("2"), a.IndexOf("4"))
                     * 
                     * a.IndexOf("4") before the remove is 3.  After the "2" is removed, the new index should be 2
                     */
                    if (newIndex > oldIndex)
                        newIndex--;

                    if (newIndex > (_collection.Count - 1) || newIndex < 0)
                        _collection.Add(oldItem);
                    else
                        _collection.Insert(newIndex, oldItem);

                    if (TrackChanges)
                        TrackableScope.Current?.TrackChange(() => { this.Move(newIndex, oldIndex); });

                    this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, (object)oldItem, newIndex, oldIndex));
                }
            });
        }

        /// <summary>
        /// Removes all items from the collection.
        /// </summary>
        protected void ClearItems()
        {
            if (!_collection.Any())
                return;

            PushToUiThreadSync(() =>
            {
                lock (SyncRoot)
                {
                    this.CheckReentrancy();

                    var chgd = Count > 0;

                    var items = _collection.ToList();
                    _collection.Clear();
                    if (chgd)
                    {
                        if (TrackChanges)
                            TrackableScope.Current?.TrackChange(() =>  this.AddRange(items));
                        this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                    }
                }
            });
        }

        /// <summary>
        /// Removes the item at the specified index of the collection.
        /// </summary>
        /// <param name="index">The zero-based index of the element to remove.</param>
        protected void RemoveItem(int index)
        {
            if (Count <= index)
                return;
            var current = this[index];

            PushToUiThreadSync(() =>
            {
                lock (SyncRoot)
                {
                    // during the context switch a couple things could have happened: 
                    // 1. The collection could have been modified (waiting on lock(this) b/c a different Remove/Add call was happening)
                    // 2. The item at the desired index could have changed (say Move() was happening during our wait on the lock)
                    if (Count <= index)
                        return;

                    // ensure the index we publish is correct now that we've context switched
                    index = _collection.IndexOf(current);
                    if (_collection.Remove(current))
                    {
                        this.CheckReentrancy();

                        if (TrackChanges)
                            TrackableScope.Current?.TrackChange(() => this.InsertItem(index, current));
                        this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, current, index));
                    }
                }
            });
        }

        /// <summary>
        /// Inserts an item into the collection at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param><param name="item">The object to insert.</param>
        protected void InsertItem(int index, TType item)
        {
            PushToUiThreadSync(() =>
            {
                lock (SyncRoot)
                {
                    this.CheckReentrancy();

                    var indexAdded = 0;
                    if (index > (_collection.Count - 1))
                    {
                        _collection.Add(item);
                        indexAdded = Count; 
                    }
                    else
                    {
                        _collection.Insert(index, item);
                        indexAdded = index; 
                    }
                    if (TrackChanges)
                        TrackableScope.Current?.TrackChange(() => this.Remove(item));

                    this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, indexAdded));
                }
            });
        }

        /// <summary>
        /// Goal here is to avoid thread issue, index could change before inserting which would throw index out of range exception so lock here..
        /// </summary>
        /// <param name="existingItem"></param>
        /// <param name="itemInsertedBeforeExisting"></param>
        public void InsertItemBefore(TType existingItem, TType itemInsertedBeforeExisting)
        {
            PushToUiThreadSync(() =>
            {
                lock (SyncRoot)
                {
                    this.CheckReentrancy();
                    var index = _collection.IndexOf(existingItem);

                    var indexAdded = 0;
                    if (index > (_collection.Count - 1))
                    {
                        _collection.Add(itemInsertedBeforeExisting);
                        indexAdded = Count;
                    }
                    else
                    {
                        _collection.Insert(index, itemInsertedBeforeExisting);
                        indexAdded = index; 
                    }

                    if (TrackChanges)
                        TrackableScope.Current?.TrackChange(() => this.Remove(existingItem));

                    this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, itemInsertedBeforeExisting, indexAdded));
                }
            });
        }

        /// <summary>
        /// Removes the first occurrence of a specific object from the <see cref="T:System.Collections.IList"/>.
        /// </summary>
        /// <param name="value">The object to remove from the <see cref="T:System.Collections.IList"/>. </param><exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.IList"/> is read-only.-or- The <see cref="T:System.Collections.IList"/> has a fixed size. </exception>
        public void Remove(object value)
        {
            PushToUiThreadSync(() =>
            {
                lock (SyncRoot)
                {
                    this.CheckReentrancy();

                    var item = (TType)value;
                    var idx = _collection.IndexOf(item);
                    if (_collection.Remove(item))
                    {
                        if (TrackChanges)
                            TrackableScope.Current?.TrackChange(() => this.InsertItem(idx, item));
                        this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, value, idx));
                    }
                }
            });
        }

        /// <summary>
        /// Removes the <see cref="T:System.Collections.IList"/> item at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the item to remove. </param><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index in the <see cref="T:System.Collections.IList"/>. </exception><exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.IList"/> is read-only.-or- The <see cref="T:System.Collections.IList"/> has a fixed size. </exception>
        public void RemoveAt(int index)
        {
            RemoveItem(index);
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="T:System.Collections.IList"/> is read-only.
        /// </summary>
        /// <returns>
        /// true if the <see cref="T:System.Collections.IList"/> is read-only; otherwise, false.
        /// </returns>
        bool IList.IsReadOnly { get; } = false;

        /// <summary>
        /// Gets a value indicating whether the <see cref="T:System.Collections.IList"/> has a fixed size.
        /// </summary>
        /// <returns>
        /// true if the <see cref="T:System.Collections.IList"/> has a fixed size; otherwise, false.
        /// </returns>
        public bool IsFixedSize { get; } = false;

        /// <summary>
        /// Gets or sets the element at the specified index.
        /// </summary>
        /// <returns>
        /// The element at the specified index.
        /// </returns>
        /// <param name="index">The zero-based index of the element to get or set. </param><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index in the <see cref="T:System.Collections.IList"/>. </exception><exception cref="T:System.NotSupportedException">The property is set and the <see cref="T:System.Collections.IList"/> is read-only. </exception>
        public TType this[int index]
        {
            [TargetedPatchingOptOut(@"Performance critical to inline across NGen image boundaries")]
            get
            {
                lock (SyncRoot)
                    return _collection[index];
            }
            set => this.SetItem(index, value);
        }

        /// <summary>
        /// Gets or sets the element at the specified index.
        /// </summary>
        /// <returns>
        /// The element at the specified index.
        /// </returns>
        /// <param name="index">The zero-based index of the element to get or set. </param><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index in the <see cref="T:System.Collections.IList"/>. </exception><exception cref="T:System.NotSupportedException">The property is set and the <see cref="T:System.Collections.IList"/> is read-only. </exception>
        object IList.this[int index]
        {
            [TargetedPatchingOptOut(@"Performance critical to inline across NGen image boundaries")]
            get
            {
                lock (SyncRoot)
                    return _collection[index];
            }
            set => this.SetItem(index, (TType)value);
        }

        /// <summary>
        /// Removes the first occurrence of a specific object from the <see cref="T:System.Collections.Generic.ICollection`1"/>.
        /// </summary>
        /// <returns>
        /// true if <paramref name="item"/> was successfully removed from the <see cref="T:System.Collections.Generic.ICollection`1"/>; otherwise, false. This method also returns false if <paramref name="item"/> is not found in the original <see cref="T:System.Collections.Generic.ICollection`1"/>.
        /// </returns>
        /// <param name="item">The object to remove from the <see cref="T:System.Collections.Generic.ICollection`1"/>.</param><exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only.</exception>
        bool ICollection<TType>.Remove(TType item) => Remove(item);

        public bool Remove(TType item)
        {
            bool removed = false;
            PushToUiThreadSync(() =>
            {
                lock (SyncRoot)
                {
                    var indx = _collection.IndexOf(item);
                    removed = _collection.Remove(item);
                    if (removed)
                    {
                        if (TrackChanges)
                            TrackableScope.Current?.TrackChange(() => this.InsertItem(indx, item));
                        this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, indx));
                    }
                }
            });
            return removed;
        }

        /// <summary>
        /// Copies the elements of the <see cref="T:System.Collections.ICollection"/> to an <see cref="T:System.Array"/>, starting at a particular <see cref="T:System.Array"/> index.
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="T:System.Array"/> that is the destination of the elements copied from <see cref="T:System.Collections.ICollection"/>. The <see cref="T:System.Array"/> must have zero-based indexing. </param><param name="index">The zero-based index in <paramref name="array"/> at which copying begins. </param><exception cref="T:System.ArgumentNullException"><paramref name="array"/> is null. </exception><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is less than zero. </exception><exception cref="T:System.ArgumentException"><paramref name="array"/> is multidimensional.-or- The number of elements in the source <see cref="T:System.Collections.ICollection"/> is greater than the available space from <paramref name="index"/> to the end of the destination <paramref name="array"/>.-or-The type of the source <see cref="T:System.Collections.ICollection"/> cannot be cast automatically to the type of the destination <paramref name="array"/>.</exception>
        public void CopyTo(Array array, int index)
        {
            // Safer Copy; Cubby's GetChildren kept failing trying to copy the Toy's Array into a temp variable
            // because the origional would grow and exceed the target's size - Neil (01 Apr 2016)
            lock (SyncRoot)
            {
                if (_collection.Any())
                    _collection.CopyTo((TType[])array, index);
            }
        }

        /// <summary>
        /// Only updated within a write-lock, so we should be good to always read from it
        /// </summary>
        public int Count
        {
            get
            {
                lock (SyncRoot)
                    return _collection.Count;
            }
        } 

        /// <summary>
        /// Gets an object that can be used to synchronize access to the <see cref="T:System.Collections.ICollection"/>.
        /// </summary>
        /// <returns>
        /// An object that can be used to synchronize access to the <see cref="T:System.Collections.ICollection"/>.
        /// </returns>
        public object SyncRoot { get; } = new object();

        /// <summary>
        /// Gets a value indicating whether access to the <see cref="T:System.Collections.ICollection"/> is synchronized (thread safe).
        /// </summary>
        /// <returns>
        /// true if access to the <see cref="T:System.Collections.ICollection"/> is synchronized (thread safe); otherwise, false.
        /// </returns>
        public bool IsSynchronized { get; } = true;

        /// <summary>
        /// Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only.
        /// </summary>
        /// <returns>
        /// true if the <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only; otherwise, false.
        /// </returns>
        bool ICollection<TType>.IsReadOnly => false;

        /// <summary>
        /// Replaces the element at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the element to replace.</param><param name="item">The new value for the element at the specified index.</param>
        protected void SetItem(int index, TType item)
        {
            var current = this[index];
            PushToUiThreadSync(() =>
            {
                lock (SyncRoot)
                {
                    this.CheckReentrancy();

                    _collection[index] = item;
                    if (TrackChanges)
                        TrackableScope.Current?.TrackChange(() => this.SetItem(index, current));
                    this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, item, current, index));
                }
            });
        }

        /// <summary>
        /// Adds an item to the <see cref="T:System.Collections.Generic.ICollection`1"/>.
        /// </summary>
        /// <param name="item">The object to add to the <see cref="T:System.Collections.Generic.ICollection`1"/>.</param><exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only.</exception>
        public void Add(TType item)
        {
            InsertItem(Count, item);
        }

        void ICollection<TType>.Add(TType item)
        {
            InsertItem(Count, item);
        }

        /// <summary>
        /// Adds an item to the <see cref="T:System.Collections.IList"/>.
        /// </summary>
        /// <returns>
        /// The position into which the new element was inserted, or -1 to indicate that the item was not inserted into the collection.
        /// </returns>
        /// <param name="value">The object to add to the <see cref="T:System.Collections.IList"/>. </param><exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.IList"/> is read-only.-or- The <see cref="T:System.Collections.IList"/> has a fixed size. </exception>
        int IList.Add(object value)
        {
            var idx = Count;
            InsertItem(idx, (TType)value);
            return idx;
        }

        /// <summary>
        /// Determines whether the <see cref="T:System.Collections.IList"/> contains a specific value.
        /// </summary>
        /// <returns>
        /// true if the <see cref="T:System.Object"/> is found in the <see cref="T:System.Collections.IList"/>; otherwise, false.
        /// </returns>
        /// <param name="value">The object to locate in the <see cref="T:System.Collections.IList"/>. </param>
        bool IList.Contains(object value) => Contains((TType)value);

        /// <summary>
        /// Removes all items from the <see cref="T:System.Collections.IList"/>.
        /// </summary>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.IList"/> is read-only. </exception>
        public void Clear() => ClearItems();

        /// <summary>
        /// Determines the index of a specific item in the <see cref="T:System.Collections.IList"/>.
        /// </summary>
        /// <returns>
        /// The index of <paramref name="value"/> if found in the list; otherwise, -1.
        /// </returns>
        /// <param name="value">The object to locate in the <see cref="T:System.Collections.IList"/>. </param>
        int IList.IndexOf(object value)
        {
            // b/c Telerik sucks - Ryan (15 Aug 2016)
            if (value is TType)
                return IndexOf((TType)value);
            return -1;
        }

        /// <summary>
        /// Inserts an item to the <see cref="T:System.Collections.IList"/> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which <paramref name="value"/> should be inserted. </param><param name="value">The object to insert into the <see cref="T:System.Collections.IList"/>. </param><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index in the <see cref="T:System.Collections.IList"/>. </exception><exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.IList"/> is read-only.-or- The <see cref="T:System.Collections.IList"/> has a fixed size. </exception><exception cref="T:System.NullReferenceException"><paramref name="value"/> is null reference in the <see cref="T:System.Collections.IList"/>.</exception>
        void IList.Insert(int index, object value)
        {
            InsertItem(index, (TType)value);
        }

        /// <summary>
        /// Removes all items from the <see cref="T:System.Collections.Generic.ICollection`1"/>.
        /// </summary>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only. </exception>
        void ICollection<TType>.Clear() => ClearItems();

        public bool Contains(TType item)
        {
            bool contains = false;
            lock (SyncRoot)
                contains = _collection.Contains(item);

            return contains;
        }

        public void CopyTo(TType[] array, int index)
        {
            lock (SyncRoot)
            {
                if (_collection.Any())
                    _collection.CopyTo(array, index);
            }
        }

        public int IndexOf(TType item)
        {
            int index = 0;
            lock (SyncRoot)
                index = _collection.IndexOf(item);

            return index;
        }

        /// <summary>
        /// Inserts an item to the <see cref="T:System.Collections.Generic.IList`1"/> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param><param name="item">The object to insert into the <see cref="T:System.Collections.Generic.IList`1"/>.</param><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index in the <see cref="T:System.Collections.Generic.IList`1"/>.</exception><exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.IList`1"/> is read-only.</exception>
        public void Insert(int index, TType item) => InsertItem(index, item);

        /// <summary>
        /// Inserts values beginning at the given position.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="stuff"></param>
        public void InsertRange(int position, IEnumerable<TType> stuff)
        {
            if (stuff == null || ReferenceEquals(stuff, this))
                return;
            var toAdd = stuff as IList<TType> ?? stuff.ToList();
            PushToUiThreadSync(() =>
            {
                lock (SyncRoot)
                {
                    //var start = position;
                    //int count = 0;
                    foreach (var item in toAdd)
                    {
                        _collection.Insert(position++, item);
                        if (TrackChanges)
                            TrackableScope.Current?.TrackChange(() => this.Remove(item));
                    }

                    this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                }
            });

        }

        /// <summary>
        /// Efficient bulk-add to a collection
        /// </summary>
        /// <param name="stuff"></param>
        public void AddRange(IEnumerable<TType> stuff) => InsertRange(_collection.Count, stuff);

        public void RemoveRange(IEnumerable<TType> children)
        {
            var enumerable = children as IList<TType> ?? children.ToList();
            PushToUiThreadSync(() =>
            {
                lock (SyncRoot)
                {
                    bool anyRemoved = false;
                    this.CheckReentrancy();

                    var tuples = new List<Tuple<int, TType>>();
                    foreach (var child in enumerable)
                    {
                        var index = _collection.IndexOf(child);
                        if (index >= 0)
                        {
                            tuples.Add(new Tuple<int, TType>(index, child));
                            _collection.RemoveAt(index);
                            anyRemoved = true;
                        }
                        else
                            Debug.WriteLine($"Unable to remove item {child.ToString()} b/c it is not in the collection.");
                    }

                    if (TrackChanges && anyRemoved)
                    {
                        TrackableScope.Current?.TrackChange(() =>
                        {
                            foreach (var tuple in tuples)
                                this.InsertItem(tuple.Item1, tuple.Item2);
                        });
                    }

                    if (anyRemoved)
                        this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                }
            });
        }

        public List<TType> ToList()
        {
            lock (SyncRoot)
            {
                var result = _collection.ToList();
                return result;
            }
        }

        public void AddIfNew(TType item)
        {
            PushToUiThreadSync(() =>
            {
                lock (SyncRoot)
                {
                    this.CheckReentrancy();

                    if (_collection.Contains(item))
                        return;

                    _collection.Add(item);
                    var indexAdded = _collection.Count - 1;

                    if (TrackChanges)
                        TrackableScope.Current?.TrackChange(() => Remove(item));
                    this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, indexAdded));
                }
            });
        }

        #endregion


        [field: NonSerialized]
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        [field: NonSerialized]
        public event PropertyChangedEventHandler PropertyChanged;

        [field: NonSerialized]
        private List<NotifyCollectionChangedEventArgs> _pendingNotifications = new List<NotifyCollectionChangedEventArgs>();

        /// <summary>
        /// Raises the <see cref="E:System.Collections.ObjectModel.ObservableCollection`1.CollectionChanged"/> event with the provided arguments asynchronously.
        /// </summary>
        /// <param name="e">Arguments of the event being raised.</param>
        private void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            _changeDetectedWhileNotificationIsShutOff = ShutOffCollectionChangedEventsOnUiThread;

            if (UseDispatcherForCollectionChanged && ShutOffCollectionChangedEventsOnUiThread)
            {
                _pendingNotifications.Add(e);
                return;
            }

            _changeDetectedWhileNotificationIsShutOff = false;

            using (BlockReentrancy())
            {
                if (CollectionChanged != null)
                {
                    try
                    {
                        if (_pendingNotifications.Any())
                        {
                            e = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
                            _pendingNotifications.Clear();
                        }

                        if (e.Action != NotifyCollectionChangedAction.Move && e.Action != NotifyCollectionChangedAction.Replace)
                            this.OnPropertyChanged(nameof(Count));
                        this.OnPropertyChanged(@"Item[]");
                        CollectionChanged?.Invoke(this, e);
                    }
                    catch (Exception ex)
                    {
                        Log(@"Unable to fire collection changed event cleanly.", ex);
                    }
                }
            }
        }

        private void Log(string message, Exception ex = null)
        {
            if(ex == null)
                Debug.WriteLine(message);
            else
                Trace.TraceError(message, ex);
        }

        private void PushToUiThreadSync(Action action, bool force = false)
        {
            if (action == null)
                return;

            if ((!force && ShutOffCollectionChangedEventsOnUiThread) || !UseDispatcherForCollectionChanged || (PlatformImplementation.Dispatcher?.CheckAccess() ?? true))
            {
                action();
            }
            else
            {
                var allDone = new ManualResetEvent(false);
                PlatformImplementation.Dispatcher.BeginInvoke(() =>
                {
                    try { action(); }
                    catch (Exception ex) { Log(@"Unable to finish changing collection", ex); }
                    finally { allDone.Set(); }
                });
                allDone.WaitOne();
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            this.OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Raises the <see cref="E:System.Collections.ObjectModel.ObservableCollection`1.PropertyChanged"/> event with the provided arguments on the current thread
        /// 
        /// </summary>
        /// <param name="e">Arguments of the event being raised.</param>
        protected void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            var eventHandler = PropertyChanged;
            eventHandler?.Invoke(this, e);
        }

        private bool _shutOffCollectionChangedEvents = false;

        /// <summary>
        /// Turns off CollectionChanged events on the UI thread if true
        ///
        /// Fires a Reset CollectionChanged event if toggled back to false
        /// </summary>
        public bool ShutOffCollectionChangedEventsOnUiThread
        {
            get => _shutOffCollectionChangedEvents;
            set
            {
                PushToUiThreadSync(() =>
                {
                    if (value == _shutOffCollectionChangedEvents)
                        return;

                    _shutOffCollectionChangedEvents = value;

                    // Conforming to changes made to WPF in .Net 4.5, verification of the source collection for an itemscontrol
                    // is done AFTER the handler for the CollectionChanged event is completed (BeginInvoke'd)
                    // This verification ensures that the ItemsControl generated items is consistent with the bound collection items
                    // if the two are different, then an InvalidOperationException is thrown.

                    // To mitigate this, do a Dispatcher.Wait() before shutting off the CollectionChanged event firing so that this 
                    // verification can complete (processes all BeginInvoke'd actions)
                    /* Handles this scenario:
                     * Add
                     * CollChanged (add)
                     * Shutoff collectionchgd
                     * Add
                     * ItemsControl Verification() (you said there's 1, but now there's 2?  wth??)
                     * Add
                     * Add
                     * Exception caught on Dispatcher thread (InvalidOperation)
                     * Turn on collectionchgd
                     * CollChg (Reset)
                     *
                     * Now that workflow is transformed to this with the Dispatcher.Wait() call:
                     * Add
                     * CollChanged
                     * Dispatcher.Wait() -- ItemsControl Verification() (you said there's 1, and yes there is only 1)
                     * Shutoff collectionchgd
                     * Add
                     * Add
                     * Add
                     * Turn on collectionchgd
                     * CollChg (Reset)
                     */

                    if (UseDispatcherForCollectionChanged && CollectionChanged != null)
                        PlatformImplementation.Dispatcher.Wait();

                    if (!_shutOffCollectionChangedEvents && _changeDetectedWhileNotificationIsShutOff)
                        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                    else
                        _changeDetectedWhileNotificationIsShutOff = false;
                }, true);
            }
        }

        public bool UseDispatcherForCollectionChanged { get; set; } = true;

        #region IConvertible

        /// <summary>
        /// Returns the <see cref="T:System.TypeCode"/> for this instance.
        /// </summary>
        /// <returns>
        /// The enumerated constant that is the <see cref="T:System.TypeCode"/> of the class or value type that implements this interface.
        /// </returns>
        public TypeCode GetTypeCode()
        {
            return TypeCode.Object;
        }

        /// <summary>
        /// Converts the value of this instance to an equivalent Boolean value using the specified culture-specific formatting information.
        /// </summary>
        /// <returns>
        /// A Boolean value equivalent to the value of this instance.
        /// </returns>
        /// <param name="provider">An <see cref="T:System.IFormatProvider"/> interface implementation that supplies culture-specific formatting information. </param>
        public bool ToBoolean(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Converts the value of this instance to an equivalent Unicode character using the specified culture-specific formatting information.
        /// </summary>
        /// <returns>
        /// A Unicode character equivalent to the value of this instance.
        /// </returns>
        /// <param name="provider">An <see cref="T:System.IFormatProvider"/> interface implementation that supplies culture-specific formatting information. </param>
        public char ToChar(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Converts the value of this instance to an equivalent 8-bit signed integer using the specified culture-specific formatting information.
        /// </summary>
        /// <returns>
        /// An 8-bit signed integer equivalent to the value of this instance.
        /// </returns>
        /// <param name="provider">An <see cref="T:System.IFormatProvider"/> interface implementation that supplies culture-specific formatting information. </param>
        public sbyte ToSByte(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Converts the value of this instance to an equivalent 8-bit unsigned integer using the specified culture-specific formatting information.
        /// </summary>
        /// <returns>
        /// An 8-bit unsigned integer equivalent to the value of this instance.
        /// </returns>
        /// <param name="provider">An <see cref="T:System.IFormatProvider"/> interface implementation that supplies culture-specific formatting information. </param>
        public byte ToByte(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Converts the value of this instance to an equivalent 16-bit signed integer using the specified culture-specific formatting information.
        /// </summary>
        /// <returns>
        /// An 16-bit signed integer equivalent to the value of this instance.
        /// </returns>
        /// <param name="provider">An <see cref="T:System.IFormatProvider"/> interface implementation that supplies culture-specific formatting information. </param>
        public short ToInt16(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Converts the value of this instance to an equivalent 16-bit unsigned integer using the specified culture-specific formatting information.
        /// </summary>
        /// <returns>
        /// An 16-bit unsigned integer equivalent to the value of this instance.
        /// </returns>
        /// <param name="provider">An <see cref="T:System.IFormatProvider"/> interface implementation that supplies culture-specific formatting information. </param>
        public ushort ToUInt16(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Converts the value of this instance to an equivalent 32-bit signed integer using the specified culture-specific formatting information.
        /// </summary>
        /// <returns>
        /// An 32-bit signed integer equivalent to the value of this instance.
        /// </returns>
        /// <param name="provider">An <see cref="T:System.IFormatProvider"/> interface implementation that supplies culture-specific formatting information. </param>
        public int ToInt32(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Converts the value of this instance to an equivalent 32-bit unsigned integer using the specified culture-specific formatting information.
        /// </summary>
        /// <returns>
        /// An 32-bit unsigned integer equivalent to the value of this instance.
        /// </returns>
        /// <param name="provider">An <see cref="T:System.IFormatProvider"/> interface implementation that supplies culture-specific formatting information. </param>
        public uint ToUInt32(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Converts the value of this instance to an equivalent 64-bit signed integer using the specified culture-specific formatting information.
        /// </summary>
        /// <returns>
        /// An 64-bit signed integer equivalent to the value of this instance.
        /// </returns>
        /// <param name="provider">An <see cref="T:System.IFormatProvider"/> interface implementation that supplies culture-specific formatting information. </param>
        public long ToInt64(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Converts the value of this instance to an equivalent 64-bit unsigned integer using the specified culture-specific formatting information.
        /// </summary>
        /// <returns>
        /// An 64-bit unsigned integer equivalent to the value of this instance.
        /// </returns>
        /// <param name="provider">An <see cref="T:System.IFormatProvider"/> interface implementation that supplies culture-specific formatting information. </param>
        public ulong ToUInt64(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Converts the value of this instance to an equivalent single-precision floating-point number using the specified culture-specific formatting information.
        /// </summary>
        /// <returns>
        /// A single-precision floating-point number equivalent to the value of this instance.
        /// </returns>
        /// <param name="provider">An <see cref="T:System.IFormatProvider"/> interface implementation that supplies culture-specific formatting information. </param>
        public float ToSingle(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Converts the value of this instance to an equivalent double-precision floating-point number using the specified culture-specific formatting information.
        /// </summary>
        /// <returns>
        /// A double-precision floating-point number equivalent to the value of this instance.
        /// </returns>
        /// <param name="provider">An <see cref="T:System.IFormatProvider"/> interface implementation that supplies culture-specific formatting information. </param>
        public double ToDouble(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Converts the value of this instance to an equivalent <see cref="T:System.Decimal"/> number using the specified culture-specific formatting information.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Decimal"/> number equivalent to the value of this instance.
        /// </returns>
        /// <param name="provider">An <see cref="T:System.IFormatProvider"/> interface implementation that supplies culture-specific formatting information. </param>
        public decimal ToDecimal(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Converts the value of this instance to an equivalent <see cref="T:System.DateTime"/> using the specified culture-specific formatting information.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.DateTime"/> instance equivalent to the value of this instance.
        /// </returns>
        /// <param name="provider">An <see cref="T:System.IFormatProvider"/> interface implementation that supplies culture-specific formatting information. </param>
        public DateTime ToDateTime(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Converts the value of this instance to an equivalent <see cref="T:System.String"/> using the specified culture-specific formatting information.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> instance equivalent to the value of this instance.
        /// </returns>
        /// <param name="provider">An <see cref="T:System.IFormatProvider"/> interface implementation that supplies culture-specific formatting information. </param>
        public string ToString(IFormatProvider provider)
        {
            return base.ToString();
        }

        /// <summary>
        /// Converts the value of this instance to an <see cref="T:System.Object"/> of the specified <see cref="T:System.Type"/> that has an equivalent value, using the specified culture-specific formatting information.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Object"/> instance of type <paramref name="conversionType"/> whose value is equivalent to the value of this instance.
        /// </returns>
        /// <param name="conversionType">The <see cref="T:System.Type"/> to which the value of this instance is converted. </param><param name="provider">An <see cref="T:System.IFormatProvider"/> interface implementation that supplies culture-specific formatting information. </param>
        public object ToType(Type conversionType, IFormatProvider provider)
        {
            //logger.Debug(@"Converting from type '{0}' to SafeObservableCollection");
            return Convert.ChangeType(this, conversionType, provider);
        }

        #endregion

        /// <summary>
        /// Populates a <see cref="T:System.Runtime.Serialization.SerializationInfo"/> with the data needed to serialize the target object.
        /// </summary>
        /// <param name="info">The <see cref="T:System.Runtime.Serialization.SerializationInfo"/> to populate with data. </param><param name="context">The destination (see <see cref="T:System.Runtime.Serialization.StreamingContext"/>) for this serialization. </param><exception cref="T:System.Security.SecurityException">The caller does not have the required permission. </exception>
        [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(@"Items", _collection.ToList());
            info.AddValue(@"BlockCollectionChanged", this.ShutOffCollectionChangedEventsOnUiThread);
            info.AddValue(@"UseDispatcherForCollectionChanged", this.UseDispatcherForCollectionChanged);
            info.AddValue(@"TrackChanges", this.TrackChanges);
        }

        private List<TType> _itemsDeserialized = null;
        private bool _changeDetectedWhileNotificationIsShutOff;

        protected TrackableCollection(SerializationInfo info, StreamingContext context)
        {
            var enumerator = info.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var key = enumerator.Name;
                if (key == @"UseDispatcherForCollectionChanged")
                    UseDispatcherForCollectionChanged = (bool)enumerator.Value;
                else if (key == @"BlockCollectionChanged")
                    _shutOffCollectionChangedEvents = (bool)enumerator.Value;
                else if (key == @"Items")
                    _itemsDeserialized = info.GetValue(@"Items", typeof(List<TType>)) as List<TType>;
                else if (key == @"TrackChanges")
                    TrackChanges = (bool)info.GetValue(@"TrackChanges", typeof(bool));
            }
        }

        public virtual IEnumerator<TType> GetEnumerator()
        {
            List<TType> result = null;
            lock (SyncRoot)
            {
                result = _collection.ToList();
            }

            Debug.Assert(result != null);
            return result.GetEnumerator();
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (_itemsDeserialized != null)
            {
                // don't fire collection changed events after deserialization
                // can cause stackoverflows if listeners are created on this collection during deserialization
                _collection.AddRange(_itemsDeserialized);
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the collection.
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #region IQueue<TType>

        /// <inheritdoc />
        void IQueue<TType>.Enqueue(TType item)
        {
            Add(item);
        }

        /// <inheritdoc />
        TType IQueue<TType>.Dequeue()
        {
            if (!((IQueue<TType>)this).TryDequeue(out var item))
            {
                throw new IndexOutOfRangeException($"Queue is empty!");
            }

            return item;
        }

        /// <inheritdoc />
        bool IQueue<TType>.TryDequeue(out TType item)
        {
            item = default(TType);
            lock (SyncRoot)
            {
                if (_collection.Count == 0)
                    return false;

                item = _collection[0];
                _collection.RemoveAt(0);
                return true;
            }
        }

        /// <inheritdoc />
        bool IQueue<TType>.Any()
        {
            return Count > 0;
        }

        IEnumerable<TType> IQueue<TType>.GetEnumerable()
        {
            lock (SyncRoot)
                return this.ToList();
        }

        #endregion

        #region IStack<TType>

        /// <inheritdoc />
        void IStack<TType>.Push(TType item)
        {
            Insert(0, item);
        }

        /// <inheritdoc />
        TType IStack<TType>.Pop()
        {
            if (!((IStack<TType>)this).TryPop(out var item))
            {
                throw new IndexOutOfRangeException($"Stack is empty!");
            }

            return item;
        }

        /// <inheritdoc />
        bool IStack<TType>.TryPop(out TType item)
        {
            item = default(TType);
            lock (SyncRoot)
            {
                if (_collection.Count == 0)
                    return false;

                item = _collection[0];
                _collection.RemoveAt(0);
                return true;
            }
        }

        /// <inheritdoc />
        bool IStack<TType>.Any()
        {
            return Count > 0;
        }

        IEnumerable<TType> IStack<TType>.GetEnumerable()
        {
            lock (SyncRoot)
                return this.ToList();
        }


        #endregion
    }
}