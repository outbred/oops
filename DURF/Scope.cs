using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Text;

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
            // if this isn't a 'sub' scope, then save it off to add to appropriate stack
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
}
