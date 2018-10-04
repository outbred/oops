using System;
using System.Collections.Generic;
using System.Text;
using oops.Interfaces;

namespace oops
{
    public static class Globals
    {
        /// <summary>
        /// Platform-specific implementation of the Ui thread dispatcher
        /// </summary>
        public static IDispatcher Dispatcher { get; set; }

        /// <summary>
        /// Set to true to have every individual change, everywhere, pushed to the undo stack individually.
        ///
        /// If there is a current, global Accumulator, that will be used.  Else, if this is true, each change is pushed in its own Accumulator.
        /// </summary>
        public static bool ScopeEachChange { get; set; }
    }
}
