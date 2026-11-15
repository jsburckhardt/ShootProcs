namespace ShootProcs.Core;

public sealed class DiagnosticEngine
{
    private static readonly HashSet<string> SharedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "node.exe", "python.exe", "pythonw.exe", "java.exe", "javaw.exe", "dotnet.exe",
        "wscript.exe", "cscript.exe", "pwsh.exe", "powershell.exe", "cmd.exe", "dllhost.exe",
        "rundll32.exe", "RuntimeBroker.exe", "ApplicationFrameHost.exe", "backgroundTaskHost.exe",
        "taskhostw.exe", "conhost.exe", "svchost.exe", "msedgewebview2.exe"
    };
    private static readonly HashSet<string> Launchers = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer.exe", "cmd.exe", "powershell.exe", "pwsh.exe", "svchost.exe",
        "WindowsTerminal.exe", "conhost.exe"
    };
    private readonly Dictionary<string, (double Cpu, double Memory, double Disk, Queue<double> History)> _state = [];
    private DateTimeOffset? _previous;

    public Diagnosis Analyze(MonitorSnapshot snapshot)
    {
        var machine = snapshot.Machine;
        var seconds = _previous is { } previous
            ? Math.Clamp((machine.Timestamp - previous).TotalSeconds, 0.05, 10) : 1;
        _previous = machine.Timestamp;
        var alpha = 1 - Math.Exp(-seconds / 5);
        var processes = snapshot.Processes.DistinctBy(p => p.Identity).ToArray();
        var byPid = processes.ToDictionary(p => p.Identity.Pid);
        var grouped = processes.GroupBy(p => GroupKey(p, byPid), StringComparer.OrdinalIgnoreCase);
        var groups = new List<ApplicationGroup>();
        var alive = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var grouping in grouped)
        {
            var members = grouping.OrderBy(p => p.Identity.Pid).ToArray();
            var rootPath = grouping.Key.Split('|').ElementAtOrDefault(2);
            var representative = members.OrderByDescending(p => p.Executable.Equals(rootPath, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(p => p.HasWindow)
                .ThenBy(p => p.Executable.Length).First();
            var cpu = Sum(members.Select(p => p.CpuPercent));
            var memory = SumLong(members.Select(p => p.PrivateBytes));
            var working = SumLong(members.Select(p => p.WorkingSetBytes));
            var disk = Sum(members.Select(p => p.DiskBytesPerSecond));
            var network = Sum(members.Select(p => p.NetworkBytesPerSecond));
            var rawCpu = Math.Clamp((machine.CpuPercent - 55) / 35, 0, 1)
                * Math.Clamp((cpu ?? 0) / Math.Max(1, machine.CpuPercent), 0, 1);
            var rawMemory = Math.Clamp((machine.MemoryPressure - 0.72) / 0.22, 0, 1)
                * Math.Clamp((memory ?? 0) / Math.Max(1d, machine.TotalMemoryBytes * 0.2), 0, 1);
            var rawDisk = machine.DiskLatencyMs is { } latency && machine.DiskBytesPerSecond is > 0
                ? Math.Clamp((latency - 8) / 32, 0, 1)
                    * Math.Clamp((disk ?? 0) / machine.DiskBytesPerSecond.Value, 0, 1) : 0;
            _state.TryGetValue(grouping.Key, out var state);
            var history = state.History ?? new Queue<double>();
            var smoothCpu = state.Cpu + alpha * (rawCpu - state.Cpu);
            var smoothMemory = state.Memory + alpha * (rawMemory - state.Memory);
            var smoothDisk = state.Disk + alpha * (rawDisk - state.Disk);
            // Independent pressure evidence is combined as a bounded union, not a sum of percentages.
            var score = 100 * (1 - (1 - smoothCpu) * (1 - smoothMemory) * (1 - smoothDisk));
            var reasons = new List<string>();
            if (smoothCpu >= .025) reasons.Add($"sustained CPU share under {machine.CpuPercent:0}% system load");
            if (smoothMemory >= .025) reasons.Add($"private commit under {machine.MemoryPressure:P0} physical memory pressure");
            if (smoothDisk >= .025) reasons.Add($"disk traffic while I/O latency is {machine.DiskLatencyMs:0.0} ms");
            var reason = reasons.Count == 0
                ? "No sustained pressure attributed. Consumption alone does not establish a slowdown."
                : "Likely contributor: " + string.Join("; ", reasons) + ".";
            if (machine.DiskBytesPerSecond is null)
                reason += " Disk contention and network attribution unavailable without tracing.";
            else
                reason += " Network is shown as activity, not scored without evidence of link contention.";
            if (memory is not null)
                reason += " Memory is private committed bytes, not exclusive physical RAM.";
            history.Enqueue(score);
            while (history.Count > 40) history.Dequeue();
            _state[grouping.Key] = (smoothCpu, smoothMemory, smoothDisk, history);
            alive.Add(grouping.Key);
            groups.Add(new(grouping.Key, FriendlyName(representative, members),
                representative.Executable, members, cpu, memory, working, disk, network,
                new(score, reason, cpu is null || memory is null || disk is null || network is null
                    ? "Partial telemetry; attribution may be incomplete" : "Measured pressure; heuristic attribution"),
                members.Any(p => p.HasWindow), history.ToArray()));
        }
        foreach (var key in _state.Keys.Where(k => !alive.Contains(k)).ToArray()) _state.Remove(key);
        var ordered = groups.OrderByDescending(g => g.Impact.Score)
            .ThenByDescending(g => g.CpuPercent).ThenByDescending(g => g.PrivateBytes).ToArray();
        var pressured = machine.CpuPercent > 65 || machine.MemoryPressure > .8 || machine.DiskLatencyMs > 15;
        return new(machine, ordered,
            pressured ? "Pressure detected. Find your next move." : "No significant pressure",
            pressured ? "Ranked by sustained contribution, not consumption alone."
                : "Your machine has headroom. Large applications are not necessarily offenders.");
    }

    private static double? Sum(IEnumerable<double?> values)
    {
        var array = values.ToArray();
        return array.Length == 0 || array.Any(v => v == null) ? null : array.Sum(v => v!.Value);
    }

    private static long? SumLong(IEnumerable<long?> values)
    {
        var array = values.ToArray();
        return array.Length == 0 || array.Any(v => v == null) ? null : array.Sum(v => v!.Value);
    }

    private static string GroupKey(ProcessSample process, Dictionary<int, ProcessSample> byPid)
    {
        var root = process;
        var visited = new HashSet<int> { process.Identity.Pid };
        while (byPid.TryGetValue(root.ParentPid, out var parent) && visited.Add(parent.Identity.Pid))
        {
            if (parent.Session != process.Session || parent.Owner != process.Owner
                || parent.Identity.CreatedFileTime > root.Identity.CreatedFileTime
                || (root.Profile != null && parent.Profile != null
                    && !root.Profile.Equals(parent.Profile, StringComparison.OrdinalIgnoreCase))
                || !Related(root, parent))
                break;
            root = parent;
        }
        if (string.IsNullOrEmpty(root.Executable) || string.IsNullOrEmpty(root.Owner))
            return $"unknown:{process.Identity.Pid}:{process.Identity.CreatedFileTime}";
        var hostRoot = SharedHosts.Contains(Path.GetFileName(root.Executable))
            ? $"|job:{root.Identity.Pid}:{root.Identity.CreatedFileTime}" : "";
        return $"{root.Owner}|{root.Session}|{root.Executable.ToUpperInvariant()}|{root.Profile ?? process.Profile ?? ""}{hostRoot}";
    }

    private static bool Related(ProcessSample childProcess, ProcessSample parentProcess)
    {
        var child = childProcess.Executable;
        var parent = parentProcess.Executable;
        if (string.IsNullOrEmpty(child) || string.IsNullOrEmpty(parent)) return false;
        if (child.Equals(parent, StringComparison.OrdinalIgnoreCase)) return true;
        if (Path.GetFileName(child).Equals("msedgewebview2.exe", StringComparison.OrdinalIgnoreCase)
            && !Launchers.Contains(Path.GetFileName(parent)))
            return true;
        if (SharedHosts.Contains(Path.GetFileName(child)) && !Launchers.Contains(Path.GetFileName(parent))
            && (parentProcess.HasWindow || Path.GetFileName(parent).Equals("Code.exe", StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(parent).Equals("devenv.exe", StringComparison.OrdinalIgnoreCase)))
            return true;
        var directory = Path.GetDirectoryName(parent);
        // Distinct helper executables join only through an observed ancestry within the installation.
        return directory is { Length: > 3 }
            && !directory.EndsWith("System32", StringComparison.OrdinalIgnoreCase)
            && !directory.EndsWith("Windows", StringComparison.OrdinalIgnoreCase)
            && child.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string FriendlyName(ProcessSample representative, ProcessSample[] members)
    {
        var profile = members.Select(p => p.Profile).FirstOrDefault(p => p != null);
        var job = "";
        if (SharedHosts.Contains(Path.GetFileName(representative.Executable)))
        {
            var pids = members.Select(p => p.Identity.Pid).ToHashSet();
            var root = members.FirstOrDefault(p => !pids.Contains(p.ParentPid)) ?? representative;
            job = $" / job {root.Identity.Pid}";
        }
        return representative.Name + job + (profile == null ? "" : $" [{profile}]");
    }
}

public sealed class ArenaSelection
{
    private readonly HashSet<string> _keys = [];

    public IReadOnlyList<ApplicationGroup> Select(IReadOnlyList<ApplicationGroup> groups,
        ResourceMode mode, string? pinned = null, int limit = 24)
    {
        var ranked = groups.OrderByDescending(g => Weight(g, mode))
            .ThenByDescending(g => g.IsForeground).ThenBy(g => g.Name).ToArray();
        var rankedByKey = ranked.ToDictionary(g => g.Key);
        var selected = _keys.Where(rankedByKey.ContainsKey).Select(k => rankedByKey[k]).ToList();
        selected = selected.OrderByDescending(g => Weight(g, mode)).Take(limit).ToList();
        foreach (var candidate in ranked)
        {
            if (selected.Any(g => g.Key == candidate.Key)) continue;
            if (selected.Count < limit) { selected.Add(candidate); continue; }
            var weakest = selected.Where(g => g.Key != pinned).MinBy(g => Weight(g, mode));
            if (weakest != null && Weight(candidate, mode) > Weight(weakest, mode) * 1.3 + .5)
            {
                selected.Remove(weakest);
                selected.Add(candidate);
            }
        }
        _keys.Clear();
        foreach (var group in selected) _keys.Add(group.Key);
        return selected.OrderByDescending(g => Weight(g, mode)).ToArray();
    }

    public static double Weight(ApplicationGroup group, ResourceMode mode) => mode switch
    {
        ResourceMode.CPU => group.CpuPercent ?? 0,
        ResourceMode.Memory => (group.PrivateBytes ?? 0) / (1024d * 1024),
        ResourceMode.Disk => (group.DiskBytesPerSecond ?? 0) / 1024,
        ResourceMode.Network => (group.NetworkBytesPerSecond ?? 0) / 1024,
        _ => group.Impact.Score > 1 ? group.Impact.Score * 100
            : (group.CpuPercent ?? 0) + (group.PrivateBytes ?? 0) / (1024d * 1024 * 1024)
    };
}
