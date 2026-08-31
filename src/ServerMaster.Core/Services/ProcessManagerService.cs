using System.Diagnostics;

namespace ServerMaster.Core.Services;

/// <summary>
/// Low-level wrapper around <see cref="Process"/> to launch, communicate with,
/// and monitor game server processes.
/// </summary>
public sealed class ProcessManagerService : IDisposable
{
    private readonly WindowsJobObject _jobObject = new();

    /// <summary>
    /// Starts a new child process with redirected stdio streams.
    /// </summary>
    public Process Start(string workingDir, string fileName, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName               = fileName,
            Arguments              = arguments,
            WorkingDirectory       = workingDir,
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            RedirectStandardInput  = true,
            CreateNoWindow         = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding  = System.Text.Encoding.UTF8
        };

        var process = new Process
        {
            StartInfo            = psi,
            EnableRaisingEvents  = true
        };

        process.Start();
        
        // Ensure child process dies with us
        _jobObject.AddProcess(process);
        
        return process;
    }

    /// <summary>
    /// Writes a command line to the process's stdin.
    /// </summary>
    public void SendCommand(Process process, string command)
    {
        if (process.HasExited) return;
        process.StandardInput.WriteLine(command);
        process.StandardInput.Flush();
    }

    /// <summary>
    /// Attempts a graceful kill; falls back to hard kill after <paramref name="timeout"/>.
    /// </summary>
    public async Task KillAsync(Process process, TimeSpan timeout)
    {
        if (process.HasExited) return;
        process.CloseMainWindow();
        var exited = await Task.Run(() => process.WaitForExit((int)timeout.TotalMilliseconds));
        if (!exited) process.Kill(entireProcessTree: true);
    }

    public void Dispose()
    {
        _jobObject.Dispose();
    }
}
