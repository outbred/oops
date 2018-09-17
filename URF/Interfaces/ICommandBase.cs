using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace URF.Interfaces
{
    public interface ICommandBase : IDisposable
    {
        /// <summary>
        /// Command execution, async
        /// </summary>
        /// <returns></returns>
        Task Do();

        /// <summary>
        /// Complimentary logic for Run() to undo the operation
        /// </summary>
        /// <returns></returns>
        Task Undo();

        /// <summary>
        /// Generally, just re-invoke Run(), but allow flexibility in commands to do something different
        /// </summary>
        /// <returns></returns>
        Task Redo();

        Task Run();

        /// <summary>
        /// When you Do, UnDo, or ReDo, this is set true if no exception.  False if exception was caught.
        /// Command can set this to false.
        /// </summary>
        bool LastResult { get; set; }

        /// <summary>
        /// Exception (if any) from last Do, UnDo, or ReDo
        /// </summary>
        Exception LastException { get; set; }

        /// <summary>
        /// Some commands are not undo-able
        /// </summary>
        bool CanUndo { get; }

        /// <summary>
        /// Serves as an optional command runner to run the command on if you do not want to run on the Facade's default command runner.
        /// </summary>
        ICommandRunner Runner { get; set; }

        /// <summary>
        /// Recorded right before do, undo, or redo begins
        /// </summary>
        DateTime LastStart { get; set; }

        /// <summary>
        /// Recorded right after do, undo, or redo completes
        /// </summary>
        DateTime LastEnd { get; set; }

        /// <summary>
        /// User-readable description of the command's Do() function (e.g. Rename Circle1 to Circle2), if a command
        /// If not an ICommandBase, a description for the context menu
        /// </summary>
        string Description { get; set; }

        /// <summary>
        /// Set to false if this command has its own progress indicator
        /// </summary>
        bool AutomaticProgressIndicator { get; }

        /// <summary>
        /// Do not wait for a timer to expire to show the progress dialog - show it immediately
        /// </summary>
        bool ImmediatelyShowAutomaticProgress { get; }

        /// <summary>
        /// Should be checked by the command during Do, Undo, and Redo to see if the current operation has been canceled
        /// </summary>
        CancellationTokenSource CancelToken { get; set; }

        /// <summary>
        /// True shows the 'Cancel' button in the ui for a long running operation
        /// If true, the command is responsable for checking the CancelToken for any operation (Do, Undo, and/or Redo)
        /// If Canceltoken.RequestCancellation, then the command should quick out and throw the OperationCanceledException
        /// </summary>
        bool IsCancelable { get; }

        /// <summary>
        /// Since the building or the User can cancel a command, need to know who's at fault
        /// </summary>
        bool UserCanceled { get; set; }
    }

    // SA-5894 implement INotifyPropertyChanged to remove binding mem leak - Brent (28 Feb 2018)
}
