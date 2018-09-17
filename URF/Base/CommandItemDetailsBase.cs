using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UI.Commands.Base;
using UI.Models.Collections;

namespace URF.Base
{
    public abstract class CommandItemDetailsBase : ViewModelBase, ICommandItemDetails
    {
        protected static readonly ICommandStateManager _manager = null;
        // If a command is executing, any other cmd's undo that is executed within this one's execution is accumulated into one command...this one - Brent (28 Jan 2016)
        protected readonly TrackableCollection<Task> _accumulatedUndos = new TrackableCollection<Task>() { DoNotTrackChanges = true, NeverOnUiThread = true, ShutOffCollectionChangedEventsOnUiThread = true };
        protected readonly TrackableCollection<Task> _accumulatedRedos = new TrackableCollection<Task>() { DoNotTrackChanges = true, NeverOnUiThread = true, ShutOffCollectionChangedEventsOnUiThread = true };

        static CommandItemDetailsBase()
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
            await Undo();
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
            await Redo();
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
        /// Available for temp classes like commands that don't want their changes tracked in a current scope
        /// </summary>
        protected override bool TrackChanges => false;


        /// <summary>
        /// njt - please override to true for commands that should sweep dirt - defaulting TRUE here is sweeping after every command, often calling twice
        /// </summary>
        protected virtual bool DemandSweepDirtAfterCurrentOperation => false;

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
                Logger.Debug($"BEGIN executing {this.GetType().FullName}");
                await OnClick.ExecuteAsync(input);
                await Task.Run(() => _onClickFinished.WaitOne());
                Logger.Debug($"END executing {this.GetType().FullName}");

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
                Logger.Debug($"BEGIN executing {this.GetType().FullName}");
                OnClick.ExecuteAsync(input);
                var result = Task.Run(() => _onClickFinished.WaitOne()).Result;
                Logger.Debug($"END executing {this.GetType().FullName}");

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
        /// By default, OnClick() is called for a redo, but allow an inheriting class to override this if there's something special (like undoing a deletion)
        /// 
        /// In a derived class to override what happens on Redo, override the OnRedo property instead of this method
        /// </summary>
        public async Task Redo()
        {
            await Task.Run(() =>
            {
                _manager.EnqueueRedo(async () =>
                {
                    try
                    {
                        RedoPassed = false;
                        await _manager.BeginExecution(this);
                        Logger.Debug($"BEGIN redo for {this.GetType().FullName}");
                        CurrentState = ExecutionType.Redo;
                        await OnRedo;
                        // do this post-execution so that (if needed) the executing command can inject other commands to put onto the execution stack first (like pre-execution)
                        // eg AddMeasurementPoint cmd will create a target construction if there isn't one already -- bbulla
                        RedoBegin = DateTime.Now;

                        if (DemandSweepDirtAfterCurrentOperation) // Only demand if needed - joe (10 Oct 2015)
                            await DataRoot.BackPlane.DemandDirtSweepNow(); // jmc 2015.10.05 - 

                        await _manager.EndExecution(this);

                        Logger.Debug($"END redo for {this.GetType().FullName}");
                        if (AwaitMeansPassed)
                        {
                            RedoPassed = true;
                        }
                        _redoCompleted.Set();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Unable to execute command '{this.GetType().Name}'", ex);
                        throw; // preserve stack trace
                    }
                });
            });
        }

        protected virtual Task OnRedo
        {
            get
            {
                return Task.Run(() =>
                {
                    ClickAction(_clickContext);
                    RedoPassed = ExecutePassed;
                });
            }
        }

        /// <summary>
        /// If used directly, the command will not be managed in the undo/redo stack
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
        public abstract Task ClickAction(object context);

        protected virtual bool CanExecuteClickAction()
        {
            return true;
        }

        #endregion

        // We will want the scripting user to use the cmdstatemgr for this, or the Undo() class
        //[ScriptableMethod]
        public async Task Undo()
        {
            await Task.Run(() =>
            {
                _manager.EnqueueUndo(async () =>
                {
                    try
                    {
                        CurrentState = ExecutionType.Undo;
                        UndoBegin = DateTime.Now;

                        foreach (var undo in _accumulatedUndos)
                        {
                            await undo;
                        }

                        _accumulatedUndos.Clear();

                        await UndoAction();

                        if (DemandSweepDirtAfterCurrentOperation)
                            await DataRoot.BackPlane.DemandDirtSweepNow(); // jmc 2015.10.05 - 

                        if (AwaitMeansPassed)
                        {
                            UndoPassed = true;
                        }

                        _undoCompleted.Set();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Unable to undo command '{this.GetType().Name}'", ex);
                        throw; // preserve stack trace
                    }
                });
            });
        }

        protected override bool MapDependencies => false;

        public virtual bool DoNotCloseMenu => false;

        public virtual IEnumerable<ICommandItemDetails> Children { get; set; }

