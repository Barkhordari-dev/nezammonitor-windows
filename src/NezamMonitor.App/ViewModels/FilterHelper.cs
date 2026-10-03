using NezamMonitor.Core;

namespace NezamMonitor.App.ViewModels;

/// <summary>
/// Reusable filter helper for Persian/Arabic tolerant filtering.
/// </summary>
public static class FilterHelper
{
    /// <summary>
    /// Normalize a value for comparison: unify Persian/Arabic chars, trim, collapse whitespace.
    /// </summary>
    public static string Normalize(string? value) => DigitNormalizer.Normalize(value);

    /// <summary>
    /// Check if a stored value matches a filter value using tolerant comparison.
    /// </summary>
    public static bool Matches(string? storedValue, string? filterValue)
    {
        if (string.IsNullOrWhiteSpace(filterValue) || filterValue == "همه")
            return true;
        var normalizedStored = Normalize(storedValue);
        var normalizedFilter = Normalize(filterValue);
        return normalizedStored.Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extract distinct values from a collection, sorted and with "همه" prepended.
    /// </summary>
    public static List<string> ExtractDistinctValues<T>(IEnumerable<T> items, Func<T, string?> selector)
    {
        var values = items
            .Select(selector)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .Distinct()
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList();
        values.Insert(0, "همه");
        return values;
    }

    /// <summary>
    /// Extract distinct values with counts.
    /// </summary>
    public static List<FilterOption> ExtractDistinctWithCounts<T>(IEnumerable<T> items, Func<T, string?> selector)
    {
        var grouped = items
            .Where(i => !string.IsNullOrWhiteSpace(selector(i)))
            .GroupBy(i => selector(i)!)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new FilterOption { Value = g.Key, Count = g.Count() })
            .ToList();
        grouped.Insert(0, new FilterOption { Value = "همه", Count = items.Count() });
        return grouped;
    }

    /// <summary>
    /// Filter a list by a single value using tolerant matching.
    /// </summary>
    public static IEnumerable<T> ApplyFilter<T>(IEnumerable<T> source, Func<T, string?> selector, string? filterValue)
    {
        if (string.IsNullOrWhiteSpace(filterValue) || filterValue == "همه")
            return source;
        return source.Where(item => Matches(selector(item), filterValue));
    }

    /// <summary>
    /// Apply multiple filters with AND logic.
    /// </summary>
    public static IEnumerable<T> ApplyFilters<T>(IEnumerable<T> source, params (Func<T, string?> selector, string? value)[] filters)
    {
        var result = source;
        foreach (var (selector, value) in filters)
        {
            if (!string.IsNullOrWhiteSpace(value) && value != "همه")
                result = result.Where(item => Matches(selector(item), value));
        }
        return result;
    }
}

/// <summary>
/// Represents a filter option with value and count.
/// </summary>
public class FilterOption
{
    public string Value { get; set; } = "";
    public int Count { get; set; }
    public string Display => Count > 0 ? $"{Value} ({Count})" : Value;
}
