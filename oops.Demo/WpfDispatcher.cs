//#define AUDIT_Dispatcher


using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DURF;
using DURF.Interfaces;

namespace UI.WpfCore.Services
{
    /// <summary>
    /// Helper class for dispatcher operations on the UI thread.
    /// </summary>
    public class WpfDispatcher : IDispatcher
    {
        #region Fields/Props

        private static readonly DispatcherOperationCallback ExitFrameCallback = ExitFrame;

        /// <summary>
        /// Gets a reference to the UI thread's dispatcher, after the
        /// <see cref="Initialize" /> method has been called on the UI thread.
        /// </summary>
        private static Dispatcher UiDispatcher { get; set; }

        #endregion

        private static Object ExitFrame(Object state)
        {
            // Exit the nested message loop.
            if (state is DispatcherFrame frame)
                frame.Continue = false;

            return null;
        }

        /// <summary>
        /// This method should be called once on the UI thread to ensure that
        /// the <see cref="UiDispatcher" /> property is initialized.
        /// <para>In a Silverlight application, call this method in the
        /// Application_Startup event handler, after the MainPage is constructed.</para>
        /// <para>In WPF, call this method on the static App() constructor.</para>
        /// </summary>
        [DebuggerStepThrough]
        public static void Initialize(Dispatcher dispatcher = null)
        {
            if (dispatcher == null && UiDispatcher != null)
                return;

            UiDispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
        }

        #region Implementation of IDispatcher

        [DebuggerStepThrough]
        public bool CheckAccess() => UiDispatcher?.CheckAccess() ?? true;

        public bool ShuttingDown => UiDispatcher?.HasShutdownStarted ?? false;

        [DebuggerStepThrough]
        public Task BeginInvoke(Action action)
        {
            if (action == null || ShuttingDown)
                return Task.Run(() => { });

            if (CheckAccess())
                action();
            else
            {
                // CheckAccess() can take a while, so last second check - Brent (06 Sep 2017)
                if (ShuttingDown)
                    return Task.Run(() => { });

                var result = UiDispatcher.BeginInvoke(action);
                result.Task.ConfigureAwait(false);
                return result.Task;
            }

            return Task.Run(() => { });
        }

        [DebuggerStepThrough]
        public void Invoke(Action action)
        {
            if (action == null)
                return;

            if (CheckAccess())
                action();
            else
                UiDispatcher.Invoke(action, DispatcherPriority.DataBind);
        }

        [DebuggerStepThrough]
        public Task BeginInvoke(Func<Task> action)
        {
            if (action == null)
                return Task.Run(() => { });

            if (CheckAccess())
                return action();
            else
            {
                // Anything passed into BeginInvoke is not passed up the async chain, so we have to force with a ManualResetEvent - Brent (09 Mar 2017)
                ManualResetEvent allDone = new ManualResetEvent(false);
                UiDispatcher.BeginInvoke(new Action(async () =>
                {
                    await action();
                    allDone.Set();
                }));
                allDone.WaitOne();
            }

            return Task.Run(() => { });
        }

        [DebuggerStepThrough]
        public Task Invoke(Func<Task> action)
        {
            if (CheckAccess())
                return action();
            else
                return UiDispatcher.Invoke(action, DispatcherPriority.DataBind);
        }

        [DebuggerStepThrough]
        public void Wait()
        {
            // see https://stackoverflow.com/questions/35172613/invalidoperationexception-dispatcher-processing-has-been-suspended-but-message
            // If we're on the UI thread already, there is little point to this call b/c the UI pump has finished processing previous messages
            // and is ready to process the current one.
            // Furthermore, if the current thread is the UI thread and then tries to re-enter the UI thread, you can get an InvalidOperationException, which is thrown
            // b/c of re-entrancy checks in the Dispatcher.
            if (UiDispatcher == null || UiDispatcher.CheckAccess())
            {
                return;
            }

            try
            {
                // Create new nested message pump.
                DispatcherFrame nestedFrame = new DispatcherFrame();

                // Dispatch a callback to the current message queue, when getting called,
                // this callback will end the nested message loop.
                // The priority of this callback should be lower than that of event message you want to process.
                DispatcherOperation exitOperation = UiDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, ExitFrameCallback, nestedFrame);

                // pump the nested message loop, the nested message loop will immediately
                // process the messages left inside the message queue.
                Dispatcher.PushFrame(nestedFrame);

                // If the "exitFrame" callback is not finished, abort it.
                if (exitOperation.Status != DispatcherOperationStatus.Completed)
                {
                    exitOperation.Abort();
                }
            }
            // threads can be out of sync depending on usage, but don't let that kill the app
            catch { }
        }

        #endregion
    }
}
