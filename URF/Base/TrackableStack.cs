using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Threading;
using System.Threading.Tasks;
using UI.Models.Collections;
using URF.Interfaces;

namespace URF.Base
{
    [Serializable]
    public class TrackableStack<T> : ICollection, IReadOnlyCollection<T>, INotifyPropertyChanged, INotifyCollectionChanged, ISerializable
    {
        #region private fields/props

        [NonSerialized]
        private ReaderWriterLockSlim _locker = null;

        private readonly Stack<T> _stack = new Stack<T>();
        private static readonly IDispatcher Dispatcher = null;
        public bool BlockCollectionChanged { get; private set; } = false;
        private bool _doNotTrackChanges;

        private ReaderWriterLockSlim Locker
        {
            get
            {
                if (_locker == null)
                {
                    _locker = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
                }
                return _locker;
            }
        }

        #endregion

        static TrackableStack()
        {
            // unit test friendly - bbulla
            try
            {
                Dispatcher = IoC.Current.Container.GetInstance<IDispatcherHelper>();
            }
            catch
            {
                Dispatcher = new DispatcherHelper(new DummyLogger());
            }
        }

        public TrackableStack(IEnumerable<T> collection)
        {
            _stack = new Stack<T>(collection);
        }

        public TrackableStack()
        {
            _stack = new Stack<T>();
        }

        public event PropertyChangedEventHandler PropertyChanged = null;
        public event NotifyCollectionChangedEventHandler CollectionChanged = null;

        public bool DoNotTrackChanges
        {
            get { return _doNotTrackChanges; }
            set { _doNotTrackChanges = value; }
        }

