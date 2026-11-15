using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using ShootProcs.Core;

namespace ShootProcs.Windows;

public sealed class WindowsTerminator : IProcessTerminator
{
    public ProcessTermination Terminate(ProcessSample captured)
    {
        ProcessTermination Result(TerminationOutcome outcome, string detail) => new(captured.Identity, outcome, detail);
        if (captured.ProtectionReason != null)
            return Result(TerminationOutcome.Protected, captured.ProtectionReason);
        using var handle = Native.OpenProcess(Native.Query | Native.Terminate | 0x100000, false, captured.Identity.Pid);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            return Result(error == 87 ? TerminationOutcome.Exited : TerminationOutcome.AccessDenied,
                $"OpenProcess: {new System.ComponentModel.Win32Exception(error).Message}");
        }
        if (!Native.GetProcessTimes(handle, out var created, out _, out _, out _))
            return Result(TerminationOutcome.Failed, "Could not revalidate process creation time");
        if (created != captured.Identity.CreatedFileTime)
            return Result(TerminationOutcome.Replaced, "PID now belongs to a different process; skipped");
        try
        {
            using var process = Process.GetProcessById(captured.Identity.Pid);
            if (!Native.IsProcessCritical(handle, out var critical))
                return Result(TerminationOutcome.Protected, "Could not revalidate Windows critical-process protection");
            if (!Native.OpenProcessToken(handle, 8, out var token))
                return Result(TerminationOutcome.Protected, "Could not revalidate owner");
            string owner;
            using (token) using (var identity = new WindowsIdentity(token.DangerousGetHandle()))
                owner = identity.User?.Value ?? "";
            var reason = WindowsMonitor.RuntimeProtection(process.Id, process.ProcessName, process.SessionId, owner, critical);
            if (reason != null) return Result(TerminationOutcome.Protected, reason);
            if (process.HasExited) return Result(TerminationOutcome.Exited, "Already exited");
            if (!Native.TerminateProcess(handle, 1))
            {
                var error = Marshal.GetLastWin32Error();
                return Result(error == 5 ? TerminationOutcome.AccessDenied : TerminationOutcome.Failed,
                    new System.ComponentModel.Win32Exception(error).Message);
            }
            return Native.WaitForSingleObject(handle, 1500) == 0
                ? Result(TerminationOutcome.Terminated, "Termination confirmed")
                : Result(TerminationOutcome.Failed, "Termination requested but exit was not confirmed");
        }
        catch (ArgumentException) { return Result(TerminationOutcome.Exited, "Already exited"); }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        { return Result(TerminationOutcome.Failed, ex.Message); }
    }
}
