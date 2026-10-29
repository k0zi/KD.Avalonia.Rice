namespace KD.Avalonia.Rice.Controls.Diffs;

public enum DiffOperation
{
    Equal,
    Delete,
    Insert,
}

/// <summary>A run of one operation: <see cref="OldLength"/> items of the old sequence from <see cref="OldStart"/>, and so on.</summary>
public readonly record struct DiffSpan(DiffOperation Operation, int OldStart, int OldLength, int NewStart, int NewLength);

/// <summary>
/// Myers' O(ND) difference algorithm (the one of <c>git diff</c>): the shortest edit script between two sequences.
/// The common start and end are cut off first; when more than <c>maxEdits</c> edits would be needed, the rest is reported
/// as one replacement instead of searching on (which would cost quadratic memory).
/// </summary>
public static class DiffAlgorithm
{
    public static IReadOnlyList<DiffSpan> Diff<T>(IReadOnlyList<T> oldItems, IReadOnlyList<T> newItems,
        IEqualityComparer<T>? comparer = null, int maxEdits = 2000)
    {
        comparer ??= EqualityComparer<T>.Default;
        var n = oldItems.Count;
        var m = newItems.Count;
        var prefix = 0;
        while (prefix < n && prefix < m && comparer.Equals(oldItems[prefix], newItems[prefix]))
            prefix++;
        var suffix = 0;
        while (suffix < n - prefix && suffix < m - prefix && comparer.Equals(oldItems[n - 1 - suffix], newItems[m - 1 - suffix]))
            suffix++;

        var ops = new List<DiffOperation>(n + m);
        ops.AddRange(Enumerable.Repeat(DiffOperation.Equal, prefix));
        var middle = Middle(oldItems, newItems, prefix, n - prefix - suffix, m - prefix - suffix, comparer, maxEdits);
        ops.AddRange(middle);
        ops.AddRange(Enumerable.Repeat(DiffOperation.Equal, suffix));
        return ToSpans(ops);
    }

    private static List<DiffOperation> Middle<T>(IReadOnlyList<T> a, IReadOnlyList<T> b, int start, int n, int m,
        IEqualityComparer<T> comparer, int maxEdits)
    {
        if (n == 0 || m == 0)
            return [.. Enumerable.Repeat(DiffOperation.Delete, n), .. Enumerable.Repeat(DiffOperation.Insert, m)];

        var max = n + m;
        var offset = max + 1;
        var v = new int[2 * max + 3];
        // trace[d] is the furthest x of every diagonal k (index k + d) before step d.
        var trace = new List<int[]>();
        for (var d = 0; d <= max; d++)
        {
            if (d > maxEdits)
                return [.. Enumerable.Repeat(DiffOperation.Delete, n), .. Enumerable.Repeat(DiffOperation.Insert, m)];
            var snapshot = new int[2 * d + 1];
            Array.Copy(v, offset - d, snapshot, 0, snapshot.Length);
            trace.Add(snapshot);
            for (var k = -d; k <= d; k += 2)
            {
                var x = k == -d || k != d && v[offset + k - 1] < v[offset + k + 1] ? v[offset + k + 1] : v[offset + k - 1] + 1;
                var y = x - k;
                while (x < n && y < m && comparer.Equals(a[start + x], b[start + y]))
                {
                    x++;
                    y++;
                }
                v[offset + k] = x;
                if (x >= n && y >= m)
                    return Backtrack(trace, n, m);
            }
        }
        return [];
    }

    private static List<DiffOperation> Backtrack(List<int[]> trace, int n, int m)
    {
        var ops = new List<DiffOperation>(n + m);
        int x = n, y = m;
        for (var d = trace.Count - 1; d >= 0; d--)
        {
            int prevX, prevY;
            if (d == 0)
            {
                prevX = 0;
                prevY = 0;
            }
            else
            {
                var v = trace[d];
                var k = x - y;
                var prevK = k == -d || k != d && v[k - 1 + d] < v[k + 1 + d] ? k + 1 : k - 1;
                prevX = v[prevK + d];
                prevY = prevX - prevK;
            }
            while (x > prevX && y > prevY)
            {
                ops.Add(DiffOperation.Equal);
                x--;
                y--;
            }
            if (d > 0)
                ops.Add(x == prevX ? DiffOperation.Insert : DiffOperation.Delete);
            x = prevX;
            y = prevY;
        }
        ops.Reverse();
        return ops;
    }

    private static List<DiffSpan> ToSpans(List<DiffOperation> ops)
    {
        var spans = new List<DiffSpan>();
        int oldIndex = 0, newIndex = 0, i = 0;
        while (i < ops.Count)
        {
            var op = ops[i];
            var count = 0;
            while (i < ops.Count && ops[i] == op)
            {
                count++;
                i++;
            }
            spans.Add(op switch
            {
                DiffOperation.Equal => new DiffSpan(op, oldIndex, count, newIndex, count),
                DiffOperation.Delete => new DiffSpan(op, oldIndex, count, newIndex, 0),
                _ => new DiffSpan(op, oldIndex, 0, newIndex, count),
            });
            if (op != DiffOperation.Insert)
                oldIndex += count;
            if (op != DiffOperation.Delete)
                newIndex += count;
        }
        return spans;
    }
}
