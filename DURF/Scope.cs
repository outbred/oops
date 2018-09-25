using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Threading;
using DURF.Collections;
using DURF.Interfaces;

namespace DURF
{
    public class Scope : IDisposable
    {
        private TrackableScope _scope = null;
        public Scope(string name)
        {
            // if this isn't a 'sub' scope, then save it off to add to undo stack
            if(TrackableScope.CurrentOrNew(out var scope, name))
                _scope = scope;
        }


        /// <inheritdoc />
        public void Dispose() 
        {
            if (_scope != null)
            {
                _scope.Name = $"Undo '{_scope.Name}";
                ScopeManager.Instance.Undoables.Add(_scope);
            }
        }
    }

    public class ScopeManager
    {
        public IStack<TrackableScope> Undoables { get; } = new TrackableCollection<TrackableScope>(){TrackChanges = false};
        public IStack<TrackableScope> Redoables { get; } = new TrackableCollection<TrackableScope>(){TrackChanges = false};

        private static ScopeManager _instance = null;
        public static ScopeManager Instance
        {
            get
            {
                Interlocked.CompareExchange(ref _instance, new ScopeManager(), null);
                return _instance;
            }
        }

    }
}
