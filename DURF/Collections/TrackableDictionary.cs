using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Threading;
using DURF.Interfaces;

namespace DURF.Collections
{
    public class TrackableDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IDictionary, IReadOnlyDictionary<TKey, TValue>, ISerializable, INotifyCollectionChanged, INotifyPropertyChanged
    {
        private Dictionary<TKey, TValue> _internal;
        private bool _shutOffCollectionChangedEvents = false;
        private bool _changeDetectedWhileNotificationIsShutOff;
        private ConcurrentList<NotifyCollectionChangedEventArgs> _pendingNotifications = new ConcurrentList<NotifyCollectionChangedEventArgs>();

        #region Properties
        private object SyncRoot => ((ICollection) _internal).SyncRoot;

        /// <inheritdoc />
        public bool IsSynchronized { get; } = true;

        public bool TrackChanges { get; set; }

        /// <summary>
        /// False by default b/c of the expensive context switching
        /// </summary>
        public bool UseDispatcherForCollectionChanged { get; set; } = false;

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

        #endregion

        #region Ctor

        public TrackableDictionary()
        {
            _internal = new Dictionary<TKey, TValue>();
        }

        public TrackableDictionary(int capacity)
        {
            _internal = new Dictionary<TKey, TValue>(capacity);
        }

        public TrackableDictionary(IEqualityComparer<TKey> comparer)
        {
            _internal = new Dictionary<TKey, TValue>(comparer);
        }

        public TrackableDictionary(IDictionary<TKey, TValue> dictionary)
        {
            _internal = new Dictionary<TKey, TValue>(dictionary);
        }

        public TrackableDictionary(int capacity, IEqualityComparer<TKey> comparer)
        {
            _internal = new Dictionary<TKey, TValue>(capacity, comparer);
        }

        public TrackableDictionary(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer)
        {
            _internal = new Dictionary<TKey, TValue>(dictionary, comparer);
        }

        protected TrackableDictionary(SerializationInfo info, StreamingContext context)
        {
            _internal = (Dictionary<TKey, TValue>) info.GetValue("Internal", typeof(Dictionary<TKey, TValue>));
        }

        #endregion

        #region Extra methods

        /// <summary>
        /// Replace entire dictionary with another.  Use this because it is done behind the lock.
        /// </summary>
        public void Replace(IDictionary<TKey, TValue> source)
        {
            PushToUiThreadSync(() =>
            {
                lock (SyncRoot)
                {
                    var items = _internal.ToDictionary(p => p.Key, p => p.Value);
                    _internal = new Dictionary<TKey, TValue>(source);
                    if (TrackChanges)
                        Accumulator.Current?.AddUndo(() => this.Replace(items));
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                }
            });
        }

        /// <summary>
        /// Safely adds a key/value to the dictionary within a lock. If key is already present, does nothing.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool SafeAdd(TKey key, TValue value)
        {
            if (key == null)
                return false;
            var result = false;

            PushToUiThreadSync(() =>
            {
                lock (SyncRoot)
                {
                    if (!_internal.ContainsKey(key))
                    {
                        var item = new KeyValuePair<TKey, TValue>(key, value);
                        _internal.Add(key, value);
                        if (TrackChanges)
                            Accumulator.Current?.AddUndo(() => Remove(key));

                        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
                        result = true;
                    }
                }
            });

            return result;
        }

        /// <summary>
        /// Safely removes a key from the dictionary within a lock. If not present, early out and return false.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool SafeRemove(TKey key)
        {
            if (key == null)
                return false;

            var result = false;
            PushToUiThreadSync(() =>
            {
                lock (SyncRoot)
                {
                    if (_internal.ContainsKey(key))
                    {
                        var item = _internal[key];
                        if (TrackChanges)
                            Accumulator.Current?.AddUndo(() => Add(key, item));

                        result = _internal.Remove(key);
                        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item));
                    }
                }
            });

