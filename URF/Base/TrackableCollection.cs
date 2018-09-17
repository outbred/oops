using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Threading;
using System.Threading.Tasks;
using URF.Interfaces;

namespace UI.Models.Collections
{
    /// <summary>
    /// An observable collection that ensures it is always on the correct thread for collectionchanged and propertychanged events (based on subscriber's thread)
    /// 
    /// This collection is also thread-safe (concurrency is ok)
    ///
    /// Also tracks changes for undo using TrackableScope
    /// </summary>
    /// <typeparam name="TType"></typeparam>
    [Serializable]
    public class deprecated_TrackableCollection<TType> : IList<TType>, IList, IReadOnlyList<TType>, IConvertible, ISerializable, INotifyCollectionChanged, INotifyPropertyChanged
    {
        #region private fields/props

        private readonly List<TType> _collection = new List<TType>();
        private bool _forceCollectionChanged = false;
        private bool _doNotTrackChanges;
        private bool _inBulkChgTrack;

        [NonSerialized]
        private ReaderWriterLockSlim _locker = null;

        public ReaderWriterLockSlim Locker
        {
            get
            {
                if (_locker == null)
                    _locker = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
                return _locker;
            }
        }

        #endregion

        #region ctor

        public deprecated_TrackableCollection(IEnumerable<TType> collection)
        {
            _collection = new List<TType>(collection.ToList()); // added ToList()  - Joe (08 Apr 2016)
            _count = _collection.Count;
            NeverOnUiThread = true;
        }

        public deprecated_TrackableCollection()
        {
            NeverOnUiThread = true;
        }

        #endregion

        // A mechanism that allows collections (Children, WhoHoldsMe) to wait until changes have been made
        // and track/publish them en masse - Neil (31 Mar 2016)

        private List<TType> beginBulkState;
        private bool orgDoNotTrack;

        [Serializable]
        private class SimpleMonitor : IDisposable
        {
            private int _busyCount;

            public bool Busy => this._busyCount > 0;

            public void Enter()
            {
                this._busyCount = this._busyCount + 1;
            }

            public void Dispose()
            {
                this._busyCount = this._busyCount - 1;
            }
        }

        private SimpleMonitor _monitor = new SimpleMonitor();

        protected void CheckReentrancy()
        {
            if (this._monitor.Busy && this.CollectionChanged != null && this.CollectionChanged.GetInvocationList().Length > 1)
            {
                // this could be bad... Brent (27 Jan 2016)
                //throw new InvalidOperationException("Reentrancy not allowed.");
            }
        }

        protected IDisposable BlockReentrancy()
        {
            this._monitor.Enter();
            return this._monitor;
        }

        #region Thread-safe methods

        /// <summary>
        /// Moves an item from oldIndex to newIndex.  Fires a CollectionChanged.Reset event to avoid latency issues when completed
        /// </summary>
        /// <param name="oldIndex"></param>
        /// <param name="newIndex"></param>
        public void Move(int oldIndex, int newIndex)
        {
            var oldItem = this[oldIndex];
            try
            {
                CheckReentrancy();
                if (!Locker.IsWriteLockHeld)
                    Locker.EnterWriteLock();
                _collection.RemoveAt(oldIndex);
                // newIndex is the target index before the remove occurred, and must be adjusted to still be valid - Brent (11 Mar 2016)
                // if adjacent items are being swapped, unless adjusted the newIndex == oldIndex. Hence '<=' instead of just '<'
                if (newIndex > 0 && newIndex <= oldIndex)
                    newIndex--;

                if (newIndex > (_collection.Count - 1) || newIndex < 0)
                    _collection.Add(oldItem);
                else
                    _collection.Insert(newIndex, oldItem);

                newIndex = _collection.IndexOf(oldItem);

                if (!DoNotTrackChanges)
                {
                   TrackableScope.Current?.TrackChange("Items", this, () => null, oldItems => { this.Move(newIndex, oldIndex); });
                }

                // this event can be received and processed long after another move has occurred on the collection, making the newIndex invalid (possibly) - Brent (11 Mar 2016)
                // to work around this, fire a reset event that will cause a full refresh of the collection.  This could have unfortunate UI side-effects of flashing,
                // but thread-safety comes at this price (until we can figure something better out)
                this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
            finally
            {
                if (Locker.IsWriteLockHeld)
                    Locker.ExitWriteLock();
            }
        }

        /// <summary>
        /// Removes all items from the collection.
        /// </summary>
        protected void ClearItems()
        {
            var chgd = Count > 0;
            try
            {
                this.CheckReentrancy();
                if (!Locker.IsWriteLockHeld)
                    Locker.EnterWriteLock();
                if (!DoNotTrackChanges && chgd)
                {
                   TrackableScope.Current?.TrackChange("Items", this, _collection.ToList, oldItems => { this.AddRange(oldItems as IList<TType>); });
                }
                _collection.Clear();
                _count = 0;
                if (chgd)
                    this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
            finally
            {
                Debug.Assert(Count == 0);

                if (Locker.IsWriteLockHeld)
                    Locker.ExitWriteLock();
            }
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
            var prev = Count;
            try
            {
                this.CheckReentrancy();
                if (!Locker.IsWriteLockHeld)
                    Locker.EnterWriteLock();
                if (!DoNotTrackChanges)
                {
                   TrackableScope.Current?.TrackChange("Items", this, () => current, oldItem => { this.InsertItem(index, current); });
                }
                _collection.RemoveAt(index);
                _count--;
                this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, current));
            }
            finally
            {
                Debug.Assert(Count != prev);

                if (Locker.IsWriteLockHeld)
                    Locker.ExitWriteLock();
            }
        }

