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

        public ICommand CommitChanges
        {
            get
            {
                return new AsyncCommand(async (_, t) =>
                {
                    _scope.Dispose();
                    _scope = new Scope("Address");
                });
            }
        }

        public ICommand Undo
        {
            get
            {
                return new AsyncCommand(async (_, t) =>
                {
                    if (ScopeManager.Instance.Undoables.TryPop(out var scope))
                    {
                        using(new Scope("Address Redo"))
                            await scope.UndoAllChanges(scope.Name);
                    }
                });
            }
        }

        public ICommand Redo
        {
            get
            {
                return new AsyncCommand(async (_, t) =>
                {
                    if (ScopeManager.Instance.Redoables.TryPop(out var scope))
                    {
                        using (new Scope("Address Undo"))
                            await scope.UndoAllChanges(scope.Name);
                    }
                });
            }
        }
    }
}
