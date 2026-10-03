using System.Globalization;
using System.Text;

namespace NezamMonitor.Core;

/// <summary>
/// Persian Solar Hijri date utility using .NET's built-in PersianCalendar.
/// </summary>
public static class PersianDateHelper
{
    private static readonly PersianCalendar _pc = new();

    /// <summary>
    /// Convert Gregorian DateTime to Persian Solar Hijri date string (yyyy/MM/dd).
    /// </summary>
    public static string ToPersianDate(DateTime date)
    {
        int y = _pc.GetYear(date);
        int m = _pc.GetMonth(date);
        int d = _pc.GetDayOfMonth(date);
        return $"{y:0000}/{m:00}/{d:00}";
    }

    /// <summary>
    /// Convert to Persian date with Persian digits (۱۴۰۵/۰۶/۰۵).
    /// </summary>
    public static string ToPersianDateDigits(DateTime date)
    {
        return TextNormalizer.ToPersianDigits(ToPersianDate(date));
    }

    /// <summary>
    /// Convert to filename-safe format: ۱۴۰۵-۰۶-۰۵ (no slash, Persian digits).
    /// </summary>
    public static string ToPersianDateFile(DateTime date)
    {
        return TextNormalizer.ToPersianDigits(ToPersianDate(date).Replace("/", "-"));
    }

    /// <summary>تاریخ+ساعت میلادی (DateTime) به شمسی yyyy/MM/dd HH:mm</summary>
    public static string ToShamsiDateTime(DateTime dt)
        => $"{ToPersianDate(dt)} {dt:HH:mm}";

    /// <summary>
    /// رشته تاریخ میلادی "yyyy/MM/dd HH:mm" (یا با ثانیه/خط‌تیره) را به شمسی "yyyy/MM/dd HH:mm" تبدیل می‌کند.
    /// اگر پارس نشد، خود رشته برگردانده می‌شود.
    /// </summary>
    public static string GregoryStringToShamsi(string? gregorianDateTime)
    {
        if (string.IsNullOrWhiteSpace(gregorianDateTime)) return "";
        var s = gregorianDateTime.Trim();
        // فرمت‌های رایج ذخیره‌شده: yyyy/MM/dd HH:mm , yyyy/MM/dd HH:mm:ss , yyyy-MM-dd ...
        string[] fmts = { "yyyy/MM/dd HH:mm:ss", "yyyy/MM/dd HH:mm", "yyyy/MM/dd",
                          "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd",
                          "yyyy/MM/ddTHH:mm:ss", "O", "o" };
        if (DateTime.TryParseExact(s, fmts, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt)
            || DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out dt))
            return ToShamsiDateTime(dt);
        return s;
    }
}
