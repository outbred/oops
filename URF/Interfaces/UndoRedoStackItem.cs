using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace URF.Interfaces {
    public class UndoRedoStackItem : IDisposable, INotifyPropertyChanged
    {
        public UndoRedoStackItem(bool undo, ICommandBase cmd)
        {
            InUndo = undo;
            Command = cmd;
        }

        public ICommandBase Command { get; private set; }

        public bool InUndo { get; }

        public void Dispose()
        {
            Command?.Dispose();
            Command = null;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}