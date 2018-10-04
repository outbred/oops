using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using oops.Collections;
using oops.Interfaces;

namespace oops 
{
    /// <summary>
    /// ViewModel base class that raise prop changed using convenient Get/Set methods and tracks property changes
    /// </summary>
    [Serializable]
    public abstract class TrackableViewModel : INotifyPropertyChanged, IViewStateAware
    {
        private Dictionary<string, object> _propMap = null;

        /// <summary>
        /// Flag to turn on change tracking for this ViewModel
        /// </summary>
        protected virtual bool TrackChanges { get; } = true;

        protected virtual T Get<T>(T defaultValue, [CallerMemberName] string name = null)
        {
            if (!PropertyMap.ContainsKey(name))
                return defaultValue;

            var result = PropertyMap[name];
            if (result == null)
                return default(T);
            return (T)result;
        }

        protected virtual T Get<T>([CallerMemberName] string propName = null)
        {
            return this.Get<T>(default(T), propName);
        }


        /// <summary>
        /// Stores or updates the value in a dictionary and optionally raises the prop changed event if the value changes
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="name"></param>
        /// <param name="raisePropChanged"></param>
        /// <returns>True if property was changed (value was different).  False if not</returns>
        protected virtual bool Set<T>(T value, [CallerMemberName] string name = null, bool raisePropChanged = true)
        {
            T currentValue = default(T);
            lock (PropertyMap)
            {
                if (PropertyMap.ContainsKey(name))
                {
                    currentValue = (T)PropertyMap[name];
                    if (currentValue == null && value == null)
                        return false;

                    if (currentValue != null && currentValue.Equals(value))
                        return false;

                    if (OnPropertyChanging(name, currentValue, raisePropChanged, value))
                        PropertyMap[name] = value;
                    else return false;
                }
                else if (OnPropertyChanging(name, currentValue, raisePropChanged, value))
                    PropertyMap.Add(name, value);
                else return false;
            }



            if (raisePropChanged)
                RaisePropertyChanged(name);

            return true;
        }

        protected virtual Dictionary<string, object> PropertyMap => _propMap ?? (_propMap = new Dictionary<string, object>());


        /// <inheritdoc />
        [field: NonSerialized]
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        /// <summary>
        /// Called right before the PropertyChanged event is fired. Return false to block the change
        /// </summary>
        /// <param name="name"></param>
        /// <param name="currentValue">value before the change</param>
        /// <param name="raiseEvent">whether or not the PropertyChanged event will actually be raised</param>
        protected virtual bool OnPropertyChanging(string name, object currentValue, bool raiseEvent, object newValue)
        {
            if (TrackChanges && Globals.ScopeEachChange && Accumulator == null)
                SingleItemAccumulator.Track($"{name} changed to {newValue}", () => Set(currentValue, name, raiseEvent));
            else if (TrackChanges)
                Accumulator?.AddUndo(() => Set(currentValue, name, raiseEvent));

            return true;
        }

        private Accumulator _overridden = null;

        /// <summary>
        /// If this object needs to be locally scoped, set the Accumulator here.
        /// Otherwise, all changes go into the global Accumulator.Current
        /// </summary>
        public virtual Accumulator Accumulator
        {
            get => _overridden ?? Accumulator.Current;
            set => _overridden = value;
        }

        #region Implementation of IViewStateAware

        /// <inheritdoc />
        void IViewStateAware.Loaded()
        {
            OnLoaded();
        }

        /// <inheritdoc />
        void IViewStateAware.Unloaded()
        {
            OnUnloaded();   
        }

        protected virtual void OnLoaded() { }
        protected virtual void OnUnloaded() { }

        #endregion
    }
}