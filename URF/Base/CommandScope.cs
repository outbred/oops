using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Commands.Base;
using UI.Models.Collections;

namespace URF.Base
{
    public class CommandScope
    {
        private ScopeCommand _command;
        private string _scopeName;
        private readonly ICommandStateManager _manager = IoC.Current.Container.GetInstance<ICommandStateManager>();

        public CommandScope(string scopeName)
        {
            _command = new ScopeCommand(scopeName);
            _manager.BeginExecution(_command);
        }

        [ImportingConstructor]
        public CommandScope() { }

        /// <summary>
        /// Set first to create the scope
        /// </summary>
        public string ScopeName
        {
            get { return _scopeName; }
            set
            {
                if (_command != null)
                    return;

                _scopeName = value;
                _command = new ScopeCommand(value);
                _manager.BeginExecution(_command);
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (_command.HasScope)
                _command.OnClick.ExecuteAsync(null);
            else
                _manager.ClearCurrentCommand(_command);
            _command = null;
        }
    }

    internal class ScopeCommand : CommandBase
    {
        private TrackableScope _scope = null;
        private readonly string _scopeName;

        public ScopeCommand(string scopeName)
        {
            _scopeName = scopeName;
            _scope = TrackableScope.CurrentOrNew(_scopeName);

            // If this command is used while another scope is under way, then don't use it for undo/redo
            if (_scope.Name != _scopeName)
                _scope = null;
        }

        public override string ToolTip
        {
            get { return _scopeName; }
            set { }
        }

        internal bool HasScope => _scope != null;

        protected override Task OnRedo
        {
            get
            {
                if (_scope == null)
                    return Task.Run(() => { });

                return Task.Run(async () =>
                {
                    _scope.EndChangeTracking($"Redo {_scopeName}");
                    var undoScope = TrackableScope.CurrentOrNew(_scopeName);
                    await _scope.UndoAllChanges($"Redo {_scopeName}");
                    undoScope.EndChangeTracking(_scopeName);
                    _scope = undoScope;
                });
            }
        }

        /// <summary>
        /// If used directly, the command will not be managed in the undo/redo stack
        /// 
        /// Should only be used by encapsulating commands
        /// </summary>
        /// <returns></returns>
        public override async Task UndoAction()
        {
            if (_scope == null)
                return;

            _scope.EndChangeTracking(_scopeName);
            var redoScope = TrackableScope.CurrentOrNew($"Redo {_scopeName}");
            await _scope.UndoAllChanges(_scopeName);
            redoScope.EndChangeTracking($"Redo {_scopeName}");
            _scope = redoScope;
        }

        /// <summary>
        /// If used directly, the command will NOT be managed in the undo/redo stack
        /// 
        /// Should only be used by encapsulating commands
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
#pragma warning disable 1998
        public override async Task ExecuteAction(object context)
        {
            _scope?.EndChangeTracking(_scopeName);
        }
#pragma warning restore 1998


        #region Overrides of CommandItemDetailsBase

        /// <summary>
        /// Flag for whether the OnClick() pushes the command onto the Undo stack
        /// </summary>
        public override bool CanUndo => _scope != null && (_scope.TrackedChanges.Count) > 0;

        #endregion
    }
}
