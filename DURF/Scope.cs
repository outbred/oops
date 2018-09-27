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
        private readonly ScopeState _state;
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
                ScopeManager.Instance.Add(_scope, _state);
                TrackableScope.Current.EndChangeTracking(_scope.Name);
                _scope = null;
            }
        }
    }
}
