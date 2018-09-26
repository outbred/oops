using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using DURF;
using DURF.Collections;
using UI.WpfCore.Services;

namespace URF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        #region Overrides of Application

        /// <inheritdoc />
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // current dispatcher is the UI thread for the App
            WpfDispatcher.Initialize(Dispatcher.CurrentDispatcher);
            PlatformImplementation.Dispatcher = new WpfDispatcher();
            PlatformImplementation.ToRaiseCanExecuteChanged = CommandManager.InvalidateRequerySuggested;
            PlatformImplementation.OnCanExecuteSubscribed = h => CommandManager.RequerySuggested += h;
            PlatformImplementation.OnCanExecuteUnsubscribed = h => CommandManager.RequerySuggested -= h;
        }

        #endregion
    }
}
