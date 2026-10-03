using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NezamMonitor.App.Converters;
/// <summary>
/// Converts bool to Visibility (true = Visible, false = Collapsed).
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility v && v == Visibility.Visible;
    }
}

/// <summary>
/// Inverts a boolean value.
/// </summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return false;
    }
}

/// <summary>
/// Converts non-empty string to Visibility.
/// </summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s && !string.IsNullOrEmpty(s))
            return Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts bool to arrow character (▲/▼).
/// </summary>
public sealed class BoolToArrowConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? "▲" : "▼";
        return "▼";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts integer stage to bool (for RadioButton IsChecked).
/// </summary>
public sealed class StageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int stage && parameter is string s && int.TryParse(s, out int param))
            return stage == param;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b && parameter is string s && int.TryParse(s, out int param))
            return param;
        return 0;
    }
}

/// <summary>
/// Converts int tab index to Visibility (0 = Visible, else Collapsed).
/// </summary>
public sealed class TabToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int activeTab && parameter is string s && int.TryParse(s, out int tabIndex))
            return activeTab == tabIndex ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

/// <summary>
/// Converts int tab index to background color for tab buttons.
/// </summary>
public sealed class TabBgConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int activeTab && parameter is string s && int.TryParse(s, out int tabIndex))
            return activeTab == tabIndex ? "#3498DB" : "#ECF0F1";
        return "#ECF0F1";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

/// <summary>
/// Converts int tab index to foreground color for tab buttons.
/// </summary>
public sealed class TabFgConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int activeTab && parameter is string s && int.TryParse(s, out int tabIndex))
            return activeTab == tabIndex ? "White" : "#2C3E50";
        return "#2C3E50";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

/// <summary>
/// Converts int to bool for report count > 1 (for styling reports with multiple entries).
/// Used by FollowUpView to highlight cases with more than one report.
/// </summary>
public sealed class ReportCountConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
            return count > 1;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// ═══════════════════════════════════════════════════════
// Dashboard Converters
// ═══════════════════════════════════════════════════════

/// <summary>Converts status level string to brush for left-side indicator bar.</summary>
public sealed class LevelToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "critical" => "#E74C3C",
            "warning" => "#E67E22",
            "urgent" => "#F39C12",
            "normal" => "#3498DB",
            _ => "#BDC3C7"
        };
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

/// <summary>Converts status level string to background brush for status badge.</summary>
public sealed class LevelToBgBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "urgent" => "#E74C3C",
            "normal" => "#3498DB",
            _ => "#3498DB"
        };
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

/// <summary>Converts int: 0 → Gray, else → default (Black).</summary>
public sealed class ZeroToGrayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int i && i == 0) return "#27AE60";
        return "#E74C3C";
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

/// <summary>Converts int: 0 → Collapsed, else → Visible.</summary>
public sealed class ZeroToVisibleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int i && i == 0) return Visibility.Collapsed;
        return Visibility.Visible;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

/// <summary>Converts int: >0 → Visible (badge), 0 → Collapsed.</summary>
public sealed class CountToBadgeVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int i && i > 0) return Visibility.Visible;
        return Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
