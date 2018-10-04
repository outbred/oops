using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using oops;
using oops.Collections;
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
            Globals.Dispatcher = new WpfDispatcher();
        }

        #endregion
    }
}
