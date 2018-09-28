using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Text;

namespace DURF
{
    /// <summary>
    /// indicates to the TrackableScopesManager where the TrackableScope should go and what state the system in in (just finished a redo?  Or is this a brand new scope for some user-driven process?)
    /// </summary>
    public enum ScopeState
    {
        Do, Undo, Redo
    }

    /// <summary>
    /// Tracks all changes within it's lifetime, aggregating them into one 'scope' or changeset.  When disposed, pushes those aggregated changes
    /// to the TrackableScopesManager's Undo stack.  A scope undoes/redoes all changes in order from any monitored ViewModel or collection in the system.
    ///
    /// Use like:
    /// using(new TrackableScope("Dumpster diving"))
    /// {
    ///    // put on throw away clothes
    ///    // open lid
    ///    // call family in case something happens and you die in the dumpster
    ///    // Find original Commodore64, put in bag. Score!
    ///    // Find unopened bag of mulch for your garden, toss out of the dumpster
    ///    // Find a very scared kitten, and...what do YOU do?
    ///    // Find a wedding ring and put in bag
    ///    // etc.
    /// }
    ///
    /// When disposed, TrackableScopesManager.Instance.Undables.First() is that collection of changes.  Simply use the Undo command in the  TrackableScopesManager
    /// to roll everything back, and push the scope to the Redo stack.  Using the Redo command in the TrackableScopesManager would then continue the game of ping pong
    /// and push the scope back to the Undo stack, and so on and so forth.
    /// </summary>
    [DebuggerDisplay("{Accumulator.Name}")]
    public class TrackableScope : IDisposable
    {
        private readonly ScopeState _state;
        public TrackableScope(string name, ScopeState state = ScopeState.Do)
        {
            _state = state;
            // if this isn't a 'sub' scope, then save it off to add to appropriate stack
            if(Accumulator.CurrentOrNew(out var acc, name))
                Accumulator = acc;
        }

        public Accumulator Accumulator { get; private set; } = null;


        /// <inheritdoc />
        public void Dispose() 
        {
            if (Accumulator != null)
            {
                TrackableScopesManager.Instance.Add(Accumulator, _state);
                Accumulator.Current.Close(Accumulator.Name);
                Accumulator = null;
            }
        }
    }
}
