using System;
using System.Collections.Generic;
using System.Text;
using DURF.Interfaces;

namespace DURF
{
    public static class DispatcherHolder
    {
        public static IDispatcher Dispatcher { get; set; }
    }
}
