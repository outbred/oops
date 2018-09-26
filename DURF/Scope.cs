using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DURF.Collections;
using DURF.Interfaces;

namespace DURF
{
    public enum ScopeState
    {
        Do, Undo, Redo
    }

    [DebuggerDisplay("{_scope.Name}")]
    public class Scope : IDisposable
    {
        private TrackableScope _scope = null;
        private ScopeState _state;
        public Scope(string name, ScopeState state = ScopeState.Do)
        {
            _state = state;
            // if this isn't a 'sub' scope, then save it off to add to undo stack
            if(TrackableScope.CurrentOrNew(out var scope, name))
                _scope = scope;
        }


        /// <inheritdoc />
        public void Dispose() 
        {
            if (_scope != null)
            {
                switch (_state)
                {
                    case ScopeState.Do:
                        ScopeManager.Instance.Redoables.Clear();
                        ScopeManager.Instance.Undoables.Push(_scope);
                        break;
                    case ScopeState.Undo:
                        ScopeManager.Instance.Undoables.Push(_scope);
                        break;
                    case ScopeState.Redo:
                        ScopeManager.Instance.Redoables.Push(_scope);
                        break;
                }
                TrackableScope.Current.EndChangeTracking(_scope.Name);
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

        public async Task UndoLast()
        {
            if (Undoables.TryPop(out var scope))
            {
                using (new Scope(scope.Name, ScopeState.Redo))
                    await scope.UndoAllChanges(scope.Name);
            }
        }

        public async Task RedoLast()
        {
            if (Redoables.TryPop(out var scope))
            {
                using (new Scope(scope.Name, ScopeState.Undo))
                    await scope.UndoAllChanges(scope.Name);
            }
        }

    }
}
