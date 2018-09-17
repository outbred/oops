using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cannoli;
using NRK.Data.Framework;
using NRK.Data.Metrology;
using NRK.Logging;
using UI.Models;
using UI.Models.Base;
using UI.Models.Collections;
using UI.Models.Facade;
using UI.Models.Interfaces;
using UI.Models.Interfaces.Commands;
using UI.Models.ViewModelBases;
using URF.Base;
using URF.Interfaces;

namespace UI.Commands.Base
{
    //[InheritedExport(typeof(ICommandItemDetails))]
    public abstract class CommandBase : ViewModelBase, ICommandBase
    {
        protected static readonly ICommandStateManager _manager = null;
        private static readonly ILogIt logger = LogItFactory.Instance.GetLogger("CommandBase");
        // If a command is executing, any other cmd's undo that is executed within this one's execution is accumulated into one command...this one - Brent (28 Jan 2016)
        protected readonly TrackableCollection<Task> _accumulatedUndos = new TrackableCollection<Task>() { NeverOnUiThread = true, ShutOffCollectionChangedEventsOnUiThread = true };
        protected readonly TrackableCollection<Task> _accumulatedRedos = new TrackableCollection<Task>() { NeverOnUiThread = true, ShutOffCollectionChangedEventsOnUiThread = true };

        protected DisplayConverter Converter => Building?.Converter;
        // the idea is that later we can have a command that changes this so that scripting can work on multiple facades at once
        protected internal static IFacade CurrentFacade => Injector.GetInstance<IFacade>();


        protected static ICommandBase CurrentUndoCommand { get; private set; }

        static CommandBase()
        {
            try
            {
                _manager = IoC.Current.Container.GetInstance<ICommandStateManager>();
            }
            catch
            {
                _manager = new CommandStateManager(); // unit test scenario
            }
        }

        #region Unit test support

        /// <summary>
        /// By default, execution/undo/redo is deemed 'Passed' if it finishes.
        /// 
        /// Set this to false if a cmd needs to manually control whether it passes or fails
        /// </summary>
        protected virtual bool AwaitMeansPassed { get; } = true;

        /// <summary>
        /// Check after awaiting the ExecuteSync or ExecuteAsync methods
        /// </summary>
        public bool ExecutePassed { get; protected set; }

        public async Task UndoAsync()
        {
            await UndoAndForget();
            _undoCompleted.WaitOne();
        }

        /// <summary>
        /// Check after awaiting the UndoAsync method
        /// </summary>
        public bool UndoPassed { get; protected set; }

        /// <summary>
        /// Check after awaiting the RedoAsync method
        /// </summary>
        public bool RedoPassed { get; protected set; }

        public async Task RedoAsync()
        {
            await RedoAndForget();
            _redoCompleted.WaitOne();
        }

        #endregion

        public virtual CommandTypes? CommandType { get; set; }
        public virtual string MenuName { get; set; }

        public void AddUndo(Task onUndo)
        {
            _accumulatedUndos.Add(onUndo);
        }

        public void AddRedo(Task onUndo)
        {
            _accumulatedRedos.Add(onUndo);
        }

        /// <summary>
        /// override to true for commands that must sweep dirt while waiting 
        /// </summary>
        protected virtual bool DemandSweepDirtAfterCurrentOperation => false;

        /// <summary>
        /// override to false for commands that don't need to sweep.  If demand is set, this variable is ignored.
        /// </summary>
        protected virtual bool RequestSweepDirtAfterCurrentOperation => true; // need to make sure commands that don't need sweep override this! - Joe (19 Feb 2016)

        /// <summary>
        /// If CommandType is null, then this value is used for display to the user
        /// </summary>
        public virtual string Header { get; set; }

        //TEB 9.18.15--Made ToolTip abstract to force implementation by subclasses.
        public abstract string ToolTip { get; set; }

        public override string ToString()
        {
            return ToolTip ?? this.GetType().Name;
        }

        /// <summary>
        /// Used to bind a DataContext to the menu item
        /// </summary>
        public virtual object DataContext { get; protected set; }

        /// <summary>
        /// Wired up by the base class when the ctor is called
        /// 
        /// Puts the command into the undo stack when invoked. 
        /// 
        /// This command just enqueues the ClickAction into the CommandProcessor. If you MUST wait on the command to finish executing before proceeding,
        /// then use ExecuteSync() or ExecuteAsync()
        /// 
        /// If executing this command in code, use this property and not ClickAction
        /// </summary>
        public virtual IAsyncCommand OnClick { get; }

