using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using DURF.Collections;
using DURF.Interfaces;

namespace DURF
{
    public class ScopeManager : TrackableViewModel
    {
        protected override bool TrackChanges => false;

        private TrackableCollection<TrackableScope> _undoables = new TrackableCollection<TrackableScope>();
        private TrackableCollection<TrackableScope> _redoables = new TrackableCollection<TrackableScope>();

        /// <summary>
        /// Readonly view into Undoables collection.  To add to the Undoables, use the Add() method
        /// </summary>
        public IReadOnlyCollection<TrackableScope> Undoables => _undoables;

        /// <summary>
        /// Readonly view into Redoables collection.  Only manipulated internally
        /// </summary>
        public IReadOnlyCollection<TrackableScope> Redoables => _redoables;

        private static ScopeManager _instance = null;

        /// <summary>
        /// Singleton instance, if that's your flavor.  If using injection (preferred), can use this as well depending on if you feed the injection container this instance.
        /// </summary>
        public static ScopeManager Instance
        {
            get
            {
                Interlocked.CompareExchange(ref _instance, new ScopeManager(), null);
                return _instance;
            }
        }

        public void Add(TrackableScope scope, ScopeState state)
        {
            switch (state)
            {
                case ScopeState.Do:
                    _redoables.Clear();
                    _undoables.Push(scope);
                    break;
                case ScopeState.Undo:
                    _undoables.Push(scope);
                    break;
                case ScopeState.Redo:
                    _redoables.Push(scope);
                    break;
            }
        }

        /// <summary>
        /// Clears all undo's and redo's
        /// </summary>
        public void Reset()
        {
            _undoables.Clear();
            _redoables.Clear();
        }

        private AsyncCommand<TrackableScope> _undo = null;

        /// <summary>
        /// Performs an Undo up to and including the scope supplied.  If none supplied, undoes the last scope
        /// </summary>
        public AsyncCommand<TrackableScope> Undo
        {
            get
            {
                if (_undo == null)
                {
                    _undo = new AsyncCommand<TrackableScope>(async item =>
                        {
                            if (item == null)
                                await UndoLast();
                            else
                            {
                                while (_undoables.TryPop(out var scope))
                                {
                                    using (new Scope(scope.Name, ScopeState.Redo))
                                        await scope.UndoAllChanges(scope.Name);

                                    if (scope == item)
                                        break;
                                }
                            }
                        },
                        _ => _undoables.Any());
                }

                return _undo;
            }
        }

        private AsyncCommand<TrackableScope> _redo = null;

        /// <summary>
        /// Performs a Redo up to and including the scope supplied.  If none supplied, redoes the last scope
        /// </summary>
        public AsyncCommand<TrackableScope> Redo
        {
            get
            {
                if (_redo == null)
                {
                    _redo = new AsyncCommand<TrackableScope>(async item =>
                        {
                            if (item == null)
                                await RedoLast();
                            else
                            {
                                while (_redoables.TryPop(out var scope))
                                {
                                    using (new Scope(scope.Name, ScopeState.Undo))
                                        await scope.UndoAllChanges(scope.Name);

                                    if (scope == item)
                                        break;
                                }
                            }
                        }, _ => _redoables.Any());
                }

                return _redo;
            }
        }

        public async Task UndoLast()
        {
            if (_undoables.TryPop(out var scope))
            {
                using (new Scope(scope.Name, ScopeState.Redo))
                    await scope.UndoAllChanges(scope.Name);
            }
        }

        public async Task RedoLast()
        {
            if (_redoables.TryPop(out var scope))
            {
                using (new Scope(scope.Name, ScopeState.Undo))
                    await scope.UndoAllChanges(scope.Name);
            }
        }
    }
}