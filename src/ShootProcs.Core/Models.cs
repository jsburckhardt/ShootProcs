namespace ShootProcs.Core;

public enum ResourceMode { Overall, CPU, Memory, Disk, Network }

public readonly record struct ProcessIdentity(int Pid, long CreatedFileTime);

public sealed record ProcessSample(
    ProcessIdentity Identity, int ParentPid, string Name, string Executable,
    string Owner, int Session, string? Profile, bool HasWindow,
    double? CpuPercent, long? PrivateBytes, long? WorkingSetBytes,
    double? DiskBytesPerSecond, double? NetworkBytesPerSecond,
    string? ProtectionReason)
{
    public bool CanTerminate => ProtectionReason == null;
}

public sealed record MachineSample(
    DateTimeOffset Timestamp, double CpuPercent, long TotalMemoryBytes,
    long AvailableMemoryBytes, double? DiskBytesPerSecond, double? DiskLatencyMs,
    double? NetworkBytesPerSecond, string TelemetryStatus)
{
    public double MemoryPressure => TotalMemoryBytes > 0
        ? 1 - (double)AvailableMemoryBytes / TotalMemoryBytes : 0;
}

public sealed record MonitorSnapshot(
    MachineSample Machine, IReadOnlyList<ProcessSample> Processes);

public sealed record Impact(double Score, string Reason, string Confidence);

public sealed record ApplicationGroup(
    string Key, string Name, string Executable, IReadOnlyList<ProcessSample> Processes,
    double? CpuPercent, long? PrivateBytes, long? WorkingSetBytes,
    double? DiskBytesPerSecond, double? NetworkBytesPerSecond,
    Impact Impact, bool IsForeground, double[] History)
{
    public string? ProtectionReason => Processes.FirstOrDefault(p => p.ProtectionReason != null)?.ProtectionReason;
    public bool CanTerminate => ProtectionReason == null;
}

public sealed record Diagnosis(
    MachineSample Machine, IReadOnlyList<ApplicationGroup> Groups,
    string Headline, string Detail);

public sealed record TerminationRequest(
    string Name, IReadOnlyList<ProcessSample> Processes, bool SafeMode)
{
    public static TerminationRequest Capture(ApplicationGroup group, bool safeMode) =>
        new(group.Name, group.Processes.ToArray(), safeMode);
}

public enum TerminationOutcome { Terminated, Exited, Replaced, Protected, AccessDenied, Failed }
public sealed record ProcessTermination(ProcessIdentity Identity, TerminationOutcome Outcome, string Detail);
public sealed record TerminationResult(bool ConfirmationRequired, IReadOnlyList<ProcessTermination> Processes)
{
    public int Killed => Processes.Count(p => p.Outcome == TerminationOutcome.Terminated);
    public int Failed => Processes.Count(p => p.Outcome is TerminationOutcome.AccessDenied
        or TerminationOutcome.Failed or TerminationOutcome.Protected);
    public bool Complete => !ConfirmationRequired && Failed == 0;
}

public interface IProcessTerminator
{
    ProcessTermination Terminate(ProcessSample captured);
}

public sealed class TerminationController(IProcessTerminator terminator)
{
    public TerminationResult Execute(TerminationRequest request, bool confirmed = false)
    {
        if (request.SafeMode && !confirmed)
            return new(true, []);

        var results = request.Processes.DistinctBy(p => p.Identity)
            .Select(p => p.ProtectionReason is { } reason
                ? new ProcessTermination(p.Identity, TerminationOutcome.Protected, reason)
                : terminator.Terminate(p)).ToArray();
        return new(false, results);
    }
}

public static class Display
{
    public static string Bytes(double? value) => value switch
    {
        null => "Unavailable",
        >= 1024 * 1024 * 1024 => $"{value / (1024 * 1024 * 1024):0.0} GB",
        >= 1024 * 1024 => $"{value / (1024 * 1024):0.0} MB",
        >= 1024 => $"{value / 1024:0.0} KB",
        _ => $"{value:0} B"
    };

    public static string Rate(double? value) => value is null ? "Unavailable" : $"{Bytes(value)}/s";
    public static string Percent(double? value) => value is null ? "Unavailable" : $"{value:0.0}%";
}
