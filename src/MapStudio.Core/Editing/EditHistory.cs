namespace MapStudio.Core.Editing;

/// <summary>
/// A reversible edit to the in-memory map document. Implementations should make
/// Undo and Redo deterministic and should not silently swallow persistence errors.
/// </summary>
public interface IEditOperation
{
    string Description { get; }

    void Undo();

    void Redo();
}

/// <summary>
/// Reversible edit history shared by map editing tools. Operations are only added
/// after their initial Redo succeeds, so a failed edit never enters the history.
/// </summary>
public sealed class EditHistory
{
    private readonly object _sync = new();
    private readonly Stack<IEditOperation> _undo = new();
    private readonly Stack<IEditOperation> _redo = new();
    private readonly int _capacity;

    public EditHistory(int capacity = 500)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "History capacity must be at least one.");
        }

        _capacity = capacity;
    }

    public event EventHandler? Changed;

    public bool CanUndo
    {
        get { lock (_sync) return _undo.Count > 0; }
    }

    public bool CanRedo
    {
        get { lock (_sync) return _redo.Count > 0; }
    }

    public int UndoCount
    {
        get { lock (_sync) return _undo.Count; }
    }

    public int RedoCount
    {
        get { lock (_sync) return _redo.Count; }
    }

    public string? NextUndoDescription
    {
        get { lock (_sync) return _undo.TryPeek(out var operation) ? operation.Description : null; }
    }

    public string? NextRedoDescription
    {
        get { lock (_sync) return _redo.TryPeek(out var operation) ? operation.Description : null; }
    }

    /// <summary>Executes an edit and records it only if the edit succeeds.</summary>
    public void Execute(IEditOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        lock (_sync)
        {
            operation.Redo();
            _undo.Push(operation);
            _redo.Clear();

            // Stack enumerates newest-first. Keep the newest operations when trimming.
            if (_undo.Count > _capacity)
            {
                var newestFirst = _undo.Take(_capacity).Reverse().ToArray();
                _undo.Clear();
                foreach (var item in newestFirst)
                {
                    _undo.Push(item);
                }
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool Undo()
    {
        lock (_sync)
        {
            if (!_undo.TryPeek(out var operation))
            {
                return false;
            }

            // Do not move the operation between stacks until Undo has succeeded.
            operation.Undo();
            _undo.Pop();
            _redo.Push(operation);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool Redo()
    {
        lock (_sync)
        {
            if (!_redo.TryPeek(out var operation))
            {
                return false;
            }

            // Keep the redo entry if the operation fails.
            operation.Redo();
            _redo.Pop();
            _undo.Push(operation);

            if (_undo.Count > _capacity)
            {
                var newestFirst = _undo.Take(_capacity).Reverse().ToArray();
                _undo.Clear();
                foreach (var item in newestFirst)
                {
                    _undo.Push(item);
                }
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Starts a new history for a newly opened map/document.</summary>
    public void Clear()
    {
        lock (_sync)
        {
            _undo.Clear();
            _redo.Clear();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>A small adapter for edits represented by undo/redo delegates.</summary>
public sealed class DelegateEditOperation : IEditOperation
{
    private readonly Action _undo;
    private readonly Action _redo;

    public DelegateEditOperation(string description, Action undo, Action redo)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("An edit description is required.", nameof(description));
        }

        Description = description;
        _undo = undo ?? throw new ArgumentNullException(nameof(undo));
        _redo = redo ?? throw new ArgumentNullException(nameof(redo));
    }

    public string Description { get; }

    public void Undo() => _undo();

    public void Redo() => _redo();
}
