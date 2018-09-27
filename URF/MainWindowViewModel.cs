using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using DURF.Collections;

#pragma warning disable 1998

namespace DURF.Demo
{
    public class MainWindowViewModel : TrackableViewModel
    {
        private Scope _scope;
        private int _scopeNum = 1;
        private string ScopeName => $"Address Changes {_scopeNum}";

        public MainWindowViewModel()
        {
            Items = new TrackableCollection<string>();
        }

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

        public TrackableCollection<string> Items
        {
            get => Get<TrackableCollection<string>>();
            set => Set(value);
        }

        public ICommand AddItem => new AsyncCommand(async () => Items.Add(Guid.NewGuid().ToString()));

        public ICommand RemoveItem => new AsyncCommand(async () =>
        {
            if (Items.Any())
                Items.RemoveAt(Items.Count - 1);
        });

        private bool _threadedDemo;

        public bool ThreadedDemo
        {
            get => _threadedDemo;
            set
            {
                if (_threadedDemo == value)
                    return;

                _threadedDemo = value;
                RaisePropertyChanged();
            }
        }

        public ICommand InThreadedDemo => new AsyncCommand(async () => ThreadedDemo = true);

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

        private AsyncCommand _commitChanges = null;
        public AsyncCommand CommitChanges
        {
            get
            {
                if (_commitChanges == null)
                {
                    _commitChanges = new AsyncCommand(async () =>
                    {
                        _scope.Dispose();
                        _scope = null;
                        _scopeNum++;
                        RaisePropertyChanged(nameof(Current));
                        _track.RaiseCanExecuteChanged();
                    }, () => _scope != null);
                }

                return _commitChanges;
            }
        }

        private AsyncCommand _track = null;

        public AsyncCommand Track
        {
            get
            {
                if (_track == null)
                {
                    _track = new AsyncCommand(async () =>
                        {
                            if (_scope != null)
                                return;
                            _scope = new Scope(ScopeName);
                            RaisePropertyChanged(nameof(Current));
                            _commitChanges.RaiseCanExecuteChanged();
                        }, () => _scope == null);
                }

                return _track;
            }
        }

        public ScopeManager Manager => ScopeManager.Instance;

        public TrackableScope Current => TrackableScope.Current;
    }
}