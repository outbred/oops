using System;
using System.Collections.Generic;
using System.Text;
using DURF.Interfaces;

namespace DURF
{
    public static class PlatformImplementation
    {
        public static IDispatcher Dispatcher { get; set; }


        /// <summary>
        /// Set per platform
        /// </summary>
        public static Action ToRaiseCanExecuteChanged { get; set; }

        /// <summary>
        /// Set per platform
        /// </summary>
        public static Action<EventHandler> OnCanExecuteSubscribed { get; set; }
        public static Action<EventHandler> OnCanExecuteUnsubscribed { get; set; }
    }
}