        /// <summary>
        /// Scripting shortcut
        /// </summary>
        /// <returns>true if able to execute</returns>
        //[ScriptableMethod] // this was enabled, but with the new ctor approach, we no longer need this available via script - Brent (27 Jan 2016)
        public async Task<bool> ExecuteAsync(object input = null)
        {
            if (OnClick.CanExecute(input))
            {
                logger.Debug($"BEGIN executing {this.GetType().FullName}");
                await OnClick.ExecuteAsync(input);
                await Task.Run(() => _onClickFinished.WaitOne());
                logger.Debug($"END executing {this.GetType().FullName}");

                return true;
            }
            return false;
        }

        /// <summary>
        /// Scripting shortcut
        /// </summary>
        /// <returns>true if able to execute</returns>
        //[ScriptableMethod] // this was enabled, but with the new ctor approach, we no longer need this available via script - Brent (27 Jan 2016)
        public bool ExecuteSync(object input = null)
        {
            if (OnClick.CanExecute(input))
            {
                logger.Debug($"BEGIN executing {this.GetType().FullName}");
                OnClick.ExecuteAsync(input);
                var result = Task.Run(() => _onClickFinished.WaitOne()).Result;
                logger.Debug($"END executing {this.GetType().FullName}");

                return true;
            }
            return false;
        }

