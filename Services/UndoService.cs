using System.Collections.Generic;
using System.Threading.Tasks;

namespace PrintVault3D.Services;

public interface IUndoableAction
{
    string Description { get; }
    Task ExecuteAsync();
    Task UndoAsync();
}

public interface IUndoService
{
    bool CanUndo { get; }
    bool CanRedo { get; }
    string UndoDescription { get; }
    string RedoDescription { get; }
    
    Task ExecuteActionAsync(IUndoableAction action);
    Task UndoAsync();
    Task RedoAsync();
    
    event System.EventHandler StateChanged;
}

public class UndoService : IUndoService
{
    private readonly Stack<IUndoableAction> _undoStack = new();
    private readonly Stack<IUndoableAction> _redoStack = new();
    
    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public string UndoDescription => CanUndo ? _undoStack.Peek().Description : string.Empty;
    public string RedoDescription => CanRedo ? _redoStack.Peek().Description : string.Empty;
    
    public event System.EventHandler? StateChanged;

    public async Task ExecuteActionAsync(IUndoableAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        
        try
        {
            await action.ExecuteAsync();
            _undoStack.Push(action);
            _redoStack.Clear();
            StateChanged?.Invoke(this, System.EventArgs.Empty);
        }
        catch
        {
            // If execution fails, don't add to undo stack
            throw;
        }
    }

    public async Task UndoAsync()
    {
        if (CanUndo)
        {
            var action = _undoStack.Pop();
            try
            {
                await action.UndoAsync();
                _redoStack.Push(action);
            }
            catch
            {
                // If undo fails, put the action back on the undo stack
                _undoStack.Push(action);
                throw;
            }
            StateChanged?.Invoke(this, System.EventArgs.Empty);
        }
    }

    public async Task RedoAsync()
    {
        if (CanRedo)
        {
            var action = _redoStack.Pop();
            try
            {
                await action.ExecuteAsync();
                _undoStack.Push(action);
            }
            catch
            {
                // If redo fails, put the action back on the redo stack
                _redoStack.Push(action);
                throw;
            }
            StateChanged?.Invoke(this, System.EventArgs.Empty);
        }
    }
}
