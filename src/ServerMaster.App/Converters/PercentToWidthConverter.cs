using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ServerMaster.App.Converters;

/// <summary>
/// Converts a percentage (0-100) and a total width into the actual pixel width for a progress bar.
/// Values[0] = double percent
/// Values[1] = double totalWidth
/// </summary>
public sealed class PercentToWidthConverter : IMultiValueConverter
{
    public static readonly PercentToWidthConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count >= 2 && 
            values[0] is double percent && 
            values[1] is double width)
        {
            if (width <= 0) return 0d;
            var pct = Math.Max(0, Math.Min(100, percent)) / 100.0;
            return width * pct;
        }

        return 0d;
    }
}
