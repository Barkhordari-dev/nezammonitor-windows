using System.Text;

namespace NezamMonitor.Core;

/// <summary>
/// Text normalization and helper utilities.
/// Delegates digit normalization to DigitNormalizer for canonical operations.
/// </summary>
public static class TextNormalizer
{
    // Backward-compatible delegates to DigitNormalizer
    public static string ToLatinDigits(string? input) => DigitNormalizer.ToAsciiDigits(input);
    public static string ToPersianDigits(string? input) => DigitNormalizer.ToPersianDigits(input);
    public static string Normalize(string? input) => DigitNormalizer.Normalize(input);
    public static string UnifyPersianChars(string? input) => DigitNormalizer.UnifyPersianChars(input);
    public static string NormalizeForCompare(string? input) => DigitNormalizer.Normalize(DigitNormalizer.ToAsciiDigits(input));
    public static bool IsEmptyMarker(string? input) => DigitNormalizer.IsEmptyMarker(input);
    public static string SanitizeFileName(string input) => DigitNormalizer.SanitizeFileName(input);
    public static string ReverseCaseNumber(string caseNumber) => DigitNormalizer.ReverseForFilename(caseNumber);
    public static string ReverseDate(string date)
    {
        var parts = DigitNormalizer.ToAsciiDigits(date ?? "").Split('/');
        return parts.Length == 3 ? $"{parts[2].Trim()}/{parts[1].Trim()}/{parts[0].Trim()}" : date ?? "";
    }
}
