using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using ShootProcs.Core;

namespace ShootProcs.Windows;

public sealed partial class WindowsMonitor : IDisposable
{
    private sealed record Metadata(string Name, string Path, string Owner, int Session, string? Profile, string? Protection);
    private readonly Dictionary<ProcessIdentity, Metadata> _metadata = [];
    private readonly Dictionary<ProcessIdentity, long> _cpu = [];
    private readonly TraceTelemetry _tracing = new();
    private long _lastTimestamp = Stopwatch.GetTimestamp();
    private long _systemIdle, _systemTotal;
    private readonly int _ownSession = Process.GetCurrentProcess().SessionId;
    private readonly string _owner = CurrentOwner();
    private static readonly HashSet<string> Essential = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "Registry", "Secure System", "Memory Compression", "smss", "csrss",
        "wininit", "winlogon", "services", "lsass", "lsaiso", "svchost", "dwm", "fontdrvhost",
        "MsMpEng", "SecurityHealthService", "LsaIso", "sihost"
    };

    public static bool IsElevated
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    private static string CurrentOwner()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return identity.User?.Value ?? "";
    }

    public WindowsMonitor()
    {
        Native.GetSystemTimes(out _systemIdle, out var kernel, out var user);
        _systemTotal = kernel + user;
    }

    public MonitorSnapshot Sample()
    {
        var now = Stopwatch.GetTimestamp();
        var elapsed = Math.Max(.001, Stopwatch.GetElapsedTime(_lastTimestamp, now).TotalSeconds);
        _lastTimestamp = now;
        var parents = Native.Parents();
        var windows = Native.WindowedProcesses();
        var telemetry = _tracing.Drain(elapsed);
        var samples = new List<ProcessSample>();
        var alive = new HashSet<ProcessIdentity>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    var pid = process.Id;
                    using var handle = Native.OpenProcess(Native.Query | Native.VmRead, false, pid);
                    using var queryOnly = handle.IsInvalid ? Native.OpenProcess(Native.Query, false, pid) : null;
                    var query = handle.IsInvalid ? queryOnly! : handle;
                    var canRead = !query.IsInvalid && Native.GetProcessTimes(query, out _, out _, out _, out _);
                    long created = 0, kernel = 0, user = 0;
                    if (canRead) Native.GetProcessTimes(query, out created, out _, out kernel, out user);
                    var identity = new ProcessIdentity(pid, created);
                    alive.Add(identity);
                    if (!_metadata.TryGetValue(identity, out var metadata))
                    {
                        metadata = ReadMetadata(process, query, canRead);
                        _metadata[identity] = metadata;
                    }
                    double? cpu = canRead && _cpu.TryGetValue(identity, out var before)
                        ? Math.Clamp((kernel + user - before) / 10_000_000d / elapsed / Environment.ProcessorCount * 100, 0, 100)
                        : null;
                    if (canRead) _cpu[identity] = kernel + user;
                    long? memory = null, working = null;
                    if (!handle.IsInvalid && Native.GetProcessMemoryInfo(handle, out var counters,
                            (uint)Marshal.SizeOf<Native.MemoryCounters>()))
                    {
                        memory = (long)counters.PrivateUsage;
                        working = (long)counters.WorkingSet;
                    }
                    telemetry.Processes.TryGetValue(pid, out var io);
                    var attributionValid = telemetry.Available && created > 0
                        && (io.EarliestFileTime == 0 || io.EarliestFileTime >= created);
                    samples.Add(new(identity, parents.GetValueOrDefault(pid), metadata.Name, metadata.Path,
                        metadata.Owner, metadata.Session, metadata.Profile, windows.Contains(pid),
                        cpu, memory, working, attributionValid ? io.Disk / elapsed : null,
                        attributionValid ? io.Network / elapsed : null, metadata.Protection));
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception
                    or NotSupportedException)
                { /* Processes may exit between enumeration and sampling. */ }
            }
        }
        foreach (var identity in _metadata.Keys.Where(k => !alive.Contains(k)).ToArray())
        {
            _metadata.Remove(identity);
            _cpu.Remove(identity);
        }
        var status = new Native.MemoryStatus { Length = (uint)Marshal.SizeOf<Native.MemoryStatus>() };
        if (!Native.GlobalMemoryStatusEx(ref status))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Cannot read system memory.");
        var systemCpu = 0d;
        if (Native.GetSystemTimes(out var idle, out var sysKernel, out var sysUser))
        {
            var total = sysKernel + sysUser;
            var delta = total - _systemTotal;
            systemCpu = delta > 0 ? Math.Clamp(100d * (1 - (double)(idle - _systemIdle) / delta), 0, 100) : 0;
            _systemIdle = idle;
            _systemTotal = total;
        }
        return new(new(DateTimeOffset.Now, systemCpu, (long)status.TotalPhysical,
            (long)status.AvailablePhysical, telemetry.Available ? telemetry.DiskBytes / elapsed : null,
            telemetry.LatencyMs, telemetry.Available ? telemetry.NetworkBytes / elapsed : null,
            _tracing.Status), samples);
    }

    private Metadata ReadMetadata(Process process, Microsoft.Win32.SafeHandles.SafeProcessHandle handle, bool canRead)
    {
        var rawName = process.ProcessName;
        var path = "";
        var owner = "";
        var session = -1;
        var name = rawName;
        string? profile = null;
        if (canRead)
        {
            var builder = new StringBuilder(32768);
            var length = builder.Capacity;
            if (Native.QueryFullProcessImageName(handle, 0, builder, ref length)) path = builder.ToString();
            if (Native.OpenProcessToken(handle, 8, out var token))
            {
                using (token) using (var identity = new WindowsIdentity(token.DangerousGetHandle()))
                    owner = identity.User?.Value ?? "";
            }
            session = process.SessionId;
            if (path.Length > 0)
            {
                try { name = FileVersionInfo.GetVersionInfo(path).FileDescription is { Length: > 0 } friendly ? friendly : rawName; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { }
            }
            if (rawName is "chrome" or "msedge" or "firefox" or "brave")
            {
                var matches = ProfileArgument().Matches(Native.CommandLine(handle));
                if (matches.Count > 0) profile = string.Join("; ", matches.Select(match =>
                    $"{match.Groups["flag"].Value.TrimStart('-').ToLowerInvariant()}={match.Groups["value"].Value}")
                    .Order(StringComparer.OrdinalIgnoreCase));
            }
        }
        string? protection = null;
        if (process.Id == Environment.ProcessId) protection = "ShootProcs protects itself";
        else if (process.Id <= 4 || Essential.Contains(rawName)) protection = "Essential Windows process";
        else if (!canRead || string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(path))
            protection = "Identity or permissions unavailable";
        else if (session != _ownSession || owner != _owner)
            protection = "Outside your Windows user/session";
        else if (!Native.IsProcessCritical(handle, out var critical))
            protection = "Cannot establish process protection state";
        else if (critical) protection = "Windows marks this process as critical";
        return new(name, path, owner, session, profile, protection);
    }

    [GeneratedRegex("(?<flag>--(?:profile-directory|user-data-dir|profile)|-(?:profile|P))(?:=|\\s+)(?:\"(?<value>[^\"]+)\"|(?<value>\\S+))", RegexOptions.IgnoreCase)]
    private static partial Regex ProfileArgument();

    internal static string? RuntimeProtection(int pid, string name, int session, string owner, bool critical) =>
        pid == Environment.ProcessId ? "ShootProcs protects itself"
        : pid <= 4 || Essential.Contains(name) || critical ? "Essential Windows process"
        : session != Process.GetCurrentProcess().SessionId || owner != CurrentOwner()
            ? "Outside your Windows user/session" : null;

    public void Dispose() => _tracing.Dispose();
}
