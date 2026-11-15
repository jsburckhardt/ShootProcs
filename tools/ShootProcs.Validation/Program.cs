using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using ShootProcs.Core;
using ShootProcs.Windows;

if (args.Length > 0 && args[0] == "--verify-telemetry")
    return await VerifyTelemetry(args.Length > 1 ? args[1] : Path.Combine("artifacts", "validation"));

if (args.Contains("--worker"))
{
    if (args.Contains("--spawn-child"))
    {
        using var child = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--worker")
        { UseShellExecute = false, CreateNoWindow = true })!;
        Console.WriteLine(child.Id);
    }
    var allocation = new byte[96 * 1024 * 1024];
    for (var i = 0; i < allocation.Length; i += 4096) allocation[i] = 42;
    while (true)
    {
        var timer = Stopwatch.StartNew();
        while (timer.ElapsedMilliseconds < 100) allocation[0] = unchecked((byte)(allocation[0] + 1));
        Thread.Sleep(900);
        GC.KeepAlive(allocation);
    }
}

var output = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(Environment.CurrentDirectory, "artifacts", "validation");
Directory.CreateDirectory(output);
var fixtureDirectory = Path.Combine(output, "fixture");
Directory.CreateDirectory(fixtureDirectory);
foreach (var file in Directory.GetFiles(AppContext.BaseDirectory))
    File.Copy(file, Path.Combine(fixtureDirectory, Path.GetFileName(file)), true);
