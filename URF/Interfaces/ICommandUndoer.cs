using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace URF.Interfaces {
    public interface ICommandUndoer : INotifyPropertyChanged, IDisposable
    {
        void AddUndo(ICommandBase cmd);

        /// <summary>
        /// Performs Undo on a collection of commands that are already in the Undo stack
        /// </summary>
        /// <param name="cmds"></param>
        /// <returns></returns>
        Task UndoCommands(IEnumerable<ICommandBase> cmds);

        /// <summary>
        /// To bind to - pops newest command off stack and calls Undo() on it - bound to UI
        /// </summary>
        IAsyncCommand UndoLast { get; }

        /// <summary>
        /// Undo to a specific item (inclusive) - bound to UI
        /// </summary>
        IAsyncCommand<object> Undo { get; }

        /// <summary>
        /// To bind to - if there are any items to undo, this returns true
        /// </summary>
        bool CanUndo { get; }

        string MenuName { get; }

        void Clear();

        /// <summary>
        /// To bind to - for clearing the stack
        /// </summary>
        ICommand ClearAll { get; }

        bool TrackChanges { get; set; }

        /// <summary>
        /// Bindable list of all current undoes - 0th element is the most recent item on the stack
        /// 
        /// Does NOT fire collectionchanged b/c this collection can be hit at a high-frequency and we don't need the see
        /// the contents of the collection until the user clicks the down arrow for the undo list.
        /// 
        /// Therefore, the view is responsible for refreshing its binding appropriately.
        /// </summary>
        IList<UndoRedoStackItem> Undos { get; }

        /// <summary>
        /// When true, PropertyChanged events for the Undos collection should be fired as the collection is 
        /// changed
        /// </summary>
        bool AutoUpdate { get; set; }

        /// <summary>
        /// When true, cmds should fire canexecutechanged events to update the UI
        /// </summary>
        bool UndoVisible { get; set; }

    }
}