        /// <summary>
        /// Inserts an item into the collection at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param><param name="item">The object to insert.</param>
        protected void InsertItem(int index, TType item)
        {
            var prev = Count;
            try
            {
                this.CheckReentrancy();
                if (!Locker.IsWriteLockHeld)
                    Locker.EnterWriteLock();
                if (!DoNotTrackChanges)
                {
                    TrackableScope.Current?.TrackChange("Items", this, () => null, oldItems => { this.Remove(item); });
                }
                var indexAdded = 0;
                if (index > (_collection.Count - 1) || index < 0)
                {
                    _collection.Add(item);
                    indexAdded = _collection.Count - 1; //  - Joe (08 Apr 2016)
                }
                else
                {
                    _collection.Insert(index, item);
                    indexAdded = index; //  - Joe (08 Apr 2016)
                }
                _count++;

                //Debug.Assert(Count != prev);
                this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, indexAdded));
            }
            finally
            {
                if (Locker.IsWriteLockHeld)
                    Locker.ExitWriteLock();
            }
        }

        /// <summary>
        /// Removes the first occurrence of a specific object from the <see cref="T:System.Collections.IList"/>.
        /// </summary>
        /// <param name="value">The object to remove from the <see cref="T:System.Collections.IList"/>. </param><exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.IList"/> is read-only.-or- The <see cref="T:System.Collections.IList"/> has a fixed size. </exception>
        public void Remove(object value)
        {
            var index = this._collection.IndexOf((TType)value);
            RemoveItem(index);
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
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
            get
            {
                TType item = default(TType);
                try
                {
                    if (!_insideCollectionChanged && !Locker.IsReadLockHeld && !Locker.IsWriteLockHeld)
                        Locker.EnterReadLock();
                    if (_collection.Count > index)
                        item = _collection[index];
                }
                finally
                {
                    if (Locker.IsReadLockHeld)
                        Locker.ExitReadLock();
                }
                return item;
            }
            set
            {
                if (index < 0 || index >= Count)
                    throw new ArgumentOutOfRangeException();
                this.SetItem(index, value);
            }
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
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
            get
            {
                TType item = default(TType);
                try
                {
                    if (!_insideCollectionChanged && !Locker.IsReadLockHeld && !Locker.IsWriteLockHeld)
                        Locker.EnterReadLock();
                    // Rather than throw an exception and return default, just return default if index out of range - Neil (30 Mar 2016)
                    if (_collection.Count > index)
                        item = _collection[index];
                }
                finally
                {
                    if (Locker.IsReadLockHeld)
                        Locker.ExitReadLock();
                }
                return item;
            }
            set
            {
                if (index < 0 || index >= _count)
                    throw new ArgumentOutOfRangeException();

                this.SetItem(index, (TType)value);
            }
        }

        /// <summary>
        /// Removes the first occurrence of a specific object from the <see cref="T:System.Collections.Generic.ICollection`1"/>.
        /// </summary>
        /// <returns>
        /// true if <paramref name="item"/> was successfully removed from the <see cref="T:System.Collections.Generic.ICollection`1"/>; otherwise, false. This method also returns false if <paramref name="item"/> is not found in the original <see cref="T:System.Collections.Generic.ICollection`1"/>.
        /// </returns>
        /// <param name="item">The object to remove from the <see cref="T:System.Collections.Generic.ICollection`1"/>.</param><exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only.</exception>
        bool ICollection<TType>.Remove(TType item)
        {
            return Remove(item);
        }

        public bool Remove(TType item)
        {
            bool removed = false;
            try
            {
                if (!Locker.IsWriteLockHeld)
                    Locker.EnterWriteLock();
                int idx = _collection.IndexOf(item);
                removed = _collection.Remove(item);
                if (removed)
                {
                    if (!DoNotTrackChanges)
                    {
                        TrackableScope.Current?.TrackChange("Items", this, () => null, oldItems => { this.InsertItem(idx, item); });
                    }
                    _count--;
                }
                // Only raise the event if the collection actually changed - Neil (27 Oct 2015)
                if (removed)
                    this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item));
            }
            finally
            {
                if (Locker.IsWriteLockHeld)
                    Locker.ExitWriteLock();
            }
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
            try
            {
                Locker.EnterUpgradeableReadLock();
                if (_collection.Any())
                    _collection.CopyTo((TType[])array, index);
            }
            finally
            {
                if (Locker.IsUpgradeableReadLockHeld)
                    Locker.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Only updated within a write-lock, so we should be good to always read from it
        /// </summary>
        private int _count = 0;

        public int Count => _count;

        /// <summary>
        /// Gets an object that can be used to synchronize access to the <see cref="T:System.Collections.ICollection"/>.
        /// </summary>
        /// <returns>
        /// An object that can be used to synchronize access to the <see cref="T:System.Collections.ICollection"/>.
        /// </returns>
        public object SyncRoot => Locker;

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
            try
            {
                this.CheckReentrancy();
                if (!Locker.IsWriteLockHeld)
                    Locker.EnterWriteLock();
                if (!DoNotTrackChanges)
                {
                    TrackableScope.Current?.TrackChange("Items", this, () => null, oldItems => { SetItem(index, current); });
                }
                _collection[index] = item;
                this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, current, item, index));
            }
            finally
            {
                if (Locker.IsWriteLockHeld)
                    Locker.ExitWriteLock();
            }
        }

        /// <summary>
        /// Adds an item to the <see cref="T:System.Collections.Generic.ICollection`1"/>.
        /// </summary>
        /// <param name="item">The object to add to the <see cref="T:System.Collections.Generic.ICollection`1"/>.</param><exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only.</exception>
        public void Add(TType item)
        {
            InsertItem(_count, item);
        }

        void ICollection<TType>.Add(TType item)
        {
            InsertItem(_count, item);
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
            var idx = _count;
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
        bool IList.Contains(object value)
        {
            return Contains((TType)value);
        }

        /// <summary>
        /// Removes all items from the <see cref="T:System.Collections.IList"/>.
        /// </summary>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.IList"/> is read-only. </exception>
        public void Clear()
        {
            ClearItems();
        }

        /// <summary>
        /// Determines the index of a specific item in the <see cref="T:System.Collections.IList"/>.
        /// </summary>
        /// <returns>
        /// The index of <paramref name="value"/> if found in the list; otherwise, -1.
        /// </returns>
        /// <param name="value">The object to locate in the <see cref="T:System.Collections.IList"/>. </param>
        int IList.IndexOf(object value)
        {
            return IndexOf((TType)value);
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
        void ICollection<TType>.Clear()
        {
            ClearItems();
        }

        public bool Contains(TType item)
        {
            bool contains = false;
            try
            {
                if (!_insideCollectionChanged && !Locker.IsReadLockHeld && !Locker.IsWriteLockHeld)
                    Locker.EnterReadLock();
                contains = _collection.Contains(item);
            }
            finally
            {
                if (Locker.IsReadLockHeld)
                    Locker.ExitReadLock();
            }
            return contains;
        }

        public new void CopyTo(TType[] array, int index)
        {
            try
            {
                // Safer Copy; Cubby's GetChildren kept failing trying to copy the Toy's Array into a temp variable
                // because the origional would grow and exceed the target's size - Neil (01 Apr 2016)

                if (!_insideCollectionChanged)
                    Locker.EnterUpgradeableReadLock();
                if (_collection.Any())
                    _collection.CopyTo(array, index);
            }
            finally
            {
                if (Locker.IsUpgradeableReadLockHeld)
                    Locker.ExitUpgradeableReadLock();
            }
        }

        public int IndexOf(TType item)
        {
            int index = 0;
            try
            {
                if (!_insideCollectionChanged && !Locker.IsReadLockHeld && !Locker.IsWriteLockHeld)
                    Locker.EnterReadLock();
                index = _collection.IndexOf(item);
            }
            finally
            {
                if (Locker.IsReadLockHeld)
                    Locker.ExitReadLock();
            }
            return index;
        }

        /// <summary>
        /// Inserts an item to the <see cref="T:System.Collections.Generic.IList`1"/> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param><param name="item">The object to insert into the <see cref="T:System.Collections.Generic.IList`1"/>.</param><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index in the <see cref="T:System.Collections.Generic.IList`1"/>.</exception><exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.IList`1"/> is read-only.</exception>
        public void Insert(int index, TType item)
        {
            InsertItem(index, item);
        }

        #endregion

        /// <summary>
        /// Inserts values beginning at the given position.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="stuff"></param>
        public void InsertRange(int position, IEnumerable<TType> stuff)
        {
            if (stuff == null || ReferenceEquals(stuff, this))
                return;
            var toAdd = stuff as IList<TType> ?? stuff.ToList(); // Needed this outside debug - broke release build since used below - Joe (04 Nov 2015)
#if DEBUG
            var expected = _count + toAdd.Count();
#endif
            try
            {
                if (!Locker.IsWriteLockHeld)
                    Locker.EnterWriteLock();

                if (!DoNotTrackChanges)
                {
                    TrackableScope.Current?.TrackChange("Items", this, () => null, oldItems => { this.RemoveRange(toAdd); });
                }

                foreach (var item in toAdd)
                {
                    _collection.Insert(position++, item);
                    _count++;
                }
                this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
            finally
            {
#if DEBUG
                Debug.Assert(_count == expected);
#endif

                if (Locker.IsWriteLockHeld)
                    Locker.ExitWriteLock();

            }
        }

        /// <summary>
        /// Efficient bulk-add to an observable collection
        /// </summary>
        /// <param name="stuff"></param>
        public void AddRange(IEnumerable<TType> stuff)
        {
            InsertRange(_collection.Count, stuff);
        }

        [field: NonSerialized]
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        [field: NonSerialized]
        public event PropertyChangedEventHandler PropertyChanged;

        public bool DoNotTrackChanges
        {
            get { return _doNotTrackChanges; }
            set
            {
                _doNotTrackChanges = value;
            }
        }

        /// <summary>
        /// Raises the <see cref="E:System.Collections.ObjectModel.ObservableCollection`1.CollectionChanged"/> event with the provided arguments asynchronously.
        /// 
        /// Each event is raised on subscriber's thread
        /// </summary>
        /// <param name="e">Arguments of the event being raised.</param>
        protected void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            _changeDetectedWhileNotificationIsShutOff = ShutOffCollectionChangedEventsOnUiThread;

            if (!NeverOnUiThread && ShutOffCollectionChangedEventsOnUiThread)
                return;

            _changeDetectedWhileNotificationIsShutOff = false;
            _insideCollectionChanged = true;

            try
            {
                using (BlockReentrancy())
                {
                    if (CollectionChanged != null)
                    {

                        if (NeverOnUiThread)
                        {
                            if (e.Action != NotifyCollectionChangedAction.Move && e.Action != NotifyCollectionChangedAction.Replace)
                                this.OnPropertyChanged(nameof(Count));
                            this.OnPropertyChanged("Item[]");
                            try
                            {
                                CollectionChanged?.Invoke(this, e);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Unable to fire collection changed event cleanly.\n{ex.Message}\n{ex.StackTrace}");
                            }
                        }
                        else
                        {
                            try
                            {
                                if (e.Action != NotifyCollectionChangedAction.Move && e.Action != NotifyCollectionChangedAction.Replace)
                                    this.OnPropertyChanged(nameof(Count));
                                this.OnPropertyChanged("Item[]");
                                // check for null inside this b/c the handler could've been removed on the context switch <sigh> - Brent 22 April 2016
                                PushToUiThreadSync(() => CollectionChanged?.Invoke(this, e));
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Unable to fire collection changed event cleanly.\n{ex.Message}\n{ex.StackTrace}");
                            }
                        }
                    }
                }
            }
            finally
            {
                _insideCollectionChanged = false;
            }
        }

        private static readonly IDispatcher _dispatcher = null;
        private void PushToUiThreadSync(Action action)
        {
            var allDone = new ManualResetEvent(false);
            _dispatcher.BeginInvoke(() =>
            {
                action();
                allDone.Set();
            });
            // don't ever wait on the ui thread
            if (!_dispatcher.CheckAccess())
                allDone.WaitOne(100);
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

        public bool ShutOffCollectionChangedEventsOnUiThread
        {
            get { return _shutOffCollectionChangedEvents; }
            set
            {
                var prev = _shutOffCollectionChangedEvents;
                _shutOffCollectionChangedEvents = value;
                if (prev != _shutOffCollectionChangedEvents && !_shutOffCollectionChangedEvents && _changeDetectedWhileNotificationIsShutOff)
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                else if (prev != _shutOffCollectionChangedEvents)
                    _changeDetectedWhileNotificationIsShutOff = false;
            }
        }

        public bool NeverOnUiThread
        {
            get { return _neverOnUiThread; }
            set { _neverOnUiThread = value; }
        }

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
            Debug.WriteLine("Converting from type '{0}' to TrackableCollection");
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
            info.AddValue("Items", _collection.ToList());
            info.AddValue("BlockCollectionChanged", this.ShutOffCollectionChangedEventsOnUiThread);
            info.AddValue("NeverOnUiThread", this.NeverOnUiThread);
        }

        private List<TType> _itemsDeserialized = null;
        private bool _neverOnUiThread;
        private bool _changeDetectedWhileNotificationIsShutOff;
        private bool _insideCollectionChanged;

        protected deprecated_TrackableCollection(SerializationInfo info, StreamingContext context)
        {
            _shutOffCollectionChangedEvents = info.GetBoolean("BlockCollectionChanged");
            _itemsDeserialized = info.GetValue("Items", typeof(List<TType>)) as List<TType>;

            // Until we get schema check
            var enumerator = info.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var key = enumerator.Name;
                if (key == "NeverOnUiThread")
                    NeverOnUiThread = (bool)enumerator.Value;
            }
        }

        public IEnumerator<TType> GetEnumerator()
        {
            List<TType> result = null;
            try
            {
                if (!_insideCollectionChanged && !Locker.IsWriteLockHeld)
                    Locker.EnterWriteLock();
                result = _collection.ToList();
            }
            finally
            {
                try
                {
                    if (Locker.IsWriteLockHeld)
                        Locker.ExitWriteLock();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Unable to successfully release the write lock for enumerating over a collection...but let's just move on.\n{ex.Message}\n{ex.StackTrace}");
                }
            }
            Debug.Assert(result != null);
            return result.GetEnumerator();
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (_itemsDeserialized != null)
            {
                // don't fire collection changed events after deserialization - bbulla
                // can cause stackoverflows if listeners are creating on this collection during deserialization
                _collection.AddRange(_itemsDeserialized);
                _count = _collection.Count;
            }
        }

        #region Implementation of IEnumerable

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

        #endregion

        public void RemoveRange(IEnumerable<TType> children)
        {
            var enumerable = children as IList<TType> ?? children.ToList();
            try
            {
                bool anyRemoved = false;
                this.CheckReentrancy();
                if (!Locker.IsWriteLockHeld)
                    Locker.EnterWriteLock();

                var tuples = new List<Tuple<int, TType>>();
                foreach (var child in enumerable)
                {
                    var index = _collection.IndexOf(child);
                    if (index >= 0)
                    {
                        tuples.Add(new Tuple<int, TType>(index, child));
                        _collection.RemoveAt(index);
                        _count--;
                        anyRemoved = true;
                    }
                }

                if (!DoNotTrackChanges && anyRemoved)
                {
                    TrackableScope.Current?.TrackChange("Items", this, () => null, _ =>
                    {
                        // I would like an InsertRange here, but that doesn't ensure that indices are restored correctly - Brent (03 Mar 2016)
                        foreach (var tuple in tuples)
                            this.InsertItem(tuple.Item1, tuple.Item2);
                    });
                }
                if (anyRemoved)
                    this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
            finally
            {
                // this is not an accurate asssertion b/c any of the items may not exist in the collection - Brent (03 Mar 2016)
                // Debug.Assert(Count == (prev - enumerable.Count));

                //this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

                if (Locker.IsWriteLockHeld)
                    Locker.ExitWriteLock();


            }
        }

        public List<TType> ToList()
        {
            try
            {
                if (!_insideCollectionChanged && !Locker.IsWriteLockHeld)
                    Locker.EnterWriteLock();
                var result = _collection.ToList();
                return result;
            }
            finally
            {
                try
                {
                    if (Locker.IsWriteLockHeld)
                        Locker.ExitWriteLock();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Unable to successfully release the write lock for enumerating over a collection...but let's just move on.\n{ex.Message}\n{ex.StackTrace}");
                }
            }
        }
    }

    /// <summary>
    /// An observable collection that ensures it is always on the correct thread for collectionchanged and propertychanged events (based on subscriber's thread)
    /// 
    /// This collection is also thread-safe (concurrency is ok)
    /// </summary>
    /// <typeparam name="TType"></typeparam>
    [Serializable]
    [DebuggerDisplay(@"Count = {Count}")]
    public class TrackableCollection<TType> : IList<TType>, IList, IReadOnlyList<TType>, IConvertible, ISerializable, INotifyCollectionChanged, INotifyPropertyChanged
    {
        #region private fields/props

        protected readonly List<TType> _collection = new List<TType>();

        #endregion

        #region ctor

        public TrackableCollection(IEnumerable<TType> collection) : this()
        {

            _collection = collection == null ? new List<TType>() : new List<TType>(collection.ToList()); // added ToList()  - Joe (08 Apr 2016)
            _count = _collection.Count;
        }

        public TrackableCollection()
        {
            NeverOnUiThread = true;
        }

        #endregion

        public bool TrackChanges { get; set; } = true;

        // A mechanism that allows collections (Children, WhoHoldsMe) to wait until changes have been made
        // and track/publish them en masse - Neil (31 Mar 2016)
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
        /// Moves an item from oldIndex to newIndex.  Fires a CollectionChanged.Reset event to avoid latency issues when completed
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
                lock (this)
                {
                    CheckReentrancy();
                    _collection.RemoveAt(oldIndex);

                    /*
                     * Handles this scenario: - Brent (26 Oct 2017)
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
                        TrackableScope.Current?.TrackChange("Items", this, () => null, oldItems => { this.Move(newIndex, oldIndex); });

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
                lock (this)
                {
                    this.CheckReentrancy();

                    var chgd = Count > 0;

                    var items = _collection.ToList();
                    _collection.Clear();
                    _count = 0;
                    if (chgd)
                    {
                        if (TrackChanges)
                            TrackableScope.Current?.TrackChange("Items", this, () => items, _ =>  this.AddRange(items));
                        this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                    }
                }
            });
        }

        /// <summary>
        /// Removes the item at the specified index of the collection.
        /// </summary>
        /// <param name="index">The zero-based index of the element to remove.</param>
        protected virtual void RemoveItem(int index)
        {
            if (Count <= index)
                return;
            var current = this[index];

            PushToUiThreadSync(() =>
            {
                lock (this)
                {
                    // during the context switch a couple things could have happened: - Brent (24 Feb 2017)
                    // 1. The collection could have been modified (waiting on lock(this) b/c a different Remove/Add call was happening)
                    // 2. The item at the desired index could have changed (say Move() was happening during our wait on the lock)
                    if (Count <= index)
                        return;

                    // ensure the index we publish is correct now that we've context switched
                    index = _collection.IndexOf(current);
                    if (_collection.Remove(current))
                    {
                        this.CheckReentrancy();

                        _count--;
                        if (TrackChanges)
                            TrackableScope.Current?.TrackChange("Items", this, () => null, _ => this.InsertItem(index, current));
                        this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, current, index));
                    }
                }
            });
            //Debug.Assert(Count != prev);
        }

        /// <summary>
        /// Inserts an item into the collection at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param><param name="item">The object to insert.</param>
        protected virtual void InsertItem(int index, TType item)
        {
            PushToUiThreadSync(() =>
            {
                lock (this)
                {
                    this.CheckReentrancy();

                    var indexAdded = 0;
                    if (index > (_collection.Count - 1))
                    {
                        _collection.Add(item);
                        indexAdded = _count; 
                    }
                    else
                    {
                        _collection.Insert(index, item);
                        indexAdded = index; 
                    }
                    _count++;
                    if (TrackChanges)
                        TrackableScope.Current?.TrackChange("Items", this, () => null, _ => this.Remove(item));

                    //Debug.Assert(Count != prev);
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
                lock (this)
                {
                    this.CheckReentrancy();
                    var index = _collection.IndexOf(existingItem);

                    var indexAdded = 0;
                    if (index > (_collection.Count - 1))
                    {
                        _collection.Add(itemInsertedBeforeExisting);
                        indexAdded = _count;
                    }
                    else
                    {
                        _collection.Insert(index, itemInsertedBeforeExisting);
                        indexAdded = index; 
                    }
                    _count++;

                    if (TrackChanges)
                        TrackableScope.Current?.TrackChange("Items", this, () => null, _ => this.Remove(existingItem));

                    //Debug.Assert(Count != prev);
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
                lock (this)
                {
                    this.CheckReentrancy();

                    var item = (TType)value;
                    var idx = _collection.IndexOf(item);
                    if (_collection.Remove(item))
                    {
                        _count--;
                        if (TrackChanges)
                            TrackableScope.Current?.TrackChange("Items", this, () => null, _ => this.InsertItem(idx, item));
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
                TType item = default(TType);
                lock (this)
                {
                    if (index >= 0 && _collection.Count > index)
                        item = _collection[index];
                }
                return item;
            }
            set
            {
                if (index < 0 || index >= Count)
                    throw new ArgumentOutOfRangeException();
                this.SetItem(index, value);
            }
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
                TType item = default(TType);
                lock (this)
                {
                    // Rather than throw an exception and return default, just return default if index out of range - Neil (30 Mar 2016)
                    if (_collection.Count > index)
                        item = _collection[index];
                }
                return item;
            }
            set
            {
                if (index < 0 || index >= _count)
                    throw new ArgumentOutOfRangeException();

                this.SetItem(index, (TType)value);
            }
        }

        /// <summary>
        /// Removes the first occurrence of a specific object from the <see cref="T:System.Collections.Generic.ICollection`1"/>.
        /// </summary>
        /// <returns>
        /// true if <paramref name="item"/> was successfully removed from the <see cref="T:System.Collections.Generic.ICollection`1"/>; otherwise, false. This method also returns false if <paramref name="item"/> is not found in the original <see cref="T:System.Collections.Generic.ICollection`1"/>.
        /// </returns>
        /// <param name="item">The object to remove from the <see cref="T:System.Collections.Generic.ICollection`1"/>.</param><exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only.</exception>
        bool ICollection<TType>.Remove(TType item)
        {
            return Remove(item);
        }

        public bool Remove(TType item)
        {
            bool removed = false;
            PushToUiThreadSync(() =>
            {
                lock (this)
                {
                    var indx = _collection.IndexOf(item);
                    removed = _collection.Remove(item);
                    if (removed)
                    {
                        _count--;
                        if (TrackChanges)
                            TrackableScope.Current?.TrackChange("Items", this, () => null, _ => this.InsertItem(indx, item));
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
            lock (this)
            {
                if (_collection.Any())
                    _collection.CopyTo((TType[])array, index);
            }
        }

        /// <summary>
        /// Only updated within a write-lock, so we should be good to always read from it
        /// </summary>
        private int _count = 0;

        public int Count => _count;

        public int ComputedCount
        {
            get
            {
                lock (this)
                    return this._collection.Count;
            }
        }

        /// <summary>
        /// Gets an object that can be used to synchronize access to the <see cref="T:System.Collections.ICollection"/>.
        /// </summary>
        /// <returns>
        /// An object that can be used to synchronize access to the <see cref="T:System.Collections.ICollection"/>.
        /// </returns>
        public object SyncRoot => this;

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
                lock (this)
                {
                    this.CheckReentrancy();

                    _collection[index] = item;
                    if (TrackChanges)
                        TrackableScope.Current?.TrackChange("Items", this, () => null, _ => this.SetItem(index, current));
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
            InsertItem(_count, item);
        }

        void ICollection<TType>.Add(TType item)
        {
            InsertItem(_count, item);
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
            var idx = _count;
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
        bool IList.Contains(object value)
        {
            return Contains((TType)value);
        }

        /// <summary>
        /// Removes all items from the <see cref="T:System.Collections.IList"/>.
        /// </summary>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.IList"/> is read-only. </exception>
        public void Clear()
        {
            ClearItems();
        }

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
        void ICollection<TType>.Clear()
        {
            ClearItems();
        }

        public bool Contains(TType item)
        {
            bool contains = false;
            lock (this)
            {
                contains = _collection.Contains(item);
            }

            return contains;
        }

        public void CopyTo(TType[] array, int index)
        {
            lock (this)
            {
                // Safer Copy; Cubby's GetChildren kept failing trying to copy the Toy's Array into a temp variable
                // because the origional would grow and exceed the target's size - Neil (01 Apr 2016)

                if (_collection.Any())
                    _collection.CopyTo(array, index);
            }
        }

        public int IndexOf(TType item)
        {
            int index = 0;
            lock (this)
            {
                index = _collection.IndexOf(item);
            }
            return index;
        }

        /// <summary>
        /// Inserts an item to the <see cref="T:System.Collections.Generic.IList`1"/> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param><param name="item">The object to insert into the <see cref="T:System.Collections.Generic.IList`1"/>.</param><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index in the <see cref="T:System.Collections.Generic.IList`1"/>.</exception><exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.IList`1"/> is read-only.</exception>
        public void Insert(int index, TType item)
        {
            InsertItem(index, item);
        }

        /// <summary>
        /// Inserts values beginning at the given position.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="stuff"></param>
        public void InsertRange(int position, IEnumerable<TType> stuff)
        {
            if (stuff == null || ReferenceEquals(stuff, this))
                return;
            var toAdd = stuff as IList<TType> ?? stuff.ToList(); // Needed this outside debug - broke release build since used below - Joe (04 Nov 2015)
            PushToUiThreadSync(() =>
            {
                lock (this)
                {
                    //var start = position;
                    //int count = 0;
                    foreach (var item in toAdd)
                    {
                        _collection.Insert(position++, item);
                        _count++;
                        if (TrackChanges)
                            TrackableScope.Current?.TrackChange("Items", this, () => null, _ => this.Remove(item));
                    }

                    this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                }
            });

        }

        /// <summary>
        /// Efficient bulk-add to an observable collection
        /// </summary>
        /// <param name="stuff"></param>
        public void AddRange(IEnumerable<TType> stuff)
        {
            InsertRange(_collection.Count, stuff);
        }

        public void RemoveRange(IEnumerable<TType> children)
        {
            var enumerable = children as IList<TType> ?? children.ToList();
            PushToUiThreadSync(() =>
            {
                lock (this)
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
                            _count--;
                            anyRemoved = true;
                        }
                        else
                            Debug.WriteLine($"Unable to remove item {child.ToString()} b/c it is not in the collection.");
                    }

                    if (TrackChanges && anyRemoved)
                    {
                        TrackableScope.Current?.TrackChange("Items", this, () => null, _ =>
                        {
                            // I would like an InsertRange here, but that doesn't ensure that indices are restored correctly - Brent (03 Mar 2016)
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
            lock (this)
            {
                var result = _collection.ToList();
                return result;
            }
        }

        public void AddIfNew(TType item)
        {
            PushToUiThreadSync(() =>
            {
                lock (this)
                {
                    this.CheckReentrancy();

                    if (_collection.Contains(item))
                        return;

                    _collection.Add(item);
                    var indexAdded = _collection.Count - 1;
                    _count++;

                    if (TrackChanges)
                        TrackableScope.Current?.TrackChange("Items", this, () => null, _ => Remove(item));
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
        /// 
        /// Each event is raised on subscriber's thread
        /// </summary>
        /// <param name="e">Arguments of the event being raised.</param>
        protected void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            _changeDetectedWhileNotificationIsShutOff = ShutOffCollectionChangedEventsOnUiThread;

            if (!NeverOnUiThread && ShutOffCollectionChangedEventsOnUiThread)
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
                        // check for null inside this b/c the handler could've been removed on the context switch <sigh> - Brent 22 April 2016
                        //if (CollectionChanged == null)
                        //logger.Debug("CollectionChanged handler detached! Will not fire!");
                        CollectionChanged?.Invoke(this, e);
                        //logger.Debug($"CollectionChanged: Count {Count}; type {e.Action}; startingindex {e.NewStartingIndex}; oldindex {e.OldStartingIndex}");
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

        private static IDispatcher _dispatcher = null;
        private static IDispatcher Dispatcher = _dispatcher; //?? (_dispatcher = IoC.Current?.Container.GetInstance<IDispatcher>());
        protected void PushToUiThreadSync(Action action, bool force = false)
        {
            if (action == null)
                return;

            if ((!force && ShutOffCollectionChangedEventsOnUiThread) || NeverOnUiThread || (Dispatcher?.CheckAccess() ?? true))
            {
                action();
            }
            else
            {
                var allDone = new ManualResetEvent(false);
                //var start = DateTime.Now;
                Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        Log(@"Unable to finish changing collection", ex);
                    }
                    finally
                    {
                        allDone.Set();
                    }
                });
                allDone.WaitOne();
                //logger.Debug($"Took {(DateTime.Now - start).TotalMilliseconds}ms to push CollectionChanged event.");
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

                    if (!NeverOnUiThread && CollectionChanged != null)
                        Dispatcher.Wait();

                    if (!_shutOffCollectionChangedEvents && _changeDetectedWhileNotificationIsShutOff)
                        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                    else
                        _changeDetectedWhileNotificationIsShutOff = false;
                }, true);
            }
        }

        public bool NeverOnUiThread
        {
            get => _neverOnUiThread;
            set => _neverOnUiThread = value;
        }

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
            info.AddValue(@"NeverOnUiThread", this.NeverOnUiThread);
            info.AddValue(@"TrackChanges", this.TrackChanges);
        }

        private List<TType> _itemsDeserialized = null;
        private bool _neverOnUiThread;
        private bool _changeDetectedWhileNotificationIsShutOff;

        protected TrackableCollection(SerializationInfo info, StreamingContext context)
        {
            var enumerator = info.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var key = enumerator.Name;
                if (key == @"NeverOnUiThread")
                    NeverOnUiThread = (bool)enumerator.Value;
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
            lock (this)
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
                // don't fire collection changed events after deserialization - bbulla
                // can cause stackoverflows if listeners are creating on this collection during deserialization
                _collection.AddRange(_itemsDeserialized);
                _count = _collection.Count;
            }
        }

        #region Implementation of IEnumerable

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

        #endregion
    }
}