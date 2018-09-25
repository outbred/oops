using System;
using System.Threading;

namespace DURF.Interfaces {
    public interface ICanceller
    {
        /// <summary>
        /// Bindable command to cancel the current command
        /// </summary>
        void Cancel();

        /// <summary>
        /// The current token that can be canceled. Reset after every commmand execution (Do, Undo, or Redo)
        /// </summary>
        CancellationTokenSource CurrentToken { get; }

        /// <summary>
        /// Provides an object that when disposed, cancels and resets the CancellationToken/Source
        /// </summary>
        /// <returns></returns>
        IDisposable CancelOnDispose();
    }
}