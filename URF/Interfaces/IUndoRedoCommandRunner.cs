namespace URF.Interfaces {
    public interface IUndoRedoCommandRunner : ICommandRunner
    {
        ICommandUndoer Undoer { get; }

        ICommandRedoer Redoer { get; }
    }
}