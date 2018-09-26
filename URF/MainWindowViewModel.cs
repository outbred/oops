using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using WpfAsyncPack.Command;

namespace DURF.Demo
{
    public class MainWindowViewModel : TrackableViewModel
    {
        private Scope _scope;

        public string FirstName
        {
            get => Get<string>();
            set => Set(value);
        }

        public string LastName
        {
            get => Get<string>();
            set => Set(value);
        }

        public string Address
        {
            get => Get<string>();
            set => Set(value);
        }

        public string City
        {
            get => Get<string>();
            set => Set(value);
        }

        public string State
        {
            get => Get<string>();
            set => Set(value);
        }

        public string Zip
        {
            get => Get<string>();
            set => Set(value);
        }

        #region Overrides of TrackableViewModel

        /// <inheritdoc />
        protected override void OnLoaded()
        {
            base.OnLoaded();
            _scope = new Scope("Address");
        }

        /// <inheritdoc />
        protected override void OnUnloaded()
        {
            base.OnUnloaded();
            _scope.Dispose();
        }

        #endregion

        public AsyncCommand CommitChanges => new AsyncCommand(async (_, t) =>
        {
            _scope.Dispose();
            _scope = null;
        }, _ => _scope != null);

        public AsyncCommand Track
        {
            get
            {
                return new AsyncCommand(async (_, t) =>
                {
                    if (_scope != null)
                        return;
                    _scope = new Scope("Address");
                }, _ => _scope == null);
            }
        }

        public AsyncCommand Undo => new AsyncCommand(async (_, t) =>
        {
            await ScopeManager.Instance.UndoLast();
        }, _ => ScopeManager.Instance.Undoables.Any());

        public AsyncCommand Redo => new AsyncCommand(async (_, t) =>
        {
            await ScopeManager.Instance.RedoLast();
        }, _ => ScopeManager.Instance.Redoables.Any());
    }
}
