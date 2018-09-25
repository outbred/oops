using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using DURF.Interfaces;

namespace DURF.Collections
{
    /// <summary>
    /// Thread-safe list.  No change tracking.  Also has efficient Queue and Stack extensions
    /// </summary>
    /// <typeparam name="TType"></typeparam>
    public class ConcurrentList<TType> : IConcurrentList<TType>
    {
        private List<TType> _internal;

        [DebuggerStepThrough]
        public ConcurrentList()
        {
            _internal = new List<TType>();
        }

        [DebuggerStepThrough]
        public ConcurrentList(IEnumerable<TType> source)
        {
            _internal = source.ToList();
        }

        /// <summary>
        /// Returns a new list with a ref to all items in this collection, in the same order
        /// </summary>
        /// <returns></returns>
        public IList<TType> GetCopy()
        {
            lock(_syncRoot)
                return _internal.ToList();
        }

        /// <summary>
        /// Make custom ToList so it is THREAD SAFE.  Normal extension method on IEnumerable is not.
        /// Be sure you call THIS ONE not the default extension method.
        /// </summary>
        /// <returns></returns>
        public IList<TType> ToList()
        {
            return GetCopy();
        }

        /// <summary>
        /// Returns an enumerator that iterates through a COPY of the collection
        /// </summary>
        public IEnumerator<TType> GetEnumerator()
        {
            return GetCopy().GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through a COPY of the collection
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

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
                lock(_syncRoot)
                    return _internal[index];
            }
            set
            {
                lock(_syncRoot)
                    _internal[index] = value;
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
                lock(_syncRoot)
                    return _internal[index];
            }
            set
            {
                lock(_syncRoot)
                    _internal[index] = (TType)value;
            }
        }

        public void Add(TType item)
        {
            lock(_syncRoot)
                _internal.Add(item);
        }

        /// <summary>
        /// Similiar to Add, but only does the add if not already in the list.  Basically, it lets you use a
        /// thread safe list like a hashset so it will only have a unique set of entities.  This assumes you only
        /// call AddIfNew of course
        /// </summary>
        /// <param name="item">Item we may need to add</param>
        /// <returns>True if item added.  False if it was already in the list.</returns>
        public bool AddIfNew(TType item)
        {
            lock(_syncRoot)
            {
                if (_internal.Contains(item))
                    return false;
                _internal.Add(item);
                return true;
            }
        }

        /// <summary>
        /// Same as Add except returns the index and locks around the checking of size and adding so it is safe.
        /// </summary>
        /// <returns>Index of the newly added item</returns>
        public int AddAndGetIndex(TType item)
        {
            lock(_syncRoot)
            {
                var idx = _internal.Count;
                _internal.Add(item);
                return idx;
            }
        }

        int IList.Add(object value)
        {
            lock(_syncRoot)
            {
                Add((TType) value);
                return Count;
            }
        }

        bool IList.Contains(object value)
        {
            return Contains((TType) value);
        }

        public void Clear()
        {
            lock(_syncRoot)
                _internal.Clear();
        }

        int IList.IndexOf(object value)
        {
            return IndexOf((TType) value);
        }

        void IList.Insert(int index, object value)
        {
            Insert(index, (TType) value);
        }

        void IList.Remove(object value)
        {
            Remove((TType)value);
        }

        public void ReplaceWith(IEnumerable<TType> items)
        {
            lock(_syncRoot)
                _internal = items.ToList();
        }

        public bool Contains(TType item)
        {
            lock(_syncRoot)
                return _internal.Contains(item);
        }

        public void CopyTo(TType[] array, int arrayIndex)
        {
            lock(_syncRoot)
                _internal.CopyTo(array, arrayIndex);
        }

        public bool Remove(TType item)
        {
            lock(_syncRoot)
                return _internal.Remove(item);
        }

        /// <summary>
        /// Remove a list of items from the internal list under a single lock.
        /// </summary>
        /// <returns>Number removed.</returns>
        public int Remove(IEnumerable<TType> items)
        {
            lock(_syncRoot)
                return items.Count(item => _internal.Remove(item));
        }

        /// <summary>
        /// Replace all instances of existing with new in the list (inside a lock)
        /// </summary>
        /// <returns>The number of elements replaced.</returns>
        public int ReplaceAll(TType existingItem, TType newItem)
        {
            lock(_syncRoot)
            {
                var total = 0;
                for (var index = 0; index < _internal.Count; index++)
                {
                    var item = _internal[index];
                    if ((item == null && existingItem == null) || item?.Equals(existingItem) == true)
                    {
                        _internal[index] = newItem;
                        total++;
                    }
                }
                return total;
            }
        }

        public int RemoveAndGetIndex(TType item)
        {
            lock(_syncRoot)
            {
                var idx = _internal.IndexOf(item);
                if (idx < 0)
                    return idx;
                _internal.RemoveAt(idx);
                return idx;
            }
        }

        void ICollection.CopyTo(Array array, int index)
        {
            lock(_syncRoot)
            {
                if (_internal.Any())
                    _internal.CopyTo((TType[])array, index);
            }
        }

        public int Count
        {
            get
            {
                lock(_syncRoot)
                    return _internal.Count;
            }
        }

        private object _syncRoot = new object();
        object ICollection.SyncRoot => _syncRoot;

