using System;
using System.Collections.Generic;
using System.Text;
using DURF.Interfaces;

namespace DURF
{
    public static class Globals
    {
        /// <summary>
        /// Platform-specific implementation of the Ui thread dispatcher
        /// </summary>
        public static IDispatcher Dispatcher { get; set; }

        /// <summary>
        /// Set to true to have every individual change, everywhere, pushed to the undo stack.
        ///
        /// If there is a current, global Accumulator, that will be used.  Else, if this is true, each change is pushed.
        /// </summary>
        public static bool ScopeEachChange { get; set; }
    }
}