        public bool ExecuteAndForget(object input = null)
        {
            if (OnClick.CanExecute(input))
            {
                OnClick.Execute(input);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Enqueues the RedoAction.  Use RedoAsync() to await the redo to be completed
        /// 
        /// By default, OnClick() is called for a redo, but allow an inheriting class to override this if there's something special (like undoing a deletion)
        /// 
        /// In a derived class to override what happens on Redo, override the OnRedo property instead of this method
        /// </summary>
        public async Task RedoAndForget()
        {
            await Task.Run(() =>
            {
                _manager.EnqueueRedo(async () =>
                {
                    try
                    {
                        RedoPassed = false;
                        _manager.BeginRedo(this);
                        await _manager.BeginExecution(this);
                        logger.Debug($"BEGIN redo for {this.GetType().FullName}");
                        CurrentState = ExecutionType.Redo;
                        await OnRedo;
                        // do this post-execution so that (if needed) the executing command can inject other commands to put onto the execution stack first (like pre-execution)
                        // eg AddMeasurementPoint cmd will create a target construction if there isn't one already -- bbulla
                        RedoBegin = DateTime.Now;

                        if (DemandSweepDirtAfterCurrentOperation && CurrentFacade != null) // Only demand if needed - joe (10 Oct 2015)
                            await CurrentFacade.DemandDirtSweep(); // jmc 2015.10.05 - 
                        else if (RequestSweepDirtAfterCurrentOperation)
                            CurrentFacade?.RequestDirtSweep(); // request optional - Joe (19 Feb 2016)

                        logger.Debug($"END redo for {this.GetType().FullName}");
                        if (AwaitMeansPassed)
                        {
                            RedoPassed = true;
                        }

                        await _manager.EndExecution(this);

                        _redoCompleted.Set();
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"Unable to execute command '{this.GetType().Name}'", ex);
                        throw; // preserve stack trace
                    }
                });
            });
        }

        protected virtual Task OnRedo
        {
            get
            {
                return Task.Run(async () =>
                {
                    await ExecuteAction(_clickContext);
                    RedoPassed = ExecutePassed;
                });
            }
        }

        /// <summary>
        /// Represents the 'meat' of the undo execution, specific to each command.
        /// 
        /// If used directly by external code, the command will not be managed in the undo/redo stack.
        /// 
        /// this is public ONLY b/c we have commands outside of UI.Commands
        /// 
        /// Should only be used by encapsulating commands
        /// </summary>
        /// <returns></returns>
        public abstract Task UndoAction();

        #region abstract OnClick components

        /// <summary>
        /// If used directly, the command will NOT be managed in the undo/redo stack
        /// 
        /// Should only be used by encapsulating commands
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public abstract Task ExecuteAction(object context);

        protected virtual bool CanExecuteClickAction()
        {
            return true;
        }

        #endregion

        // We will want the scripting user to use the cmdstatemgr for this, or the Undo() class
        //[ScriptableMethod]
        public async Task UndoAndForget()
        {
            await Task.Run(() =>
            {
                _manager.EnqueueUndo(async () =>
                {
                    await UndoInQueue();
                });
            });
        }

        /// <summary>
        /// Executed after a command has come through the queue
        /// </summary>
        /// <returns></returns>
        public async Task UndoInQueue()
        {
            try
            {
                lock (this)
                    CurrentUndoCommand = this;
                CurrentState = ExecutionType.Undo;
                UndoBegin = DateTime.Now;
                _manager.BeginUndo(this);

                foreach (var undo in _accumulatedUndos)
                {
                    undo.Start();
                    await undo;
                }

                _accumulatedUndos.Clear();

                await UndoAction();

                if (DemandSweepDirtAfterCurrentOperation && CurrentFacade != null) // Only demand if needed - joe (10 Oct 2015)
                    await CurrentFacade.DemandDirtSweep(); // jmc 2015.10.05 - 
                else if (RequestSweepDirtAfterCurrentOperation)
                    CurrentFacade?.RequestDirtSweep(); // request optional - Joe (19 Feb 2016)

                if (AwaitMeansPassed)
                    UndoPassed = true;

                _undoCompleted.Set();
            }
            catch (Exception ex)
            {
                logger.Error($"Unable to undo command '{this.GetType().Name}'", ex);
                throw; // preserve stack trace
            }
            finally
            {
                lock (this)
                    CurrentUndoCommand = null;
            }
        }

        public virtual IEnumerable<ICommandBase> Children { get; set; }

        protected object _clickContext = null;
        protected readonly ManualResetEvent _onClickFinished = new ManualResetEvent(false);
        protected readonly ManualResetEvent _undoCompleted = new ManualResetEvent(false);
        protected readonly ManualResetEvent _redoCompleted = new ManualResetEvent(false);

        /// <summary>
        /// If for whatever reason this command must be executed outside the command processor queue, set to false
        /// 
        /// In a general sense, do not use (shouldn't have to use this...except in one known scenario in the ScopeCommand b/c it is a GENERIC command)
        /// </summary>
        protected virtual bool Enqueue => true;

        /// <summary>
        /// Only used by inheriting classes where ClickAction and UndoAction have been overridden
        /// </summary>
        protected CommandBase(params object[] arguments)
        {
            // eventually, control the Facade reference by commands - Brent (22 Jul 2016)
            Facade = ActiveFacade;

            OnClick = new AsyncDelegateCommand<object>
                (async item =>
                {
                    await Task.Run(async () =>
                    {
                        if (Enqueue)
                            _manager.EnqueueExecute(async () => await OnDequeuedExecute(item));
                        else
                            await OnDequeuedExecute(item);
                    });
                }, (c) => CanExecuteClickAction());

            ConstructorArguments = new Queue<object>();

            foreach (var arg in arguments)
            {
                if (arg == null)
                {
                    ConstructorArguments.Enqueue(null);
                    continue;
                }

                // Allowing commands to enqueue objects or strings as needed - will that cause problems? - Neil (17 May 2016)
                // yes...any argument enqueued needs to be scriptable (ie a string) - do not remove this check - Brent (23 may 2016)
                var type = arg.GetType();
                if (arg is string || arg is IEnumerable<string> || type.IsValueType)
                {
                    ConstructorArguments.Enqueue(arg);
                }
                else
                {
                    throw new ArgumentException($"Argument of type '{type.FullName}' is not allowed. Must be a value type or castable to IEnumerable<string>", nameof(arguments));
                }
            }
        }

        private async Task OnDequeuedExecute(object item)
        {
            try
            {
                ExecutePassed = false;
                await _manager.BeginExecution(this);
                CurrentState = ExecutionType.Execute;
                _clickContext = item;


                await ExecuteAction(item);

                // record this after the execution so that commands can get undone in the right order - Brent 5 Nov 2015
                ExecuteEnd = DateTime.Now;

                if (DemandSweepDirtAfterCurrentOperation) // missing condition - Todd (09 Oct 2015)
                {
                    await CurrentFacade.DemandDirtSweep(); // jmc 2015.10.05 - 
                }
                else if (RequestSweepDirtAfterCurrentOperation) // made this conditional based on setting in command so we aren't always sweeping - Joe (19 Feb 2016)
                    CurrentFacade.RequestDirtSweep();

                // todo: add this to make sure facade is all caught up after processing data from a command...evaluate if we still need this later...
                //await CurrentFacade.WaitForBuildingChangeQueueToCatchUp(); // appears to hang the ui when shutting down (waiting on queue to process when it has been shut off)
                await _manager.EndExecution(this);

                if (AwaitMeansPassed)
                    ExecutePassed = true;
            }
            catch (Exception ex)
            {
                logger.Error($"Unable to execute command '{this.GetType().Name}'", ex);
                ExecutePassed = false;
                throw; // preserve stack trace
            }
            finally
            {
                _onClickFinished.Set();
            }
        }

        public virtual bool CanUndo => true;

        /// <summary>
        /// Time this command was executed
        /// </summary>
        public DateTime? ExecuteEnd { get; protected set; }

        /// <summary>
        /// Time this command was undone
        /// </summary>
        public DateTime? UndoBegin { get; protected set; }

        public DateTime? RedoBegin { get; protected set; }

        public ExecutionType? CurrentState { get; protected set; }

        /// <summary>
        /// Arguments required to recreate an instance of this command. Used for scripting the command
        /// 
        /// Rules:
        /// 1. If an arg is a list, must be a comma-separated single-string list; not wrapped in curly brackets or anything
        ///  - like "one, two, three" or "one,two,three" but not "{ one, two, three }"
        /// 2. The argument must represent the value, not the name of the param, etc.
        /// 3. The args must be in order - 0-th element is first arg to the ctor, etc.
        /// 4. Each argument must be a string, a value type - int, bool, etc. or an IEnumerable<string>
        /// 5. Null arguments are allowed...but discouraged :) (hopefully it's a sign of an inefficient ctor that we can improve, or of one cmd that's doing too much)
        /// </summary>
        public Queue<object> ConstructorArguments { get; }

        #region Cloning Helpers
        //#if DEBUG
        /// <summary>
        /// For testing purposes, each command must return a 'for-testing' instance of itself.
        /// 
        /// The inheriting command can presume there is a test instance of MetrologyData all set up to be consumed.
        /// </summary>
        /// <typeparam name="TCommand"></typeparam>
        /// <returns></returns>
        protected abstract TCommand GetInstance<TCommand>() where TCommand : CommandBase;

        protected CommandBase GetInstance()
        {
            return GetInstance<CommandBase>();
        }

        /// <summary>
        /// Factory method to product testable instances of each command available in unit tests.
        /// 
        /// The inheriting command sets up a testable instance of itself in the Getinstace() method
        /// </summary>
        /// <typeparam name="TCommand"></typeparam>
        /// <returns></returns>
        public static TCommand Factory<TCommand>() where TCommand : CommandBase, new()
        {
            var cmd = new TCommand();
            return cmd.GetInstance<TCommand>();
        }

        public static CommandBase Factory(Type command)
        {
            var cmd = Activator.CreateInstance(command) as CommandBase;
            return cmd.GetInstance();
        }
        //#endif
        /// <summary>
        /// Creates a new object that is a copy of the current instance by using the same construction arguments
        /// </summary>
        /// <returns>
        /// A new object that is a copy of this instance.
        /// </returns>
        public T Clone<T>() where T : class, ICommandBase
        {
            var args = ConstructorArguments.ToList();
            // added for the default 'executeNow' argument - Brent (23 Feb 2016)
            args.Add(false);
            // create a new instance of this one, with the same args
            return Activator.CreateInstance(this.GetType(), args.ToArray()) as T;
        }

        public CommandBase Clone()
        {
            return this.Clone<CommandBase>();
        }

        object ICloneable.Clone()
        {
            return Clone<ICommandBase>();
        }

        #endregion

        /// <summary>
        /// Scripting helper
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        protected static IEnumerable<string> GetAllCubbyPaths(Func<Cubby, bool> filter = null)
        {
            List<string> results = new List<string>();

            if (CurrentFacade == null)
                return results;

            if (filter == null)
            {
                results.AddRange(CurrentFacade.Building.GetDescendantsOfType<Cubby>().Select(entity => entity.GetPath()));
                return results;
            }
            else
            {
                results.AddRange(CurrentFacade.Building.GetDescendantsOfType<Cubby>().Where(filter).Select(entity => entity.GetPath()));
                return results;
            }
        }
    }
}