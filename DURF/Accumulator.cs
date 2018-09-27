//#define NO_CHANGES_TRACKED

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DURF.Collections;
using DURF.Interfaces;

namespace DURF
{
    /// <summary>
    /// An accumulator of actions, each targeted at undoing some other action (user-generated or otherwise)
    /// 
    /// use like this:
    /// var scope = Accumulator.CurrentOrNew("Add new address");
    /// ...do your work here; all work underneath will track changes in this scope (or the current one if this is a nested execution flow)
    /// scope.Close() // scope is now locked and Accumulator.Current will return null until Accumulator.CurrentOrNew() is called again
    /// scope.UndoAll() will undo all the tracked changes
    /// </summary>
    [Serializable]
    public class Accumulator
    {
        private static Accumulator _current = null;
        private IStack<Record> _changes = new ConcurrentList<Record>();
        private readonly string _scopeName;
        private static readonly object _currentLocker = new object();

        public static Accumulator Current => _current;

        /// <summary>
        /// Factory method for working with undoable scopes.  If a scope is currently in use, will return that one.  
        /// If there is not one currently in use, creates a new one with the scopeName and sets that to the one currently in use.
        /// 
        /// call scope.Close() to remove a scope as the current one.
        /// 
        /// using this methodology allows nesting scope behavior w/o actually nesting (no need to actually nest scopes; just combine them into one instead)
        ///  - this is useful when a Command use one or more other Commands - they're all on the parent scope
        /// </summary>
        /// <param name="scopeName"></param>
        /// <returns>True if new scope</returns>
        public static bool CurrentOrNew(out Accumulator scope, string scopeName = "Track Changes")
        {
            lock (_currentLocker)
            {
                scope = _current;

                if (_current != null)
                    return false;

                _current = new Accumulator(scopeName);
                scope = _current;
                return true;
            }
        }

        private Accumulator(string scopeName)
        {
            _scopeName = scopeName;
        }

        /// <summary>
        /// Tracks changes to a instance, allowing the instance to react to undo events via an Action
        /// </summary>
        /// <param name="propertyName">(optional) PropertyName associated with change</param>
        /// <param name="instance">(optional) The object instance associated with the change</param>
        /// <param name="onUndo">Action to be performed when the scope is disposed (undo occurs). Thread marshaling within the Func<> is the caller's responsibility.</param>
        /// <returns>True if tracking is allowed within this Accumulator for the propname/instance, 
        /// false if this scope is not the current scope (locked) or a duplicate value for an existing prop name/instance is received</returns>
        /// <exception cref="ArgumentNullException">If the property name is empty, instance is null, or onUndo is null</exception>
        public void AddUndo(Action onUndo, string propertyName = null, object instance = null)
        {
            // do not allow changes to be tracked in the middle of an undo...well, maybe we do in case of redoing an undo
            if ( !ReferenceEquals(_current, this) || _changes == null)
            {
                //Log.WriteLine($"Disallowing change to be tracked for property {propertyName}");
                return;
            }

            if (onUndo == null)
            {
#if DEBUG
                if (Debugger.IsAttached)
                    Debugger.Break();
#endif
                throw new ArgumentNullException(nameof(onUndo), @"Must have an undoable action associated to an instance.");
            }

            var change = new Record() { PropertyName = propertyName, Instance = instance, OnUndo = onUndo };

            _changes.Push(change);
        }

        /// <summary>
        /// Ends change tracking for the current scope.
        /// </summary>
        /// <param name="scopeName">Ensure that changes are no longer tracked iff current is the right scope, and not a nested one</param>
        public void Close(string scopeName)
        {
            if (ReferenceEquals(_current, this) && _current._scopeName == scopeName)
            {
                Debug.WriteLine($"Completed tracking changes for '{scopeName}' with {(this._changes?.Count() ?? 0)} changes tracked.");
                _current = null;
            }
        }
        
        /// <summary>
        /// A list of all the undo's accumulated throughout the lifetime of the scope
        /// </summary>
        public IStack<Record> Records => _changes;
        
        /// <summary>
        /// Some user-friendly name for this accumulation of undo's
        /// </summary>
        public string Name => _scopeName;

        /// <summary>
        /// Undoes all the changes tracked in reverse order; name check in case of nested scope
        /// </summary>
        /// <returns></returns>
        public async Task UndoAll(string scopeName)
        {
            // limit this to the scope that is calling; i.e. a scope can only undo its own changes (not another scope's changes)
            if (_scopeName != scopeName)
                return;

            Close(scopeName);

            if (_changes == null)
                return;

            await Task.Run(() =>
            {
                lock (this)
                {
                    if (_changes == null)
                        return;
                    Debug.WriteLine($"Beginning undo for scope named '{_scopeName}'");
                    foreach (var change in _changes.GetEnumerable()) // in order, from top of 'stack' on down
                        change.OnUndo();

                    Debug.WriteLine($"Completed undo for scope named '{_scopeName}'");
                    _changes = null;
                }
            });
        }
    }


    public class Record
    {
        public string PropertyName { get; set; }

        public object Instance { get; set; } 

        public Action OnUndo { get; set; }
    }
}