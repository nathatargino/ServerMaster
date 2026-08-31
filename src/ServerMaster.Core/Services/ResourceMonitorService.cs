using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ServerMaster.Core.Models;

namespace ServerMaster.Core.Services;

/// <summary>
/// Polls a running server process every second, emitting CPU and RAM usage.
/// </summary>
public sealed class ResourceMonitorService : IDisposable
{
    private readonly Subject<ResourceSnapshot> _subject = new();
    private IDisposable? _timer;
    private Process? _target;
    private TimeSpan _prevTotalProcTime;
    private DateTime _prevTime;

    public IObservable<ResourceSnapshot> ResourceStream => _subject.AsObservable();

    /// <summary>Starts polling <paramref name="process"/> every <paramref name="interval"/>.</summary>
    public void Start(Process process, TimeSpan? interval = null)
    {
        Stop();
        _target = process;
        _prevTotalProcTime = process.TotalProcessorTime;
        _prevTime = DateTime.UtcNow;

        _timer = Observable
            .Interval(interval ?? TimeSpan.FromSeconds(1))
            .Subscribe(_ => Poll());
    }

    private void Poll()
    {
        if (_target is null || _target.HasExited)
        {
            Stop();
            return;
        }

        try
        {
            _target.Refresh();
            var now = DateTime.UtcNow;
            var cpuUsed = _target.TotalProcessorTime - _prevTotalProcTime;
            var elapsed = now - _prevTime;

            _prevTotalProcTime = _target.TotalProcessorTime;
            _prevTime = now;

            double cpuPercent = elapsed.TotalMilliseconds > 0
                ? cpuUsed.TotalMilliseconds / elapsed.TotalMilliseconds / Environment.ProcessorCount * 100.0
                : 0;

            _subject.OnNext(new ResourceSnapshot(
                Timestamp: DateTimeOffset.Now,
                CpuPercent: Math.Round(Math.Min(cpuPercent, 100.0), 1),
                RamBytes: _target.WorkingSet64
            ));
        }
        catch (InvalidOperationException) { Stop(); }
    }

    public void Stop()
    {
        if (_timer != null)
        {
            _subject.OnNext(new ResourceSnapshot(DateTimeOffset.Now, 0, 0));
        }

        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose()
    {
        Stop();
        _subject.Dispose();
    }
}
