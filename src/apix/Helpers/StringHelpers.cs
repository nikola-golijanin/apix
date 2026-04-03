namespace apix.Helpers;

internal static class StringHelpers
{
    /// <summary>
    /// Returns the closest match from candidates if its Levenshtein distance
    /// from input is ≤ maxDistance. Returns null when no close enough match is found.
    /// </summary>
    public static string? FindClosestMatch(string input, IEnumerable<string> candidates, int maxDistance = 2)
    {
        string? best = null;
        var bestDist = int.MaxValue;

        foreach (var candidate in candidates)
        {
            var dist = Levenshtein(input, candidate);
            if (dist >= bestDist) continue;
            bestDist = dist;
            best = candidate;
        }

        return bestDist <= maxDistance ? best : null;
    }

    private static int Levenshtein(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var d = new int[a.Length + 1, b.Length + 1];

        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        for (var j = 1; j <= b.Length; j++)
        {
            var cost = a[i - 1] == b[j - 1] ? 0 : 1;
            d[i, j] = Math.Min(
                Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                d[i - 1, j - 1] + cost);
        }

        return d[a.Length, b.Length];
    }
}
