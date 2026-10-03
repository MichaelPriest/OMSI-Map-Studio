namespace MapStudio.Renderer.Picking;

public sealed class PickingRegistry<T>
    where T : class
{
    private readonly Dictionary<int, T> _items =
        new();

    private readonly Queue<int> _freeIds =
        new();

    private int _nextId = 1;

    public int Count => _items.Count;

    public PickingId Register(
        PickingKind kind,
        T item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (kind == PickingKind.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                "A selectable item needs a concrete picking kind.");
        }

        var id =
            _freeIds.Count > 0
                ? _freeIds.Dequeue()
                : _nextId++;

        _items.Add(id, item);

        return new PickingId(
            kind,
            id);
    }

    public bool TryResolve(
        PickingId id,
        out T? item)
    {
        if (id.IsNone)
        {
            item = null;
            return false;
        }

        return _items.TryGetValue(
            id.Value,
            out item);
    }

    public bool Unregister(
        PickingId id)
    {
        if (
            id.IsNone ||
            !_items.Remove(id.Value)
        )
        {
            return false;
        }

        _freeIds.Enqueue(id.Value);
        return true;
    }

    public void Clear()
    {
        _items.Clear();
        _freeIds.Clear();
        _nextId = 1;
    }
}
