using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Windows.Input;
using Prism.Commands;
using Microsoft.Practices.Prism.PubSubEvents;
using NRK.Core;
using NRK.Logging;
using UI.Models;
using UI.Models.Base;
using UI.Models.Collections;
using UI.Models.Interfaces;
using UI.Models.Interfaces.Commands;
using UI.Models.ViewModelBases;
using URF.Base;
using URF.Interfaces;

namespace UI.Commands.Base
{
    public class QueueTask
    {
        public Func<Task> Task { get; set; }
        public bool Redo { get; set; }
    }

    public interface ILoggableCommand
    {
        CommandItemDetailsBase.ExecutionType ExecutionType { get; }

        IUndoRedoCommand Command { get; }
    }

    public class LoggableCommand : ILoggableCommand
    {
        public CommandItemDetailsBase.ExecutionType ExecutionType { get; private set; }
        public IUndoRedoCommand Command { get; private set; }

        public LoggableCommand(CommandItemDetailsBase.ExecutionType type, IUndoRedoCommand cmd)
        {
            Contract.Requires(cmd != null);
            Command = cmd;
            ExecutionType = type;
        }
    }

    /// <summary>
    /// Manages the undo/redo stack for the application.
    /// Also keeps a running log of all commands execute (click, undo, or redo), and the order in which they were executed.
    /// </summary>
    [Export(typeof(ICommandStateManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    [Serializable]
    public class CommandStateManager : ViewModelBase, ICommandStateManager
    {
        private readonly Stack<ICommandBase> _undos = null;
        private readonly Stack<ICommandBase> _redos = null;
        private readonly TrackableCollection<ILoggableCommand> _allCommands = null;
        private readonly TrackableCollection<ICommandBase> _executed = null;
        private readonly NrkThreadSafeList<IRepeatableCommand> _repeatable = null;
        private ICommandBase _currentCmd = null;
        private readonly NrkQueue _processingQueue = new NrkQueue("Command Processing Queue");
        private CommandProcessor _processor = null;
        private DelegateCommand<object> _onRedo = null;
        private DelegateCommand<object> _onUndo = null;
        private DelegateCommand<object> _onRepeat = null;
        private static readonly ILogIt logger = LogItFactory.Instance.GetLogger("CommandStateManager");


        protected CommandStateManager(SerializationInfo info, StreamingContext context) : base(info, context) { }

        public CommandStateManager()
        {
            _undos = new Stack<ICommandBase>();
            _redos = new Stack<ICommandBase>();
            _allCommands = new TrackableCollection<ILoggableCommand>();
            _executed = new TrackableCollection<ICommandBase>() { NeverOnUiThread = true };
            _repeatable = new NrkThreadSafeList<IRepeatableCommand>();
            _processor = new CommandProcessor(this, _processingQueue);

            Aggregator.GetEvent<LoadingProjectEvent>().Subscribe
                (ignore =>
                {
                    Clear();
                    _processor.PauseThread(true);
                }, ThreadOption.BackgroundThread, true);
            Aggregator.GetEvent<LoadedProjectEvent>().Subscribe(_ => _processor.ResumeThread(false));
        }

        public void Initialize()
        {
            if (_processor == null)
            {
                _processor = new CommandProcessor(this, _processingQueue);
            }
        }

        public void Clear()
        {
            _undos.Clear();
            _redos.Clear();
            _executed.Clear();
            _repeatable.Clear();
            _allCommands.Clear();
            RaisePropertyChanged(nameof(CanUndo));
            RaisePropertyChanged(nameof(CanRedo));
            RaisePropertyChanged(nameof(CanRepeat));
            _onUndo?.RaiseCanExecuteChangedOnUi();
            _onRedo?.RaiseCanExecuteChangedOnUi();
            _onRepeat?.RaiseCanExecuteChangedOnUi();
        }

        public void BeginUndo(ICommandBase cmd)
        {
            if (_undos.Contains(cmd))
            {
                // 0th item is top of stack
                var all = _undos.ToList();
                _undos.Clear();
                all.Remove(cmd);
                // reverse it so that 0th item is bottom of stack
                all.Reverse();
                // foreach's over list (starting at 0th item), pushing each onto stack
                foreach (var item in all)
                    _undos.Push(item);
            }

            if (!_redos.Contains(cmd))
            {
                AddRedo(cmd);
                // remove from execution history
                if (_executed.Count > 0)
                {
                    _executed.Remove(cmd);
                    if (_repeatable.Contains(cmd))
                        _repeatable.Remove((IRepeatableCommand)cmd);
                }
            }
        }

        public void BeginRedo(ICommandBase cmd)
        {
            if (_redos.Contains(cmd))
            {
                // 0th item is top of stack
                var all = _redos.ToList();
                _redos.Clear();
                all.Remove(cmd);
                // reverse it so that 0th item is bottom of stack
                all.Reverse();
                // foreach's over list (starting at 0th item), pushing each onto stack
                foreach (var item in all)
                    _redos.Push(item);

                // presume if it hasn't been removed from the _redos stack, that it also hasn't been added to
                // the allCommands list
                _allCommands.Add(new LoggableCommand(ExecutionType.Redo, cmd));
            }
        }

        /// <summary>
        /// Informs the CommandStateManager to shut down the current queue and prepare for a new queue (on demand)
        /// 
        /// Useful for unit tests, but can be useful for project loading/loaded sequence
        /// </summary>
        public void RecycleQueue()
        {
            _processor?.DisposeEx(false);
            _processor = null; // to make sure when Initialize() is called that the queue is restored - Brent (10 Aug 2016)
        }

        public void EnqueueUndo(Func<Task> toExecute)
        {
            _processingQueue.Enqueue(new QueueTask() { Task = toExecute });
        }

        public void EnqueueRedo(Func<Task> toExecute)
        {
            _processingQueue.Enqueue(new QueueTask() { Task = toExecute, Redo = true });
        }

        public void EnqueueExecute(Func<Task> toExecute)
        {
            _processingQueue.Enqueue(new QueueTask() { Task = toExecute });
        }

        public Stack<ICommandBase> Undos => _undos;

        public Stack<ICommandBase> Redos => _redos;

        public TrackableCollection<ICommandBase> Executed => _executed;

        /// <summary>
        /// View into the Executed stack for repeatable commands
        /// </summary>
        public NrkThreadSafeList<IRepeatableCommand> Repeatable => _repeatable;

        public TrackableCollection<ILoggableCommand> Everything => _allCommands;

        // for UI binding
        public bool CanUndo => _undos.Count > 0;

        public bool CanRedo => _redos.Count > 0;

        public bool CanRepeat => _repeatable.Count > 0;

        public async Task BeginExecution(ICommandBase cmd)
        {
            await Task.Run(() =>
            {
                lock (this)
                {
                    if (_currentCmd == null)
                        _currentCmd = cmd;
                    else if (!ReferenceEquals(_currentCmd, cmd))
                        _currentCmd.AddUndo(new Task(async () => await cmd.UndoAction()));
                }
            });
        }

        public void ClearCurrentCommand(ICommandBase cmd)
        {
            lock (this)
            {
                if (ReferenceEquals(_currentCmd, cmd))
                    _currentCmd = null;
            }
        }

        /// <summary>
        /// Call when command is executed (through a button's onclick event, for example).  This is how you log a command, as well.
        /// </summary>
        /// <param name="cmd"></param>
        public Task EndExecution(ICommandBase cmd)
        {
            lock (this)
            {
                if (cmd == null || !ReferenceEquals(_currentCmd, cmd))
                    return Task.Run(() => { });
            }

            // this is backgrounded to band-aid a complicated scenario where the SafeObservableCollection.Insert() is mid-execution, when another Insert comes in from the UI thread, 
            // which is waiting on the write lock release.  The other one, in turn, is waiting on the UI thread release - which creates a deadlock
            // this generally only happens with commands which have the most spider-web like execution patterns (so far), so ensure this is all backgrounded to aid in avoiding it - bbulla
            return Task.Run(() =>
            {
                if (cmd.CanUndo)
                {
                    _undos.Push(cmd);
                    RaisePropertyChanged(nameof(CanUndo));
                    _onUndo?.RaiseCanExecuteChangedOnUi();
                }
                if (!InRedo && _redos.Any())
                {
                    _redos.Clear();
                    RaisePropertyChanged(nameof(CanRedo));
                    _onRedo?.RaiseCanExecuteChangedOnUi();
                }
                _allCommands.Add(new LoggableCommand(ExecutionType.Execute, cmd));
                _executed.Add(cmd);
                var item = cmd as IRepeatableCommand;
                if (item != null)
                {
                    _repeatable.Add(item);
                    RaisePropertyChanged(nameof(CanRepeat));
                    _onRepeat?.RaiseCanExecuteChangedOnUi();
                }
                lock (this)
                    _currentCmd = null;
            });
        }

        /// <summary>
        /// Call when command is undone (Undo() is called)
        /// </summary>
        /// <param name="cmd"></param>
        private void AddRedo(ICommandBase cmd)
        {
            if (cmd == null)
                return;

            _redos.Push(cmd);
            _allCommands.Add(new LoggableCommand(ExecutionType.Undo, cmd));
            _onRedo?.RaiseCanExecuteChangedOnUi();
            RaisePropertyChanged(nameof(CanRedo));
        }

        /// <summary>
        /// Bind to "OnUndo"
        /// 
        /// Undoes the last command on the undo stack or unwinds to the selected command (inclusive)
        /// </summary>
        public ICommand OnUndo
        {
            get
            {
                if (_onUndo == null)
                {

                    _onUndo = new DelegateCommand<object>(input =>
                    {

                        var cmd = input as ICommandBase;

                        //TODO need to reimplement the below line?
                        //_undos.Reorder(s => s.ExecuteEnd);

                        // The view orders the undos list by ExecuteBegin, so we'll do the same
                        var last = _undos.Pop();
                        var last1 = last;
                        var actions = new List<QueueTask>();
                        actions.Add(new QueueTask() { Task = () => OnUndoTask(last1), Redo = false });

                        // means user clicked the arrow button, or only went back one command
                        if (cmd == last || cmd == null)
                            last = null;
                        else // means the user click one of the drop down commands
                        {
                            last = _undos.Pop();
                            while (true)
                            {
                                var last2 = last;
                                actions.Add(new QueueTask() { Task = () => OnUndoTask(last2), Redo = false });
                                if (!_undos.Any())
                                {
                                    last = null;
                                    break;
                                }
                                last = _undos.Pop();
                                if (last == cmd)
                                    break;
                            }
                        }

                        if (last != null)
                        {
                            var last3 = last;
                            actions.Add(new QueueTask() { Task = () => OnUndoTask(last3), Redo = false });
                        }

                        _processingQueue.Enqueue(actions);

                        RaisePropertyChanged(nameof(CanUndo));
                        _onUndo?.RaiseCanExecuteChangedOnUi();
                    }, _ => CanUndo);
                }
                return _onUndo;
            }

        }

        /// <summary>
        /// This is the handler for the queue - when the command has been dequeued and ready to be undone
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        private async Task OnUndoTask(ICommandBase cmd)
        {
            logger.Debug($"Undoing '{cmd.GetType().ToString()}' command");
            await cmd.UndoInQueue();
        }

        public bool InRedo { get; set; } = false;

        /// <summary>
        /// Bind to "OnRedo"
        /// </summary>
        public ICommand OnRedo
        {
            get
            {
                if (_onRedo == null)
                {
                    _onRedo = new DelegateCommand<object>(cmd =>
                    {

                        // The view orders the redos list by ExecuteBegin, so we'll do the same
                        //TODO Need to reimplement the below line?
                        //_redos.Reorder(s => s.UndoBegin);

                        var last = _redos.Pop();
                        var last1 = last;
                        var redos = new List<QueueTask>();
                        redos.Add(new QueueTask() { Task = () => OnRedoTask(last1), Redo = true });

                        if (cmd != null && cmd != last)
                        {
                            while (true)
                            {
                                last = _redos.Pop();
                                var last2 = last;
                                redos.Add(new QueueTask() { Task = () => OnRedoTask(last2), Redo = true });
                                if (last == cmd)
                                    break;
                            }
                        }

                        _processingQueue.Enqueue(redos);

                        RaisePropertyChanged(nameof(CanUndo));
                        _onUndo?.RaiseCanExecuteChangedOnUi();
                        RaisePropertyChanged(nameof(CanRedo));
                        _onRedo?.RaiseCanExecuteChangedOnUi();
                    }, _ => CanRedo);
                }
                return _onRedo;
            }
        }

        private async Task OnRedoTask(ICommandBase cmd)
        {
            _allCommands.Add(new LoggableCommand(ExecutionType.Redo, cmd));
            // OnClick wrapper will push the command back to undo stack - bbulla
            logger.Debug($"Redoing '{cmd.GetType().ToString()}' command");

            await cmd.RedoAndForget();
        }

        public ICommand OnRepeat
        {
            get
            {
                if (_onRepeat == null)
                {
                    _onRepeat = new DelegateCommand<object>(_ =>
                    {
                        var last = _repeatable.LastOrDefault();
                        if (last != null)
                        {
                            var copy = last.Clone<ICommandBase>();
                            copy.OnClick.Execute(null);
                            // let exceptions bubble up to the user
                        }
                    }, _ => CanRepeat);
                }
                return _onRepeat;
            }
        }

        #region Overrides of ViewModelBase

        protected override void OnDispose()
        {
            _processor?.DisposeEx(false);
            //_processor = null;    // don't need to null this out... why was it being nulled? - Joe (29 Jul 2016)
            base.OnDispose();
        }

        #endregion
    }
}