        protected object _clickContext = null;
        protected readonly ManualResetEvent _onClickFinished = new ManualResetEvent(false);
        protected readonly ManualResetEvent _undoCompleted = new ManualResetEvent(false);
        protected readonly ManualResetEvent _redoCompleted = new ManualResetEvent(false);

        /// <summary>
        /// Only used by inheriting classes where ClickAction and UndoAction have been overridden
        /// </summary>
        protected CommandItemDetailsBase(params object[] arguments)
        {
            OnClick = new AsyncDelegateCommand<object>
                (async item =>
                {
                    await Task.Run(() =>
                    {
                        _manager.EnqueueExecute(async () =>
                        {
                            try
                            {
                                ExecutePassed = false;
                                await _manager.BeginExecution(this);
                                CurrentState = ExecutionType.Execute;
                                _clickContext = item;


                                await ClickAction(item);

                                // record this after the execution so that commands can get undone in the right order - Brent 5 Nov 2015
                                ExecuteEnd = DateTime.Now;

                                if (DemandSweepDirtAfterCurrentOperation) // missing condition - Todd (09 Oct 2015)
                                {
                                    lock (DataRoot.CreateSyncRoot)
                                    {
                                        var things = DataRoot.BackPlane.DemandDirtSweepNow().Result; // jmc 2015.10.05 - 
                                        DataRoot.HandleBuildingChanges(things).Wait();
                                    }
                                }
                                else
                                {
                                    DataRoot.SweepDirt = true;
                                }

                                await _manager.EndExecution(this);

                                if (AwaitMeansPassed)
                                {
                                    ExecutePassed = true;
                                }

                            }
                            catch (Exception ex)
                            {
                                Logger.Error($"Unable to execute command '{this.GetType().Name}'", ex);
                                ExecutePassed = false;
                                throw; // preserve stack trace
                            }

                            _onClickFinished.Set();
                        });
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

        #region Helpers

        protected static IEnumerable<string> GetAllCubbyPaths(Func<EntityUi, bool> filter = null)
        {
            List<string> results = new List<string>();
            //njt 2015.03.31 - put a return for flat list that respects the locks
            if (filter == null)
            {
                results.AddRange(DataRoot.GetAllEntities().Select(entity => entity.CubbyPath));
                return results;
            }
            else
            {
                results.AddRange(DataRoot.GetAllEntities().Where(filter).Select(entity => entity.CubbyPath));
                return results;
            }
        }

        public static EntityUi FindEntityForCubbyPath(string cubbyPath)
        {
            try
            {
                // means we're at the root level, which has no entity
                if (string.IsNullOrWhiteSpace(cubbyPath))
                {
                    return null;
                }
                //Logger.Debug($"Finding entity for cubby path {cubbyPath}");

                var entity = DataRoot.GetEntityFromCubbyPath(cubbyPath);

                return entity;
            }
            catch
            {
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
                throw;
            }
        }
        public static EntityUi FindEntityForCubby(Cubby cubby)
        {
            if (cubby == null)
            {
                return null;
            }

            try
            {
                var cubbyPath = cubby.GetPath();
                // means we're at the root level, which has no entity
                if (string.IsNullOrWhiteSpace(cubbyPath))
                {
                    return null;
                }
                //Logger.Debug($"Finding entity for cubby path {cubbyPath}");

                var entity = DataRoot.GetEntityFromCubbyPath(cubbyPath);

                return entity;
            }
            catch
            {
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
                throw;
            }
        }

        #endregion

        #region Cloning Helpers
        //#if DEBUG
        /// <summary>
        /// For testing purposes, each command must return a 'for-testing' instance of itself.
        /// 
        /// The inheriting command can presume there is a test instance of MetrologyData all set up to be consumed.
        /// </summary>
        /// <typeparam name="TCommand"></typeparam>
        /// <returns></returns>
        protected abstract TCommand GetInstance<TCommand>() where TCommand : CommandItemDetailsBase;

        /// <summary>
        /// Factory method to product testable instances of each command available in unit tests.
        /// 
        /// The inheriting command sets up a testable instance of itself in the Getinstace() method
        /// </summary>
        /// <typeparam name="TCommand"></typeparam>
        /// <returns></returns>
        public static TCommand Factory<TCommand>() where TCommand : CommandItemDetailsBase, new()
        {
            var cmd = new TCommand();
            return cmd.GetInstance<TCommand>();
        }
        //#endif
        /// <summary>
        /// Creates a new object that is a copy of the current instance by using the same construction arguments
        /// </summary>
        /// <returns>
        /// A new object that is a copy of this instance.
        /// </returns>
        public T Clone<T>() where T : class, ICommandItemDetails
        {
            // create a new instance of this one, with the same args
            return Activator.CreateInstance(this.GetType(), ConstructorArguments.ToArray()) as T;
        }

        public CommandItemDetailsBase Clone()
        {
            return this.Clone<CommandItemDetailsBase>();
        }

        object ICloneable.Clone()
        {
            return Clone<ICommandItemDetails>();
        }

        #endregion
    }
}
