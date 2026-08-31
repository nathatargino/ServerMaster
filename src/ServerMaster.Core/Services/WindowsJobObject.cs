using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ServerMaster.Core.Services;

/// <summary>
/// Wraps a Windows Job Object to ensure child processes are terminated when the parent exits.
/// </summary>
public sealed class WindowsJobObject : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    public WindowsJobObject()
    {
        if (!OperatingSystem.IsWindows()) return;

        _handle = NativeMethods.CreateJobObject(IntPtr.Zero, null);
        
        var info = new NativeMethods.JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            LimitFlags = 0x2000 // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
        };

        var extendedInfo = new NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = info
        };

        int length = Marshal.SizeOf(typeof(NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
        IntPtr extendedInfoPtr = Marshal.AllocHGlobal(length);
        Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);

        if (!NativeMethods.SetInformationJobObject(_handle, NativeMethods.JobObjectInfoType.ExtendedLimitInformation, extendedInfoPtr, (uint)length))
        {
            // Failed to set job object information
        }
        Marshal.FreeHGlobal(extendedInfoPtr);
    }

    public void AddProcess(IntPtr processHandle)
    {
        if (!OperatingSystem.IsWindows() || _handle == IntPtr.Zero || _disposed) return;
        NativeMethods.AssignProcessToJobObject(_handle, processHandle);
    }

    public void AddProcess(Process process)
    {
        AddProcess(process.Handle);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_handle != IntPtr.Zero)
        {
            NativeMethods.CloseHandle(_handle);
            _handle = IntPtr.Zero;
        }
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetInformationJobObject(IntPtr hJob, JobObjectInfoType infoType, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        public enum JobObjectInfoType
        {
            ExtendedLimitInformation = 9
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsage;
            public UIntPtr PeakJobMemoryUsage;
        }
    }
}
