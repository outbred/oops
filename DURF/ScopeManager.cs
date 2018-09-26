using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using DURF.Collections;
using DURF.Interfaces;

namespace DURF 
{
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

        public ICommand Undo => new AsyncCommand<TrackableScope>(async item =>
        {
            if(item == null)
                await UndoLast();
            else
            {
                while (Undoables.TryPop(out var scope))
                {
                    using (new Scope(scope.Name, ScopeState.Redo))
                        await scope.UndoAllChanges(scope.Name);

                    if (scope == item) 
                        break;
                }
            }
        }, _ => Undoables.Any());

        public ICommand Redo => new AsyncCommand<TrackableScope>(async item =>
        {
            if (item == null)
                await RedoLast();
            else
            {
                while (Redoables.TryPop(out var scope))
                {
                    using (new Scope(scope.Name, ScopeState.Undo))
                        await scope.UndoAllChanges(scope.Name);

                    if (scope == item)
                        break;
                }
            }
        }, _ => Redoables.Any());

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