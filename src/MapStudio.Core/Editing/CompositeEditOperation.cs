namespace MapStudio.Core.Editing;

/// <summary>
/// Groups a completed gesture (for example, a drag, rotation, or terrain brush stroke)
/// into one undo step. Children are executed in the supplied order and undone in reverse.
/// </summary>
public sealed class CompositeEditOperation : IEditOperation
{
    private readonly IReadOnlyList<IEditOperation> _operations;

    public CompositeEditOperation(string description, IEnumerable<IEditOperation> operations)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("An edit description is required.", nameof(description));
        }

        ArgumentNullException.ThrowIfNull(operations);
        _operations = operations.ToArray();
        if (_operations.Any(operation => operation is null))
        {
            throw new ArgumentException("An edit group cannot contain null operations.", nameof(operations));
        }

        Description = description;
    }

    public string Description { get; }

    public void Redo()
    {
        var completed = 0;
        try
        {
            for (; completed < _operations.Count; completed++)
            {
                _operations[completed].Redo();
            }
        }
        catch (Exception applyError)
        {
            // Restore the pre-gesture state if a later part of the gesture fails.
            var rollbackErrors = new List<Exception>();
            for (var index = completed - 1; index >= 0; index--)
            {
                try
                {
                    _operations[index].Undo();
                }
                catch (Exception rollbackError)
                {
                    rollbackErrors.Add(rollbackError);
                }
            }

            if (rollbackErrors.Count > 0)
            {
                rollbackErrors.Insert(0, applyError);
                throw new AggregateException(
                    "The edit group failed and one or more completed edits could not be rolled back.",
                    rollbackErrors);
            }

            throw;
        }
    }

    public void Undo()
    {
        var undone = new List<IEditOperation>();
        try
        {
            for (var index = _operations.Count - 1; index >= 0; index--)
            {
                _operations[index].Undo();
                undone.Add(_operations[index]);
            }
        }
        catch (Exception undoError)
        {
            // Reapply edits already undone so a failed undo does not leave a partial gesture.
            var rollbackErrors = new List<Exception>();
            for (var index = undone.Count - 1; index >= 0; index--)
            {
                try
                {
                    undone[index].Redo();
                }
                catch (Exception rollbackError)
                {
                    rollbackErrors.Add(rollbackError);
                }
            }

            if (rollbackErrors.Count > 0)
            {
                rollbackErrors.Insert(0, undoError);
                throw new AggregateException(
                    "The edit group could not be undone atomically.",
                    rollbackErrors);
            }

            throw;
        }
    }
}
