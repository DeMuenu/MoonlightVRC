#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

// Visualization only: both neighbor searches and output are bounded, including coincident probes.
internal sealed class ProbeSceneConnections
{
    internal const int MaxComparisons = 16384;
    internal const int MaxLines = 2048;
    internal const int MaxSources = 128;
    private readonly Dictionary<Vector3Int, List<int>> cells = new Dictionary<Vector3Int, List<int>>();
    private Vector3[] positions;
    private float distance;
    internal int ComparisonCount { get; private set; }
    internal readonly List<Vector3> Lines = new List<Vector3>(MaxLines * 2);

    internal void Invalidate() => positions = null;

    internal void Build(Vector3[] points, IReadOnlyList<int> selection, float radius)
    {
        Lines.Clear();
        ComparisonCount = 0;
        if (points.Length < 2 || radius <= 0f) return;
        if (!ReferenceEquals(positions, points) || distance != radius)
        {
            positions = points;
            distance = radius;
            cells.Clear();
            for (int i = 0; i < points.Length; i++)
            {
                Vector3Int cell = Vector3Int.FloorToInt(points[i] / radius);
                if (!cells.TryGetValue(cell, out List<int> bucket))
                {
                    bucket = new List<int>();
                    cells.Add(cell, bucket);
                }
                bucket.Add(i);
            }
        }

        int sourceCount = Mathf.Min(MaxSources, selection.Count > 0 ? selection.Count : points.Length);
        float radiusSquared = radius * radius;
        for (int source = 0; source < sourceCount; source++)
        {
            int index = selection.Count > 0 ? selection[source] : source * points.Length / sourceCount;
            if (index < 0 || index >= points.Length) continue;
            Vector3 position = points[index];
            Vector3Int cell = Vector3Int.FloorToInt(position / radius);
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++)
            {
                if (!cells.TryGetValue(cell + new Vector3Int(x, y, z), out List<int> bucket)) continue;
                foreach (int neighbor in bucket)
                {
                    if (ComparisonCount >= MaxComparisons || Lines.Count >= MaxLines * 2) return;
                    ComparisonCount++;
                    if (neighbor == index || (points[neighbor] - position).sqrMagnitude > radiusSquared) continue;
                    Lines.Add(position);
                    Lines.Add(points[neighbor]);
                }
            }
        }
    }
}
#endif
