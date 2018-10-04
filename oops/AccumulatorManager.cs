using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using oops.Collections;
using oops.Interfaces;

namespace oops
{
    public class AccumulatorManager : TrackableViewModel, IAccumulatorManager
    {
        protected override bool TrackChanges => false;

        private TrackableCollection<Accumulator> _undoables = new TrackableCollection<Accumulator>() { TrackChanges = false};
        private TrackableCollection<Accumulator> _redoables = new TrackableCollection<Accumulator>() { TrackChanges = false };

        /// <summary>
        /// Readonly view into Undoables collection.  To add to the Undoables, use the Add() method
        /// </summary>
        public IReadOnlyCollection<Accumulator> Undoables => _undoables;

        /// <summary>
        /// Readonly view into Redoables collection.  Only manipulated internally
        /// </summary>
        public IReadOnlyCollection<Accumulator> Redoables => _redoables;

        private static AccumulatorManager _instance = null;

        /// <summary>
        /// Singleton instance, if that's your flavor.  If using injection (preferred), can use this as well depending on if you feed the injection container this instance.
        /// </summary>
        public static AccumulatorManager Instance
        {
            get
            {
                Interlocked.CompareExchange(ref _instance, new AccumulatorManager(), null);
                return _instance;
            }
        }

        public void Add(Accumulator scope, ScopeState state)
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

        private AsyncCommand<Accumulator> _undo = null;

        /// <summary>
        /// Performs an Undo up to and including the scope supplied.  If none supplied, undoes the last scope
        /// </summary>
        public AsyncCommand<Accumulator> Undo
        {
            get
            {
                if (_undo == null)
                {
                    _undo = new AsyncCommand<Accumulator>(async item =>
                        {
                            if (item == null)
                                await UndoLast();
                            else
                            {
                                while (_undoables.TryPop(out var scope))
                                {
                                    using (new TrackableScope(scope.Name, ScopeState.Redo))
                                        await scope.UndoAll(scope.Name);

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

        private AsyncCommand<Accumulator> _redo = null;

        /// <summary>
        /// Performs a Redo up to and including the scope supplied.  If none supplied, redoes the last scope
        /// </summary>
        public AsyncCommand<Accumulator> Redo
        {
            get
            {
                if (_redo == null)
                {
                    _redo = new AsyncCommand<Accumulator>(async item =>
                        {
                            if (item == null)
                                await RedoLast();
                            else
                            {
                                while (_redoables.TryPop(out var scope))
                                {
                                    using (new TrackableScope(scope.Name, ScopeState.Undo))
                                        await scope.UndoAll(scope.Name);

                                    if (scope == item)
                                        break;
                                }
                            }
                        }, _ => _redoables.Any());
                }

                return _redo;
            }
        }

        /// <summary>
        /// Undoes the scope on the top of the stack, pushing the new scope to the redo stack
        /// </summary>
        /// <returns></returns>
        public async Task UndoLast()
        {
            if (_undoables.TryPop(out var scope))
            {
                using (new TrackableScope(scope.Name, ScopeState.Redo))
                    await scope.UndoAll(scope.Name);
            }
        }

        /// <summary>
        /// Redoes the scope on the top of the stack, pushing the new scope to the undo stack
        /// </summary>
        /// <returns></returns>
        public async Task RedoLast()
        {
            if (_redoables.TryPop(out var scope))
            {
                using (new TrackableScope(scope.Name, ScopeState.Undo))
                    await scope.UndoAll(scope.Name);
            }
        }
    }
}