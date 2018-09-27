using System;
using System.Collections.Generic;
using System.Text;
using DURF.Interfaces;

namespace DURF
{
    public static class PlatformImplementation
    {
        /// <summary>
        /// Platform-specific implementation of the Ui thread dispatcher
        /// </summary>
        public static IDispatcher Dispatcher { get; set; }
    }
}