var fixture = Path.Combine(fixtureDirectory, "ShootProcs.Validation.exe");
var workers = new List<Process>();
var checks = new Dictionary<string, object>();
Process StartWorker()
{
    var process = Process.Start(new ProcessStartInfo(fixture, "--worker") { UseShellExecute = false, CreateNoWindow = true })!;
    workers.Add(process);
    return process;
}
void Check(string name, bool condition)
{
    checks[name] = condition;
    Console.WriteLine($"{(condition ? "PASS" : "FAIL")} {name}");
}
try
{
    var first = StartWorker();
    var second = StartWorker();
    await Task.Delay(1500);
    using var monitor = new WindowsMonitor();
    var baselineStart = Stopwatch.GetTimestamp();
    var baselineBefore = workers.ToDictionary(p => p.Id, p => p.TotalProcessorTime.TotalSeconds);
    monitor.Sample();
    var baselineAfter = workers.ToDictionary(p => p.Id, p => p.TotalProcessorTime.TotalSeconds);
    var baselineEnd = Stopwatch.GetTimestamp();
    await Task.Delay(1200);
    var currentStart = Stopwatch.GetTimestamp();
    var currentBefore = workers.ToDictionary(p => p.Id, p => p.TotalProcessorTime.TotalSeconds);
    var snapshot = monitor.Sample();
    var currentAfter = workers.ToDictionary(p => p.Id, p => p.TotalProcessorTime.TotalSeconds);
    var currentEnd = Stopwatch.GetTimestamp();
    // Native reads happen inside enumeration, not at the sample's start time.
    var longest = Stopwatch.GetElapsedTime(baselineStart, currentEnd).TotalSeconds;
    var shortest = Stopwatch.GetElapsedTime(baselineEnd, currentStart).TotalSeconds;
    var referenceCpu = workers.ToDictionary(p => p.Id, p => (
        Minimum: Math.Max(0, currentBefore[p.Id] - baselineAfter[p.Id]) / longest / Environment.ProcessorCount * 100,
        Maximum: Math.Max(0, currentAfter[p.Id] - baselineBefore[p.Id]) / shortest / Environment.ProcessorCount * 100));
    var ownWorkers = snapshot.Processes.Where(p => p.Executable.Equals(fixture, StringComparison.OrdinalIgnoreCase)).ToArray();
    Check("discoversIsolatedWorkers", ownWorkers.Length == 2);
    Check("cpuMeasuredAndNormalized", ownWorkers.All(p => p.CpuPercent is >= 0 and <= 100));
    Check("cpuAgreesWithWindowsTimeDeltas", ownWorkers.All(p =>
        p.CpuPercent >= referenceCpu[p.Identity.Pid].Minimum - .05
        && p.CpuPercent <= referenceCpu[p.Identity.Pid].Maximum + .05));
    Check("privateMemoryMeasured", ownWorkers.All(p => p.PrivateBytes >= 90 * 1024 * 1024));
    Check("memoryAgreesWithWindowsProcessAPI", ownWorkers.All(p =>
    {
        using var native = Process.GetProcessById(p.Identity.Pid);
        return Math.Abs(native.PrivateMemorySize64 - p.PrivateBytes!.Value) < 16 * 1024 * 1024;
    }));
    Check("systemMemorySane", snapshot.Machine.TotalMemoryBytes > 0
        && snapshot.Machine.AvailableMemoryBytes > 0 && snapshot.Machine.MemoryPressure is > 0 and < 1);
    var groups = new DiagnosticEngine().Analyze(snapshot).Groups;
    var group = groups.Single(g => g.Processes.Any(p => p.Identity.Pid == first.Id));
    Check("sameInstallationGrouped", group.Processes.Count == 2);
    Check("groupPrivateBytesAreSummedOnce", group.PrivateBytes == ownWorkers.Sum(p => p.PrivateBytes));
    var controller = new TerminationController(new WindowsTerminator());
    var request = TerminationRequest.Capture(group, true);
    Check("safeModeDoesNotTerminateBeforeConfirmation", controller.Execute(request).ConfirmationRequired
        && !first.HasExited && !second.HasExited);
    var third = StartWorker();
    await Task.Delay(500);
    var confirmed = controller.Execute(request, true);
    Check("confirmedGroupTerminatesCapturedWorkers", confirmed.Killed == 2 && first.HasExited && second.HasExited);
    Check("newInstanceNotSweptIntoConfirmedShot", !third.HasExited);
    var refreshed = monitor.Sample();
    var thirdSample = refreshed.Processes.Single(p => p.Identity.Pid == third.Id);
    var forged = thirdSample with { Identity = thirdSample.Identity with { CreatedFileTime = thirdSample.Identity.CreatedFileTime - 1 } };
    var replaced = controller.Execute(new("stale identity", [forged], false));
    Check("staleIdentityIsSkipped", replaced.Processes[0].Outcome == TerminationOutcome.Replaced && !third.HasExited);
    var immediate = controller.Execute(new("single worker", [thirdSample], false));
    Check("safeModeOffTerminatesImmediately", immediate.Killed == 1 && third.HasExited);
    var after = monitor.Sample();
    Check("refreshDropsExitedWorkers", !after.Processes.Any(p => ownWorkers.Any(w => w.Identity == p.Identity)
        || p.Identity == thirdSample.Identity));
    var parent = Process.Start(new ProcessStartInfo(fixture, "--worker --spawn-child")
    { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })!;
    workers.Add(parent);
    var childPidText = await parent.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5));
    var child = Process.GetProcessById(int.Parse(childPidText!));
    workers.Add(child);
    await Task.Delay(500);
    var family = monitor.Sample();
    var parentSample = family.Processes.Single(p => p.Identity.Pid == parent.Id);
    var childSample = family.Processes.Single(p => p.Identity.Pid == child.Id);
    Check("observesChildRelationship", childSample.ParentPid == parent.Id);
    var parentOnly = controller.Execute(new("parent only", [parentSample], false));
    Check("individualActionDoesNotKillDescendants", parentOnly.Killed == 1 && parent.HasExited && !child.HasExited);
    var childOnly = controller.Execute(new("child cleanup", [childSample], false));
    Check("disposableChildCleanupConfirmed", childOnly.Killed == 1 && child.HasExited);
    var self = after.Processes.Single(p => p.Identity.Pid == Environment.ProcessId);
    var protectedSelf = controller.Execute(new("self", [self], false));
    Check("selfProtectionInController", protectedSelf.Processes[0].Outcome == TerminationOutcome.Protected);
    var forgedUnprotectedSelf = self with { ProtectionReason = null };
    var runtimeSelf = new WindowsTerminator().Terminate(forgedUnprotectedSelf);
    Check("selfProtectionRecheckedAtWindowsBoundary", runtimeSelf.Outcome == TerminationOutcome.Protected);
    Check("essentialProcessesProtected", after.Processes.Where(p => p.Identity.Pid <= 4).All(p => p.ProtectionReason != null));
    Check("unprivilegedDiskNetworkMarkedUnavailable", WindowsMonitor.IsElevated
        || (after.Machine.DiskBytesPerSecond == null && after.Machine.NetworkBytesPerSecond == null));
    checks["telemetryStatus"] = after.Machine.TelemetryStatus;
    checks["processesObserved"] = after.Processes.Count;
    checks["fixtureExecutable"] = fixture;
}
catch (Exception ex) { checks["error"] = ex.ToString(); Console.Error.WriteLine(ex); }
finally
{
    foreach (var process in workers)
    {
        try { if (!process.HasExited) { process.Kill(); process.WaitForExit(3000); } }
        catch (InvalidOperationException) { }
        process.Dispose();
    }
}
checks["passed"] = !checks.ContainsKey("error") && checks.Values.OfType<bool>().All(v => v);
File.WriteAllText(Path.Combine(output, "windows-integration.json"), JsonSerializer.Serialize(checks,
    new JsonSerializerOptions { WriteIndented = true }));
