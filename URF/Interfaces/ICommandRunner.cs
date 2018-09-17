using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace URF.Interfaces {
    public interface ICommandRunner : IDisposable, INotifyPropertyChanged
    {
        /// <summary>
        /// Runs any command given it, taking care of the undo/redo cycle (if that is part of the runner's workflow)
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        Task Run(ICommandBase cmd);

        /// <summary>
        /// Returns false if the runner has been disposed or should not be used to run a command
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        bool CanRun(ICommandBase cmd);

        Task WaitForQueueToEmpty();

        ICanceller Canceller { get; }
    }
}