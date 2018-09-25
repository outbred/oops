using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DURF.Collections;
using DURF.Interfaces;

namespace DURF 
{
    /// <summary>
    /// ViewModel base class that raise prop changed using convenient Get/Set methods and tracks property changes
    /// </summary>
    public abstract class TrackableViewModel : INotifyPropertyChanged, IViewStateAware
    {
        #region Fields
        private Dictionary<string, object> _propMap = null;
        #endregion

        protected virtual bool TrackChanges { get; } = true;

        protected T Get<T>(T defaultValue, [CallerMemberName] string name = null)
        {
            if (!PropertyMap.ContainsKey(name))
                return defaultValue;

            var result = PropertyMap[name];
            if (result == null)
                return default(T);
            return (T)result;
        }

        protected T Get<T>([CallerMemberName] string propName = null)
        {
            return this.Get<T>(default(T), propName);
        }


        /// <summary>
        /// Stores or updates the value in a dictionary and optionally raises the prop changed event if the value changes
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="propName"></param>
        /// <param name="nonSerialized"></param>
        /// <param name="raisePropChanged"></param>
        /// <returns></returns>
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

                    PropertyMap[name] = value;
                }
                else
                    PropertyMap.Add(name, value);
            }

            OnPropertyChanging(name, currentValue, raisePropChanged);

            if (raisePropChanged)
                RaisePropertyChanged(name);

            return true;
        }

        private Dictionary<string, object> PropertyMap => _propMap ?? (_propMap = new Dictionary<string, object>());


        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        /// <summary>
        /// Called right before the PropertyChanged event is fired
        /// </summary>
        /// <param name="name"></param>
        /// <param name="currentValue">value before the change</param>
        /// <param name="raiseEvent">whether or not the event will actually be raised</param>
        protected virtual void OnPropertyChanging(string name, object currentValue, bool raiseEvent)
        {
            if(TrackChanges)
                TrackableScope.Current?.TrackChange(() => Set(currentValue, name, raiseEvent));
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