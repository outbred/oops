using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace DURF.Interfaces 
{
    public interface ITrackableScopesManager : INotifyPropertyChanged
    {
        /// <summary>
        /// Readonly view into Undoables collection.  To add to the Undoables, use the Add() method
        /// </summary>
        IReadOnlyCollection<Accumulator> Undoables { get; }

        /// <summary>
        /// Readonly view into Redoables collection.  Only manipulated internally
        /// </summary>
        IReadOnlyCollection<Accumulator> Redoables { get; }

        /// <summary>
        /// Performs an Undo up to and including the scope supplied.  If none supplied, undoes the last scope
        /// </summary>
        AsyncCommand<Accumulator> Undo { get; }

        /// <summary>
        /// Performs a Redo up to and including the scope supplied.  If none supplied, redoes the last scope
        /// </summary>
        AsyncCommand<Accumulator> Redo { get; }

        void Add(Accumulator scope, ScopeState state);

        /// <summary>
        /// Clears all undo's and redo's
        /// </summary>
        void Reset();

        /// <summary>
        /// Undoes the scope on the top of the stack, pushing the new scope to the redo stack
        /// </summary>
        /// <returns></returns>
        Task UndoLast();

        /// <summary>
        /// Redoes the scope on the top of the stack, pushing the new scope to the undo stack
        /// </summary>
        /// <returns></returns>
        Task RedoLast();
    }
}