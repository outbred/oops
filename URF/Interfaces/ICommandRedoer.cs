using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace URF.Interfaces {
    public interface ICommandRedoer : INotifyPropertyChanged, IDisposable
    {
        void AddRedo(ICommandBase cmd);

        /// <summary>
        /// To bind to - pops newest command off stack and calls Redo() on it
        /// </summary>
        IAsyncCommand RedoLast { get; }

        /// <summary>
        /// to bind to - returns true if there are any commands that have been undone that can be redone
        /// </summary>
        bool CanRedo { get; }

        /// <summary>
        /// When a command's Do() is called, redoes are cleared
        /// </summary>
        void Clear();

        string MenuName { get; }

        /// <summary>
        /// Bindable list of all current redoes - 0th element is the most recent item on the stack
        /// 
        /// Could be an ICollectionChanged collection since this is low frequency, but for consistency, kept as a List<>.
        /// 
        /// See ICommandUndoer.Undos for why that one is a List<> as well.
        /// </summary>
        ReadOnlyCollection<UndoRedoStackItem> Redos { get; }

        /// <summary>
        /// Redo to a specific item (inclusive)
        /// </summary>
        IAsyncCommand<UndoRedoStackItem> Redo { get; }
    }
}