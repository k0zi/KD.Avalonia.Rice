namespace KD.Avalonia.Rice.Controls;

/// <summary>Position of the matched characters in a text and how good the match is (higher is better).</summary>
internal readonly record struct FuzzyMatch(int Score, int[] Positions);

/// <summary>
/// Fuzzy matching for the command palette: a pattern matches a text when the text contains its characters in order
/// (case insensitive). Contiguous matches beat scattered ones, and characters at the start of words count more, so
/// "tt" finds "Toggle theme" and ranks it above "Bottom toolbar".
/// </summary>
internal static class FuzzyMatcher
{
    private const int CharScore = 10;
    private const int ConsecutiveBonus = 15;
    private const int WordStartBonus = 20;
    private const int TextStartBonus = 15;
    private const int MaxGapPenalty = 10;
    private const int SubstringBonus = 100;
    private const int MidWordSubstringBonus = 20;

    public static FuzzyMatch? Match(string pattern, string text)
    {
        if (pattern.Length == 0)
            return new FuzzyMatch(0, []);
        if (pattern.Length > text.Length)
            return null;

        // Jumping to word starts gives the better match ("tt" → "Toggle Theme", not "TogglE"), but can skip characters
        // that later ones need; the plain left-to-right pass finds a match whenever there is one.
        var aligned = Align(pattern, text, preferWordStarts: true) ?? Align(pattern, text, preferWordStarts: false);
        if (aligned is null)
            return null;
        var best = new FuzzyMatch(Score(text, aligned), aligned);

        // A contiguous match counts most at a word start ("set" → "Settings"); inside a word ("tt" in "Bottom") it
        // must not beat the initials of the words.
        var index = text.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            var positions = Enumerable.Range(index, pattern.Length).ToArray();
            var bonus = IsWordStart(text, index) ? SubstringBonus : MidWordSubstringBonus;
            var substring = new FuzzyMatch(bonus + Score(text, positions), positions);
            if (substring.Score > best.Score)
                best = substring;
        }
        return best;
    }

    internal static bool IsWordStart(string text, int index)
    {
        if (index == 0)
            return true;
        var previous = text[index - 1];
        var current = text[index];
        return !char.IsLetterOrDigit(previous)
               || (char.IsLower(previous) && char.IsUpper(current))
               || (char.IsLetter(previous) && char.IsDigit(current));
    }

    private static int[]? Align(string pattern, string text, bool preferWordStarts)
    {
        var positions = new int[pattern.Length];
        var start = 0;
        for (var p = 0; p < pattern.Length; p++)
        {
            var found = -1;
            for (var i = start; i < text.Length; i++)
            {
                if (char.ToLowerInvariant(text[i]) != char.ToLowerInvariant(pattern[p]))
                    continue;
                if (found < 0)
                    found = i;
                // Continuing the previous match is best; else the next word start, else the first occurrence.
                if (!preferWordStarts || (p > 0 && i == positions[p - 1] + 1) || IsWordStart(text, i))
                {
                    found = i;
                    break;
                }
            }
            if (found < 0)
                return null;
            positions[p] = found;
            start = found + 1;
        }
        return positions;
    }

    private static int Score(string text, int[] positions)
    {
        var score = 0;
        for (var i = 0; i < positions.Length; i++)
        {
            var position = positions[i];
            score += CharScore;
            if (position == 0)
                score += TextStartBonus;
            if (IsWordStart(text, position))
                score += WordStartBonus;
            if (i > 0)
            {
                var gap = position - positions[i - 1] - 1;
                score += gap == 0 ? ConsecutiveBonus : -Math.Min(gap, MaxGapPenalty);
            }
        }
        // Shorter texts win among equal matches: "Theme" before "Theme settings" for "theme".
        return score - Math.Min(text.Length - positions.Length, MaxGapPenalty);
    }
}

/// <summary>Ranks the commands of the palette for a query.</summary>
internal static class CommandSearch
{
    // Words found only in the category, keywords or description count less than the title.
    private const int SecondaryDivisor = 3;
    private const int RecentBonus = 5;

    public static List<RiceCommandMatch> Search(IEnumerable<RiceCommandItem> commands, string? query,
        IReadOnlyList<RiceCommandItem> recent, int maxResults)
    {
        var all = commands.ToList();
        var words = (query ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // No query: the recently used commands first, then the rest in their order.
        if (words.Length == 0)
        {
            return recent.Where(all.Contains)
                .Concat(all.Where(c => !recent.Contains(c)))
                .Take(maxResults)
                .Select(c => new RiceCommandMatch(c, 0, []))
                .ToList();
        }

        var recentList = recent.ToList();
        var matches = new List<RiceCommandMatch>();
        foreach (var command in all)
        {
            if (Match(command, words) is not { } match)
                continue;
            var recentIndex = recentList.IndexOf(command);
            var bonus = recentIndex < 0 ? 0 : (recent.Count - recentIndex) * RecentBonus;
            matches.Add(match with { Score = match.Score + bonus });
        }
        // OrderByDescending is stable: equal scores keep the order of the commands.
        return matches.OrderByDescending(m => m.Score).Take(maxResults).ToList();
    }

    private static RiceCommandMatch? Match(RiceCommandItem command, string[] words)
    {
        var secondary = string.Join(' ', new[] { command.Category, command.Keywords, command.Description }.Where(s => !string.IsNullOrEmpty(s)));
        var score = 0;
        var highlights = new SortedSet<int>();
        foreach (var word in words)
        {
            if (FuzzyMatcher.Match(word, command.Title) is { } title)
            {
                score += title.Score;
                highlights.UnionWith(title.Positions);
            }
            else if (FuzzyMatcher.Match(word, secondary) is { } other)
            {
                score += other.Score / SecondaryDivisor;
            }
            else
            {
                return null;
            }
        }
        return new RiceCommandMatch(command, score, highlights.ToArray());
    }
}