            return result;
        }

        /// <summary>
        /// Convenience method to trygetvalue and remove under a single lock.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGetValueAndRemoveKey(TKey key, out TValue value)
        {
            value = default(TValue);
            var result = false;
            PushToUiThreadSync(() =>
            {
                lock (SyncRoot)
                {
                    if (_internal.TryGetValue(key, out var v))
                    {
                        var item = v;
                        if (TrackChanges)
                            Accumulator.Current?.AddUndo(() => Add(key, item));

                        result = _internal.Remove(key);
                        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, v));
                    }
                }
            });

            return result;
        }

        #endregion

        #region IDictionary<TKey, TValue> implementation

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            lock (SyncRoot)
                return new Dictionary<TKey, TValue>(_internal).GetEnumerator();
        }

        /// <inheritdoc />
        public void Remove(object key)
        {
            Remove((TKey) key);
        }

        /// <inheritdoc />
        public object this[object key]
        {
            get => this[(TKey) key];
            set => this[(TKey) key] = ((TValue) value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            PushToUiThreadSync(() =>
            {
                lock (SyncRoot)
                {
                    _internal.Add(item.Key, item.Value);
                    if (TrackChanges)
                        Accumulator.Current?.AddUndo(() => Remove(item.Key));

                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
                }
            });
        }

        /// <inheritdoc />
        public bool Contains(object key)
        {
            return Contains((TKey) key);
        }

        /// <inheritdoc />
        public void Add(object key, object value)
        {
            Add((TKey) key, (TValue) value);
        }

        public void AddRange(IEnumerable<KeyValuePair<TKey, TValue>> pairs)
        {
            PushToUiThreadSync(() =>
            {
                lock (SyncRoot)
                {
                    if (TrackChanges)
                        Accumulator.Current?.AddUndo
                        (() =>
                        {
                            foreach (var pair in pairs)
                                _internal.Remove(pair.Key);

                            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                        });

                    foreach (var pair in pairs)
                        if (!ContainsKey(pair.Key))
                            _internal.Add(pair.Key, pair.Value);

                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                }
            });
        }

        public void Clear()
        {
            PushToUiThreadSync(() =>
            {
                lock (SyncRoot)
                {
                    var pairs = this._internal.ToDictionary(p => p.Key, p => p.Value);
                    _internal.Clear();
                    if (TrackChanges)
                        Accumulator.Current?.AddUndo(() => AddRange(pairs));

                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                }
            });
        }

        /// <inheritdoc />
        IDictionaryEnumerator IDictionary.GetEnumerator()
        {
            return (IDictionaryEnumerator) GetEnumerator();
        }


        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            lock (SyncRoot)
                return _internal.Contains(item);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            lock (SyncRoot)
                ((IDictionary) _internal).CopyTo(array, arrayIndex);
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            var result = false;
            PushToUiThreadSync(() =>
            {
                lock (SyncRoot)
                {
                    TValue value;
                    if (_internal.TryGetValue(item.Key, out value))
                    {
                        if (TrackChanges)
                            Accumulator.Current?.AddUndo(() => Add(item.Key, value));
                        result = _internal.Remove(item.Key);
                        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item));
                    }
                }
            });
            return result;
        }

        /// <inheritdoc />
        public void CopyTo(Array array, int index)
        {
            lock (SyncRoot)
                ((IDictionary) _internal).CopyTo(array, index);
        }

        public int Count
        {
            get
            {
                lock (SyncRoot)
                    return _internal.Count;
            }
        }

        /// <inheritdoc />
        object ICollection.SyncRoot => SyncRoot;

        /// <inheritdoc />
        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Keys;

        /// <inheritdoc />
        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Values;

        /// <inheritdoc />
        ICollection IDictionary.Values => (ICollection)Values;

        public bool IsReadOnly => false;

        /// <inheritdoc />
        public bool IsFixedSize { get; }

        public bool ContainsKey(TKey key)
        {
            lock (SyncRoot)
                return _internal.ContainsKey(key);
        }

        public void Add(TKey key, TValue value)
        {
            PushToUiThreadSync(() =>
            {
                lock (SyncRoot)
                {
                    var item = new KeyValuePair<TKey, TValue>(key, value);
                    _internal.Add(key, value);
                    if (TrackChanges)
                        Accumulator.Current?.AddUndo(() => Remove(key));

                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
                }
            });
        }

        public bool Remove(TKey key)
        {
            var result = false;
            PushToUiThreadSync(() =>
            {
                lock (SyncRoot)
                {
                    TValue value;
                    if (_internal.TryGetValue(key, out value))
                    {
                        if (TrackChanges)
                            Accumulator.Current?.AddUndo(() => Add(key, value));
                        result = _internal.Remove(key);
                        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, new KeyValuePair<TKey, TValue>(key, value)));
                    }
                }
            });
            return result;
        }


        public bool TryGetValue(TKey key, out TValue value)
        {
            lock (SyncRoot)
                return _internal.TryGetValue(key, out value);
        }

        public TValue this[TKey key]
        {
            get
            {
                lock (SyncRoot)
                    return _internal[key];
            }
            set
            {
                PushToUiThreadSync(() =>
                {
                    lock (SyncRoot)
                    {
                        TValue oldValue;
                        var exist = TryGetValue(key, out oldValue);
                        var oldItem = new KeyValuePair<TKey, TValue>(key, oldValue);
                        _internal[key] = value;
                        var newItem = new KeyValuePair<TKey, TValue>(key, value);
                        if (TrackChanges)
                            Accumulator.Current?.AddUndo(() => this[key] = oldValue);

                        if (exist)
                            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, newItem, oldItem));
                        else
                            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, newItem));
                    }
                });
            }
        }

        public ICollection<TKey> Keys
        {
            get
            {
                lock (SyncRoot)
                    return _internal.Keys.ToList();
            }
        }

        /// <inheritdoc />
        ICollection IDictionary.Keys => (ICollection) Keys;

        public ICollection<TValue> Values
        {
            get
            {
                lock (SyncRoot)
                    return _internal.Values.ToList();
            }
        }

        #endregion

        #region Implementation of ISerializable

        /// <inheritdoc />
        [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Internal", _internal);
        }

        #endregion

        /// <inheritdoc />
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;

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

            if (CollectionChanged != null)
            {
                try
                {
                    if (_pendingNotifications.Any())
                    {
                        e = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
                        _pendingNotifications.Clear();
                    }

                    switch (e.Action)
                    {
                        case NotifyCollectionChangedAction.Add:
                        case NotifyCollectionChangedAction.Remove:
                        case NotifyCollectionChangedAction.Reset:
                            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
                            break;
                    }
                    CollectionChanged?.Invoke(this, e);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(@"Unable to fire collection changed event cleanly.", ex);
                }
            }
        }

        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
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
                    catch (Exception ex) { Debug.WriteLine(@"Unable to finish changing collection", ex); }
                    finally { allDone.Set(); }
                });
                allDone.WaitOne();
            }
        }
    }
}