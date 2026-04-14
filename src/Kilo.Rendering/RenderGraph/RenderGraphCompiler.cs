namespace Kilo.Rendering.RenderGraph;

internal static class RenderGraphCompiler
{
    public static List<RenderPass> Compile(List<RenderPass> passes)
    {
        if (passes.Count == 0) return passes;

        // Map: resourceId -> last pass index that wrote it (in registration order)
        var lastWriter = new Dictionary<int, int>();
        // Map: passIndex -> set of resourceIds it reads
        var passReads = new Dictionary<int, HashSet<int>>();
        // Map: passIndex -> set of resourceIds it writes
        var passWrites = new Dictionary<int, HashSet<int>>();

        for (int i = 0; i < passes.Count; i++)
        {
            passWrites[i] = new HashSet<int>(passes[i].WrittenResources.Select(r => r.Id));
            passReads[i] = new HashSet<int>(passes[i].ReadResources.Select(r => r.Id));
        }

        // Build adjacency using registration order as tiebreaker.
        // Edge passA -> passB if:
        //   1. RAW: A writes X, B reads X (A before B in registration order)
        //   2. WAW: A writes X, B writes X, and A is the last writer before B
        var adj = new List<HashSet<int>>();
        var inDegree = new int[passes.Count];
        for (int i = 0; i < passes.Count; i++) adj.Add([]);

        // Track the last writer for each resource as we scan in registration order
        var resourceLastWriter = new Dictionary<int, int>();

        for (int i = 0; i < passes.Count; i++)
        {
            // RAW: this pass reads X — depend on the last writer of X
            foreach (var resourceId in passReads[i])
            {
                if (resourceLastWriter.TryGetValue(resourceId, out var writerIdx))
                {
                    if (writerIdx != i && adj[writerIdx].Add(i))
                        inDegree[i]++;
                }
            }

            // WAW: this pass writes X — depend on the previous writer of X
            foreach (var resourceId in passWrites[i])
            {
                if (resourceLastWriter.TryGetValue(resourceId, out var writerIdx))
                {
                    if (writerIdx != i && adj[writerIdx].Add(i))
                        inDegree[i]++;
                }
                resourceLastWriter[resourceId] = i;
            }
        }

        // Kahn's algorithm for topological sort
        var queue = new Queue<int>();
        for (int i = 0; i < passes.Count; i++)
            if (inDegree[i] == 0) queue.Enqueue(i);

        var result = new List<RenderPass>(passes.Count);
        while (queue.Count > 0)
        {
            var idx = queue.Dequeue();
            result.Add(passes[idx]);
            foreach (var next in adj[idx])
            {
                inDegree[next]--;
                if (inDegree[next] == 0) queue.Enqueue(next);
            }
        }

        if (result.Count != passes.Count)
            throw new InvalidOperationException("RenderGraph contains a cycle.");

        return result;
    }
}
