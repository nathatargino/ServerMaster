using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ServerMaster.Core.Models;

namespace ServerMaster.App.Converters;

/// <summary>
/// Converts a <see cref="LogLevel"/> enum value to a foreground <see cref="IBrush"/>
/// for the log terminal color coding.
/// </summary>
public sealed class LogLevelToBrushConverter : IValueConverter
{
    public static readonly LogLevelToBrushConverter Instance = new();

    // Cached brushes for performance
    private static readonly IBrush InfoBrush    = new SolidColorBrush(Color.Parse("#A3E6A3")); // soft green
    private static readonly IBrush WarnBrush    = new SolidColorBrush(Color.Parse("#F59E0B")); // amber
    private static readonly IBrush ErrorBrush   = new SolidColorBrush(Color.Parse("#F87171")); // soft red
    private static readonly IBrush ChatBrush    = new SolidColorBrush(Color.Parse("#60A5FA")); // sky blue
    private static readonly IBrush DebugBrush   = new SolidColorBrush(Color.Parse("#6B7280")); // muted gray
    private static readonly IBrush DefaultBrush = new SolidColorBrush(Color.Parse("#D1D5DB")); // light gray

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not LogLevel level) return DefaultBrush;

        return level switch
        {
            LogLevel.Information => InfoBrush,
            LogLevel.Warning     => WarnBrush,
            LogLevel.Error       => ErrorBrush,
            LogLevel.Chat        => ChatBrush,
            LogLevel.Debug       => DebugBrush,
            _                    => DefaultBrush
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts a <see cref="LogLevel"/> to a short label prefix string.
/// </summary>
public sealed class LogLevelToLabelConverter : IValueConverter
{
    public static readonly LogLevelToLabelConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not LogLevel level) return "?";
        return level switch
        {
            LogLevel.Information => "INFO",
            LogLevel.Warning     => "WARN",
            LogLevel.Error       => "ERR ",
            LogLevel.Chat        => "CHAT",
            LogLevel.Debug       => "DBG ",
            _                    => "????"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Converts a LogLevel enum to a background pill color.</summary>
public sealed class LogLevelToPillBrushConverter : IValueConverter
{
    public static readonly LogLevelToPillBrushConverter Instance = new();

    private static readonly IBrush InfoPill  = new SolidColorBrush(Color.Parse("#14532D"), 0.5);
    private static readonly IBrush WarnPill  = new SolidColorBrush(Color.Parse("#78350F"), 0.5);
    private static readonly IBrush ErrPill   = new SolidColorBrush(Color.Parse("#7F1D1D"), 0.5);
    private static readonly IBrush ChatPill  = new SolidColorBrush(Color.Parse("#1E3A5F"), 0.5);
    private static readonly IBrush DebugPill = new SolidColorBrush(Color.Parse("#1F2937"), 0.5);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not LogLevel level) return DebugPill;
        return level switch
        {
            LogLevel.Information => InfoPill,
            LogLevel.Warning     => WarnPill,
            LogLevel.Error       => ErrPill,
            LogLevel.Chat        => ChatPill,
            _                    => DebugPill
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Converts a double CPU/RAM percent to the status color brush.</summary>
public sealed class PercentToColorBrushConverter : IValueConverter
{
    public static readonly PercentToColorBrushConverter Instance = new();

    private static readonly IBrush LowBrush  = new SolidColorBrush(Color.Parse("#22C55E")); // green
    private static readonly IBrush MidBrush  = new SolidColorBrush(Color.Parse("#F59E0B")); // amber
    private static readonly IBrush HighBrush = new SolidColorBrush(Color.Parse("#EF4444")); // red

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double d) return LowBrush;
        return d switch
        {
            < 60  => LowBrush,
            < 85  => MidBrush,
            _     => HighBrush
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Converts a ServerState enum to a localised text label.</summary>
public sealed class ServerStateToLabelConverter : IValueConverter
{
    public static readonly ServerStateToLabelConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ServerMaster.Core.Models.ServerState s) return "—";
        return s switch
        {
            Core.Models.ServerState.Idle      => "Parado",
            Core.Models.ServerState.Preparing => "Preparando…",
            Core.Models.ServerState.Starting  => "Iniciando…",
            Core.Models.ServerState.Running   => "Online",
            Core.Models.ServerState.Stopping  => "Parando…",
            Core.Models.ServerState.Crashed   => "Crash!",
            Core.Models.ServerState.Stopped   => "Parado",
            _                                  => "—"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Converts a ServerState to a status-badge background brush.</summary>
public sealed class ServerStateToBrushConverter : IValueConverter
{
    public static readonly ServerStateToBrushConverter Instance = new();

    private static readonly IBrush Online  = new SolidColorBrush(Color.Parse("#14532D"));
    private static readonly IBrush Offline = new SolidColorBrush(Color.Parse("#1F2937"));
    private static readonly IBrush Busy    = new SolidColorBrush(Color.Parse("#78350F"));
    private static readonly IBrush Crash   = new SolidColorBrush(Color.Parse("#7F1D1D"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Core.Models.ServerState s) return Offline;
        return s switch
        {
            Core.Models.ServerState.Running   => Online,
            Core.Models.ServerState.Crashed   => Crash,
            Core.Models.ServerState.Starting  => Busy,
            Core.Models.ServerState.Preparing => Busy,
            Core.Models.ServerState.Stopping  => Busy,
            _                                  => Offline
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