return (bool)checks["passed"] ? 0 : 1;

static async Task<int> VerifyTelemetry(string directory)
{
    var output = Path.GetFullPath(directory);
    Directory.CreateDirectory(output);
    var scratch = Path.Combine(output, $"telemetry-{Guid.NewGuid():N}.tmp");
    var checks = new Dictionary<string, object> { ["elevated"] = WindowsMonitor.IsElevated };
    try
    {
        if (!WindowsMonitor.IsElevated) throw new InvalidOperationException("Run the telemetry check elevated.");
        using (var monitor = new WindowsMonitor())
        {
            await Task.Delay(500);
            var initial = monitor.Sample();
            checks["telemetryStatus"] = initial.Machine.TelemetryStatus;
            checks["kernelSessionStarts"] = initial.Machine.DiskBytesPerSecond != null
                && initial.Machine.NetworkBytesPerSecond != null;
            if (!(bool)checks["kernelSessionStarts"])
                throw new InvalidOperationException(initial.Machine.TelemetryStatus);

            var payload = new byte[256 * 1024];
            Random.Shared.NextBytes(payload);
            using (var file = new FileStream(scratch, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.WriteThrough))
            {
                for (var i = 0; i < 32; i++) file.Write(payload);
                file.Flush(true);
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using var sender = new TcpClient(AddressFamily.InterNetwork);
            var connect = sender.ConnectAsync(IPAddress.Loopback,
                ((IPEndPoint)listener.LocalEndpoint).Port, timeout.Token);
            using var receiver = await listener.AcceptTcpClientAsync(timeout.Token);
            await connect;
            var receive = receiver.GetStream().ReadExactlyAsync(new byte[payload.Length * 16], timeout.Token).AsTask();
            for (var i = 0; i < 16; i++)
                await sender.GetStream().WriteAsync(payload, timeout.Token);
            await receive;
            await Task.Delay(1500);

            var sample = monitor.Sample();
            var self = sample.Processes.Single(p => p.Identity.Pid == Environment.ProcessId);
            checks["telemetryStatus"] = sample.Machine.TelemetryStatus;
            checks["diskActivityMeasured"] = sample.Machine.DiskBytesPerSecond > 0;
            checks["diskLatencyMeasured"] = sample.Machine.DiskLatencyMs is >= 0 and < double.PositiveInfinity;
            checks["networkActivityMeasured"] = sample.Machine.NetworkBytesPerSecond > 0;
            checks["ownDiskAttributed"] = self.DiskBytesPerSecond > 0;
            checks["ownNetworkAttributed"] = self.NetworkBytesPerSecond > 0;
            checks["ownDiskBytesPerSecond"] = self.DiskBytesPerSecond ?? -1;
            checks["ownNetworkBytesPerSecond"] = self.NetworkBytesPerSecond ?? -1;
        }
        using (var restarted = new WindowsMonitor())
        {
            await Task.Delay(500);
            var sample = restarted.Sample();
            checks["sessionRestartsAfterDisposal"] = sample.Machine.DiskBytesPerSecond != null
                && sample.Machine.NetworkBytesPerSecond != null;
        }
    }
    catch (Exception ex) { checks["error"] = ex.ToString(); }
    finally { if (File.Exists(scratch)) File.Delete(scratch); }
    checks["passed"] = !checks.ContainsKey("error") && checks.Values.OfType<bool>().All(v => v);
    var json = JsonSerializer.Serialize(checks, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(Path.Combine(output, "elevated-telemetry.json"), json);
    Console.WriteLine(json);
    return (bool)checks["passed"] ? 0 : 1;
}
