using MapStudio.Core.Editing;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class EditHistoryTests
{
    [Fact]
    public void ExecuteUndoRedo_RestoresTheValue()
    {
        var value = 0;
        var history = new EditHistory();

        history.Execute(new DelegateEditOperation(
            "Move object",
            undo: () => value = 0,
            redo: () => value = 12));

        Assert.Equal(12, value);
        Assert.True(history.CanUndo);
        Assert.Equal("Move object", history.NextUndoDescription);

        Assert.True(history.Undo());
        Assert.Equal(0, value);
        Assert.True(history.CanRedo);

        Assert.True(history.Redo());
        Assert.Equal(12, value);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Execute_WhenRedoThrows_DoesNotRecordOperation()
    {
        var history = new EditHistory();
        var operation = new DelegateEditOperation(
            "Invalid edit",
            undo: () => { },
            redo: () => throw new InvalidOperationException("Cannot apply edit."));

        Assert.Throws<InvalidOperationException>(() => history.Execute(operation));
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Equal(0, history.UndoCount);
    }

    [Fact]
    public void Undo_WhenOperationThrows_LeavesHistoryUnchanged()
    {
        var history = new EditHistory();
        history.Execute(new DelegateEditOperation(
            "Edit",
            undo: () => throw new InvalidOperationException("Cannot undo."),
            redo: () => { }));

        Assert.Throws<InvalidOperationException>(() => history.Undo());
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Equal("Edit", history.NextUndoDescription);
    }

    [Fact]
    public void ExecutingNewEditAfterUndo_ClearsRedoBranch()
    {
        var value = 0;
        var history = new EditHistory();

        history.Execute(new DelegateEditOperation("First", () => value = 0, () => value = 1));
        history.Undo();
        history.Execute(new DelegateEditOperation("Second", () => value = 0, () => value = 2));

        Assert.Equal(2, value);
        Assert.False(history.CanRedo);
        Assert.Equal("Second", history.NextUndoDescription);
    }

    [Fact]
    public void Capacity_KeepsOnlyTheMostRecentOperations()
    {
        var value = 0;
        var history = new EditHistory(capacity: 2);

        for (var next = 1; next <= 3; next++)
        {
            var captured = next;
            var previous = next - 1;
            history.Execute(new DelegateEditOperation(
                $"Set {captured}",
                undo: () => value = previous,
                redo: () => value = captured));
        }

        Assert.Equal(3, value);
        Assert.Equal(2, history.UndoCount);
        Assert.True(history.Undo());
        Assert.Equal(2, value);
        Assert.True(history.Undo());
        Assert.Equal(1, value);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void Clear_RemovesBothHistoryStacks()
    {
        var history = new EditHistory();
        history.Execute(new DelegateEditOperation("Edit", () => { }, () => { }));
        history.Undo();

        history.Clear();

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Equal(0, history.UndoCount);
        Assert.Equal(0, history.RedoCount);
    }
    [Fact]
    public void CompositeOperation_UndoesInReverseAndRedoesInOrder()
    {
        var value = 0;
        var history = new EditHistory();
        var group = new CompositeEditOperation("Move and rotate", new IEditOperation[]
        {
            new DelegateEditOperation("Move", () => value -= 10, () => value += 10),
            new DelegateEditOperation("Rotate", () => value -= 1, () => value += 1)
        });

        history.Execute(group);
        Assert.Equal(11, value);

        Assert.True(history.Undo());
        Assert.Equal(0, value);

        Assert.True(history.Redo());
        Assert.Equal(11, value);
        Assert.Equal("Move and rotate", history.NextUndoDescription);
    }

    [Fact]
    public void CompositeOperation_RollsBackCompletedEditsWhenRedoFails()
    {
        var value = 0;
        var group = new CompositeEditOperation("Partially invalid gesture", new IEditOperation[]
        {
            new DelegateEditOperation("First", () => value = 0, () => value = 1),
            new DelegateEditOperation("Second", () => value = 1, () => throw new InvalidOperationException("Second failed."))
        });

        Assert.Throws<InvalidOperationException>(() => group.Redo());
        Assert.Equal(0, value);
    }

}
