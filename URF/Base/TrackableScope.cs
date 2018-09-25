//#define NO_CHANGES_TRACKED

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace URF.Base
{
    /// <summary>
    /// To be used to track changes within a certain scope.
    /// 
    /// use like this:
    /// var scope = TrackableScope.CurrentOrNew("Add construction command");
    /// ...do your work here; all work underneath will track changes in this scope (or the current one if this is a nested execution flow)
    /// scope.EndChangeTracking() // scope is now locked and TrackableScope.Current will return null until TrackableScope.CurrentOrNew() is called again
    /// scope.UndoAllChanges() will undo all the tracked changes
    /// </summary>
    [Serializable]
    public class TrackableScope
    {
        private static TrackableScope _current = null;
        private IQueue<Change> _changes = new ConcurrentList<Change>();
        private readonly string _scopeName;
        private static readonly object _currentLocker = new object();

        public static TrackableScope Current => _current;

        /// <summary>
        /// Factory method for working with undoable scopes.  If a scope is currently in use, will return that one.  
        /// If there is not one currently in use, creates a new one with the scopeName and sets that to the one currently in use.
        /// 
        /// call scope.EndChangeTracking() to remove a scope as the current one.
        /// 
        /// using this methodology allows nesting scope behavior w/o actually nesting (no need to actually nest scopes; just combine them into one instead)
        ///  - this is useful when a Command use one or more other Commands - they're all on the parent scope
        /// </summary>
        /// <param name="scopeName"></param>
        /// <returns></returns>
        public static TrackableScope CurrentOrNew(string scopeName = "Track Changes")
        {
            lock (_currentLocker)
            {
                if (_current != null)
                    return _current;

                _current = new TrackableScope(scopeName);
                return _current;
            }
        }

        private TrackableScope(string scopeName)
        {
            _scopeName = scopeName;
        }

        /// <summary>
        /// Tracks changes to a instance, allowing the instance to react to undo events via an Action
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="instance"></param>
        /// <param name="oldValue">strong reference to old value is stored when Func is called</param>
        /// <param name="onUndo">Action to be performed when the scope is disposed (undo occurs). Thread marshaling within the Func<> is the caller's responsibility.</param>
        /// <returns>True if tracking is allowed within this TrackableScope for the propname/instace, 
        /// false if this scope is not the current scope (locked) or a duplicate value for an existing prop name/instance is received</returns>
        /// <exception cref="ArgumentNullException">If the property name is empty, instance is null, or onUndo is null</exception>
        public void TrackChange(string propertyName, object instance, Func<object> oldValue, Action<object> onUndo)
        {
            // do not allow changes to be tracked in the middle of an undo...well, maybe we do in case of redoing an undo
            if ( !ReferenceEquals(_current, this) || _changes == null)
            {
                //Log.WriteLine($"Disallowing change to be tracked for property {propertyName}");
                return;
            }

            if (onUndo == null || string.IsNullOrWhiteSpace(propertyName) || instance == null || oldValue == null)
            {
#if DEBUG
                if (Debugger.IsAttached)
                    Debugger.Break();
#endif
                throw new ArgumentNullException("onUndo, propertyname, instance, oldValue", "Must be a valid property name and have an undoable action associated to an instance.");
            }

            var old = oldValue();
            var change = new Change() { PropertyName = propertyName, Instance = instance, OldValue = old, OnUndo = onUndo };

            _changes.Enqueue(change);
        }

        /// <summary>
        /// Ends change tracking for the current scope.
        /// </summary>
        /// <param name="scopeName">Ensure that changes are no longer tracked iff current is the right scope, and not a nested one</param>
        public void EndChangeTracking(string scopeName)
        {
            if (ReferenceEquals(_current, this) && _current._scopeName == scopeName)
            {
                Debug.WriteLine($"Completed tracking changes for '{scopeName}' with {(this._changes?.Count ?? 0)} changes tracked.");
                _current = null;
            }
        }

        /// <summary>
        /// Gives back a list of all instance that were tracked during this scope
        /// 
        /// The idea is to mark them dirty before an undo, or for diagnostic purposes
        /// </summary>
        public IEnumerable<object> InstancesTracked
        {
            get { return _changes.Select(c => c.Instance).Distinct(); }
        }

        public IList<Change> TrackedChanges => _changes;
        public string Name => _scopeName;

        /// <summary>
        /// Undoes all the changes tracked in reverse order
        /// </summary>
        /// <returns></returns>
        public async Task UndoAllChanges(string scopeName)
        {
            // limit this to the scope that is calling; i.e. a scope can only undo its own changes (not another scope's changes)
            if (_scopeName != scopeName)
                return;

            EndChangeTracking(scopeName);

            if (_changes == null)
                return;

            await Task.Run(() =>
            {
                lock (this)
                {
                    if (_changes == null)
                        return;
                    Debug.WriteLine($"Beginning undo for scope named '{_scopeName}'");
                    foreach (var change in _changes.Reverse()) // this pops in order, from top of stack on down
                    {
                        Debug.WriteLine($"Undoing change for property '{change.PropertyName}' in instance of type '{change.Instance.GetType().Name}' to old value '{change.OldValue}'");
                        change.OnUndo(change.OldValue);
                    }
                    Debug.WriteLine($"Completed undo for scope named '{_scopeName}'");
                    _changes = null;
                }
            });
        }
    }


    public class Change
    {
        public string PropertyName { get; set; }
        public object Instance { get; set; } 

        public object OldValue { get; set; }

        public Action<object> OnUndo { get; set; }
    }
}