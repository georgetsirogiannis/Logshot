using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Logshot.Converters;

/// <summary>
/// Converts a width value (double) into a boolean based on a breakpoint threshold passed as
/// the converter parameter. Used to switch between the desktop grid layout and the mobile
/// adaptive card layout depending on the available width.
/// </summary>
/// <remarks>
/// By default, returns true when width is LESS than the breakpoint (i.e. "is narrow / mobile").
/// Pass parameter "Invert" to get the opposite ("is wide / desktop") behavior.
/// </remarks>
public class WidthToBoolConverter : IValueConverter
{
    /// <summary>
    /// The width (in pixels) below which the layout is considered "mobile".
    /// Matches the 720px breakpoint referenced in the project roadmap.
    /// </summary>
    public double Breakpoint { get; set; } = 720;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double width)
            return false;

        var isNarrow = width < Breakpoint;
        var invert = string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase);

        return invert ? !isNarrow : isNarrow;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