        public void AskMeToRefresh()
        {
            //if (CollectionChanged != null)
            //    TrackableCollectionChangeProcessor.QueueCollectionChangeNotice(true, this, CollectionChanged);
            Dispatcher.BeginInvoke(() => CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)));
        }

        /// <summary>
        /// Fires the collection changed event synchrously
        /// </summary>
        /// <param name="action"></param>
        /// <param name="items"></param>
        /// <param name="index"></param>
        private void OnCollectionChanged(NotifyCollectionChangedAction action, List<T> items, int? index = null)
        {
            if (!BlockCollectionChanged && CollectionChanged != null)
            {
                //var idx = index;
                //var copy = items?.ToList();

                //TrackableCollectionChangeProcessor.QueueCollectionChangeNotice(true, this, CollectionChanged);

                // Allowing this to fire async raises exceptions in the SafeEnumerator.MoveNext() (changes raise in UI thread out of order, while collection is being modified)
                // It causes deadlocks in the SafeEnumerator using Invoke; using the regular enumerator and Invoke eliminate problems - Neil (30 Mar 2016)
                //Dispatcher.Invoke
                //    //Dispatcher.BeginInvoke
                //    (() =>
                //    {
                //        try
                //        {
                //            // b/c a stack is so different from a linear collection, just fire this everytime to keep things simple - Brent (18 Dec 2015)
                //            // this will result in UI refreshes instead of smooth list changes, but that's ok since we only use it for undo/redo

                //            //todo - allow this collection to publish real Action (Add, Remove, etc.) - Undo and Redo menus are bound, and it is pushing full reset to those controls!
                //            CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                //        }
                //        catch (Exception ex)
                //        {
                //            IoC.Current.Container.GetInstance<ILogger>().Error("Unable to fire collection changed for TrackableStack", ex);
                //        }
                //    });
            }

            OnPropertyChanged(nameof(Count));
        }

        /// <summary>
        /// Fires the property changed event asynchronously
        /// </summary>
        /// <param name="propName"></param>
        private void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
            {
                Task.Run(() => PropertyChanged(this, new PropertyChangedEventArgs(propName)));
            }
        }

        public void Clear()
        {
            try
            {
                Locker.EnterWriteLock();
                if (!DoNotTrackChanges)
                {
                    TrackableScope.Current?.TrackChange("Items", this, this.ToList, old =>
                    {
                        BlockCollectionChanged = true;
                        this.Clear();
                        var oldItems = (old as List<T>);
                        oldItems.Reverse();
                        PushRange(oldItems);
                        BlockCollectionChanged = false;
                        OnCollectionChanged(NotifyCollectionChangedAction.Reset, oldItems.ToList());
                    });
                }
                _stack.Clear();
            }
            finally
            {
                OnCollectionChanged(NotifyCollectionChangedAction.Reset, null);
                if (Locker.IsWriteLockHeld)
                {
                    Locker.ExitWriteLock();
                }
            }
        }

        public void Reorder<TKey>(Func<T, TKey> keySelector)
        {
            try
            {
                Locker.EnterWriteLock();
                var currentList = _stack.ToList();
                if (!DoNotTrackChanges)
                {
                    TrackableScope.Current?.TrackChange("Items", this, () => null, old =>
                    {
                        BlockCollectionChanged = true;
                        this.Clear();
                        currentList.Reverse();
                        PushRange(currentList);
                        BlockCollectionChanged = false;
                        OnCollectionChanged(NotifyCollectionChangedAction.Reset, currentList.ToList());
                    });
                }

                _stack.Clear();
                var ordered = currentList.OrderBy(keySelector);
                foreach (var o in ordered)
                {
                    _stack.Push(o);
                }
            }
            finally
            {
                OnCollectionChanged(NotifyCollectionChangedAction.Reset, null);
                if (Locker.IsWriteLockHeld)
                {
                    Locker.ExitWriteLock();
                }
            }
        }

        public T Pop()
        {
            T result = default(T);
            try
            {
                Locker.EnterWriteLock();
                if (!DoNotTrackChanges)
                {
                    TrackableScope.Current?.TrackChange("Items", this, this.ToList, old =>
                    {
                        BlockCollectionChanged = true;
                        this.Clear();
                        var oldItems = (old as List<T>);
                        oldItems.Reverse();
                        PushRange(oldItems);
                        BlockCollectionChanged = false;
                        OnCollectionChanged(NotifyCollectionChangedAction.Reset, oldItems.ToList());
                    });
                }
                result = _stack.Pop();
            }
            finally
            {
                OnCollectionChanged(NotifyCollectionChangedAction.Remove, new List<T>() { result }, _stack.Count - 1);
                if (Locker.IsWriteLockHeld)
                {
                    Locker.ExitWriteLock();
                }
            }
            return result;
        }

        public void Push(T item)
        {
            try
            {
                Locker.EnterWriteLock();
                if (!DoNotTrackChanges)
                {
                    TrackableScope.Current?.TrackChange("Items", this, this.ToList, old =>
                    {
                        BlockCollectionChanged = true;
                        this.Clear();
                        var oldItems = (old as List<T>);
                        oldItems.Reverse();
                        PushRange(oldItems);
                        BlockCollectionChanged = false;
                        OnCollectionChanged(NotifyCollectionChangedAction.Reset, oldItems.ToList());
                    });
                }
                _stack.Push(item);
            }
            finally
            {
                OnCollectionChanged(NotifyCollectionChangedAction.Add, new List<T>() { item }, _stack.Count - 1);
                if (Locker.IsWriteLockHeld)
                {
                    Locker.ExitWriteLock();
                }
            }
        }

        public void PushRange(IEnumerable<T> items)
        {
            if (items == null)
            {
                return;
            }

            var enumerable = items as T[] ?? items.ToArray();
            try
            {
                Locker.EnterWriteLock();
                if (!DoNotTrackChanges)
                {
                    TrackableScope.Current?.TrackChange("Items", this, this.ToList, old =>
                    {
                        BlockCollectionChanged = true;
                        this.Clear();
                        var oldItems = (old as List<T>);
                        oldItems.Reverse();
                        PushRange(oldItems);
                        BlockCollectionChanged = false;
                        OnCollectionChanged(NotifyCollectionChangedAction.Reset, oldItems.ToList());
                    });
                }

                foreach (var item in enumerable)
                {
                    _stack.Push(item);
                }
            }
            finally
            {
                OnCollectionChanged(NotifyCollectionChangedAction.Reset, new List<T>(enumerable), _stack.Count - 1);
                if (Locker.IsWriteLockHeld)
                {
                    Locker.ExitWriteLock();
                }
            }
        }

        public void TrimExcess()
        {
            try
            {
                Locker.EnterWriteLock();
                _stack.TrimExcess();
            }
            finally
            {
                if (Locker.IsWriteLockHeld)
                {
                    Locker.ExitWriteLock();
                }
            }
            OnPropertyChanged(nameof(Count));
        }

        /// <summary>
        /// Populates a <see cref="T:System.Runtime.Serialization.SerializationInfo"/> with the data needed to serialize the target object.
        /// </summary>
        /// <param name="info">The <see cref="T:System.Runtime.Serialization.SerializationInfo"/> to populate with data. </param><param name="context">The destination (see <see cref="T:System.Runtime.Serialization.StreamingContext"/>) for this serialization. </param><exception cref="T:System.Security.SecurityException">The caller does not have the required permission. </exception>
        [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            var list = this.ToList();
            list.Reverse();
            info.AddValue("Items", list);
            info.AddValue("DoNotTrackChanges", _doNotTrackChanges);
            info.AddValue("BlockCollectionChanged", BlockCollectionChanged);
        }

        protected TrackableStack(SerializationInfo info, StreamingContext context)
        {
            _doNotTrackChanges = info.GetBoolean("DoNotTrackChanges");
            BlockCollectionChanged = info.GetBoolean("BlockCollectionChanged");

            var items = info.GetValue("Items", typeof(List<T>)) as List<T>;
            PushRange(items);
        }

        public IEnumerator<T> GetEnumerator()
        {
            // remove usage of safeenumerator for concurrency and speed issues - Brent (04 Apr 2016)
            // do a quick tolist within a write lock and then return the enumerator on that copied list
            List<T> list = new List<T>();
            try
            {
                if (!Locker.IsWriteLockHeld)
                    Locker.EnterWriteLock();
                list = _stack.ToList();
            }
            finally
            {
                if (Locker.IsWriteLockHeld)
                    Locker.ExitWriteLock();
            }
            return list.GetEnumerator();
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

        #region Implementation of ICollection

        /// <summary>
        /// Copies the elements of the <see cref="T:System.Collections.ICollection"/> to an <see cref="T:System.Array"/>, starting at a particular <see cref="T:System.Array"/> index.
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="T:System.Array"/> that is the destination of the elements copied from <see cref="T:System.Collections.ICollection"/>. The <see cref="T:System.Array"/> must have zero-based indexing. </param><param name="index">The zero-based index in <paramref name="array"/> at which copying begins. </param><exception cref="T:System.ArgumentNullException"><paramref name="array"/> is null. </exception><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is less than zero. </exception><exception cref="T:System.ArgumentException"><paramref name="array"/> is multidimensional.-or- The number of elements in the source <see cref="T:System.Collections.ICollection"/> is greater than the available space from <paramref name="index"/> to the end of the destination <paramref name="array"/>.-or-The type of the source <see cref="T:System.Collections.ICollection"/> cannot be cast automatically to the type of the destination <paramref name="array"/>.</exception>
        public void CopyTo(Array array, int index)
        {
            try
            {
                Locker.EnterUpgradeableReadLock();
                _stack.CopyTo(array as T[], index);
            }
            finally
            {
                if (Locker.IsUpgradeableReadLockHeld)
                    Locker.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Gets the number of elements contained in the <see cref="T:System.Collections.ICollection"/>.
        /// </summary>
        /// <returns>
        /// The number of elements contained in the <see cref="T:System.Collections.ICollection"/>.
        /// </returns>
        public int Count => _stack.Count;

        /// <summary>
        /// Gets an object that can be used to synchronize access to the <see cref="T:System.Collections.ICollection"/>.
        /// </summary>
        /// <returns>
        /// An object that can be used to synchronize access to the <see cref="T:System.Collections.ICollection"/>.
        /// </returns>
        public object SyncRoot => _locker;

        /// <summary>
        /// Gets a value indicating whether access to the <see cref="T:System.Collections.ICollection"/> is synchronized (thread safe).
        /// </summary>
        /// <returns>
        /// true if access to the <see cref="T:System.Collections.ICollection"/> is synchronized (thread safe); otherwise, false.
        /// </returns>
        public bool IsSynchronized => true;

        #endregion
    }
}