        bool ICollection.IsSynchronized => true;

        public bool IsReadOnly => false;

        bool IList.IsFixedSize { get; } = false;

        public int IndexOf(TType item)
        {
            lock(_syncRoot)
                return _internal.IndexOf(item);
        }

        /// <summary>
        /// Find in backwards order for performance
        /// Static so we can use in other classes
        /// </summary>
        public static int ReverseIndexOf(IList<TType> list, TType item)
        {
            for (var index = list.Count - 1; index > -1; --index)
                if (list[index]?.Equals(item) ?? false)
                    return index;

            return -1;
        }

        public int ReverseIndexOf(TType item)
        {
            lock(_syncRoot)
                return ReverseIndexOf(_internal, item);
        }

        public void Insert(int index, TType item)
        {
            lock(_syncRoot)
            {
                if (index == 0 || index <= _internal.Count)
                    _internal.Insert(index, item);
                else
                    _internal.Add(item);
            }
        }

        public void RemoveAt(int index)
        {
            lock(_syncRoot)
                _internal.RemoveAt(index);
        }

        public void AddRange(IEnumerable<TType> source)
        {
            lock(_syncRoot)
                _internal.AddRange(source);
        }

        public void RemoveAllInstances(TType item)
        {
            lock(_syncRoot)
                while (_internal.Remove(item)) { }
        }

        /// <summary>
        /// Safely transfer all elements from a list to this one.
        /// </summary>
        /// <param name="source">Source list that will be transferred (and emptied)</param>
        public void TransferElements(IList<TType> source)
        {
            lock(_syncRoot)
            {
                var sourceList = source;
                _internal.AddRange(sourceList);
                sourceList.Clear();
            }
        }

        /// <summary>
        /// Pull out all elements of the list.  Done efficiently so no copying is
        /// needed - just transfer the internal list and reset it with a new one.
        /// </summary>
        public List<TType> ExtractElements()
        {
            lock(_syncRoot)
            {
                var tempList = _internal;
                _internal = new List<TType>();
                return tempList;
            }
        }

        /// <summary>
        /// Same as extension method, but thread safe.
        /// </summary>
        public TType FirstOrDefault()
        {
            lock(_syncRoot)
                return (_internal.Count < 1) ? default(TType) : _internal[0];
        }

        /// <summary>
        /// Same as extension method, but thread safe.
        /// </summary>
        public TType LastOrDefault()
        {
            lock (_syncRoot)
                return (_internal.Count < 1) ? default(TType) : _internal[Count - 1];
        }

        /// <summary>
        /// Same as extension method, but thread safe, and efficient since it doesn't get an enumerator
        /// </summary>
        public bool Any()
        {
            lock(_syncRoot)
                return _internal.Count > 0;
        }

        /// <summary>
        /// Same as extension method, but thread safe, and efficient since it doesn't get an enumerator
        /// </summary>
        /// <param name="checker"></param>
        /// <returns></returns>
        public bool Any(Func<TType, bool> checker)
        {
            lock(_syncRoot)
                return _internal.Any(checker);
        }

        public TType First()
        {
            lock (_syncRoot)
                return _internal[0];
        }

        public TType Last()
        {
            lock (_syncRoot)
                return _internal[Count - 1];
        }

        /// <summary>
        /// Swap if needed so that the given item ends up at the desired index.  
        /// Performed in a single function so we can maintain the lock on the internal list.
        /// </summary>
        /// <returns>True if item ends up at the requested index.  False if not.</returns>
        public bool Move(TType item, int desiredIndex)
        {
            lock(_syncRoot)
            {
                if (_internal.Count < 1)
                    return false; 
                if (_internal.Count == 1)
                    return desiredIndex == 0 && Equals(item, _internal[0]);

                var curIdx = _internal.IndexOf(item);
                if (curIdx < 0)
                    return false;
                if (desiredIndex >= 0 && desiredIndex < _internal.Count)
                {
                    if (curIdx == desiredIndex)
                        return true;

                    _internal[curIdx] = _internal[desiredIndex];
                    _internal[desiredIndex] = item;
                }
                else
                    return false;

                return true;
            }
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
            lock (_syncRoot)
            {
                if (_internal.Count == 0)
                    throw new IndexOutOfRangeException($"Queue is empty!");

                var item = _internal[0];
                _internal.RemoveAt(0);
                return item;
            }
        }

        /// <inheritdoc />
        bool IQueue<TType>.TryDequeue(out TType item)
        {
            item = default(TType);
            lock (_syncRoot)
            {
                if (_internal.Count == 0)
                    return false;

                item = _internal[0];
                _internal.RemoveAt(0);
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
            lock (_syncRoot)
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
            lock (_syncRoot)
            {
                if (_internal.Count == 0)
                    throw new IndexOutOfRangeException($"Stack is empty!");

                var item = _internal[0];
                _internal.RemoveAt(0);
                return item;
            }
        }

        /// <inheritdoc />
        bool IStack<TType>.TryPop(out TType item)
        {
            item = default(TType);
            lock (_syncRoot)
            {
                if (_internal.Count == 0)
                    return false;

                item = _internal[0];
                _internal.RemoveAt(0);
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
            lock(_syncRoot)
                return this.ToList();
        }

        #endregion
    }
}
