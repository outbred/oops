using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
#pragma warning disable 1998

namespace DURF.Demo
{
    public class MainWindowViewModel : TrackableViewModel
    {
        private Scope _scope;
        private int _scopeNum = 1;
        private string ScopeName => $"Address Changes{_scopeNum}";

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
            _scope = new Scope(ScopeName);
            RaisePropertyChanged(nameof(Current));
        }

        /// <inheritdoc />
        protected override void OnUnloaded()
        {
            base.OnUnloaded();
            _scope.Dispose();
        }

        #endregion

        public AsyncCommand CommitChanges => new AsyncCommand(async () =>
        {
            _scope.Dispose();
            _scope = null;
            _scopeNum++;
            RaisePropertyChanged(nameof(Current));
        }, () => _scope != null);

        public AsyncCommand Track
        {
            get
            {
                return new AsyncCommand(async () =>
                {
                    if (_scope != null)
                        return;
                    _scope = new Scope(ScopeName);
                    RaisePropertyChanged(nameof(Current));
                }, () => _scope == null);
            }
        }

        public ScopeManager Manager => ScopeManager.Instance;

        public TrackableScope Current => TrackableScope.Current;
    }
}
