namespace MapStudio.Core.Generation;

internal static class MapStudioOsmRingAssembler
{
    internal static bool TryAssemble(
        IReadOnlyList<long> wayIds, IReadOnlyDictionary<long, long[]> ways,
        out IReadOnlyList<IReadOnlyList<long>> rings)
    {
        rings = [];
        var pending = wayIds.Select(id => ways[id]).Where(nodes => nodes.Length >= 2)
            .Select(nodes => nodes.ToList()).ToList();
        var assembled = new List<IReadOnlyList<long>>();
        while (pending.Count > 0)
        {
            var chain = pending[0];
            pending.RemoveAt(0);
            while (chain[0] != chain[^1])
            {
                var tail = chain[^1];
                var index = pending.FindIndex(nodes => nodes[0] == tail || nodes[^1] == tail);
                if (index < 0) return false;
                var next = pending[index];
                pending.RemoveAt(index);
                if (next[^1] == tail) next.Reverse();
                chain.AddRange(next.Skip(1));
            }
            if (chain.Count < 4) return false;
            chain.RemoveAt(chain.Count - 1);
            assembled.Add(chain);
        }
        rings = assembled;
        return rings.Count > 0;
    }
}
