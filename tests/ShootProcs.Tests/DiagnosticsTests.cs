using ShootProcs.Core;
using Xunit;

namespace ShootProcs.Tests;

public sealed class DiagnosticsTests
{
    private static readonly DateTimeOffset Epoch = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
    private const long GiB = 1024L * 1024 * 1024;
    internal static ProcessSample Process(int pid, string path = @"C:\Apps\Code\Code.exe",
        int parent = 0, string owner = "user", int session = 1, string? profile = null,
        double? cpu = 10, long? memory = GiB, string? protection = null) =>
        new(new(pid, 100 + pid), parent, Path.GetFileNameWithoutExtension(path), path, owner, session,
            profile, true, cpu, memory, memory, null, null, protection);

    private static MonitorSnapshot Snapshot(int second, ProcessSample[] processes,
        double cpu = 20, double memoryPressure = .4, double? disk = null, double? latency = null) =>
        new(new(Epoch.AddSeconds(second), cpu, 16 * GiB, (long)(16 * GiB * (1 - memoryPressure)),
            disk, latency, null, "test"), processes);

    [Fact]
    public void SameInstallationAggregatesInstancesAndObservedHelpersWithoutDuplicates()
    {
        var root = Process(10);
        var second = Process(11);
        var helper = Process(12, @"C:\Apps\Code\bin\helper.exe", parent: 10);
        var group = Assert.Single(new DiagnosticEngine().Analyze(Snapshot(0, [root, second, helper, helper])).Groups);
        Assert.Equal(3, group.Processes.Count);
        Assert.Equal(30, group.CpuPercent);
        Assert.Equal(3 * GiB, group.PrivateBytes);
    }

    [Fact]
    public void MatchingExecutableNamesDoNotMergeDifferentInstallations()
    {
        var diagnosis = new DiagnosticEngine().Analyze(Snapshot(0,
            [Process(10), Process(11, @"D:\Portable\Code\Code.exe")]));
        Assert.Equal(2, diagnosis.Groups.Count);
    }

    [Fact]
    public void ProfilesOwnersAndSessionsFormDistinctGroups()
    {
        var diagnosis = new DiagnosticEngine().Analyze(Snapshot(0,
        [
            Process(10, profile: "Profile 1"), Process(11, profile: "Profile 2"),
            Process(12, owner: "other"), Process(13, session: 2), Process(14)
        ]));
        Assert.Equal(5, diagnosis.Groups.Count);
    }

    [Fact]
    public void RecycledParentPidCannotCreateAFalseAncestry()
    {
        var helper = Process(10, @"C:\Apps\Code\bin\helper.exe", parent: 20);
        var newerParent = Process(20);
        Assert.Equal(2, new DiagnosticEngine().Analyze(Snapshot(0, [helper, newerParent])).Groups.Count);
    }

    [Fact]
    public void EmbeddedWebViewJoinsItsObservedHostButNotTheDesktopShell()
    {
        var host = Process(10, @"C:\Apps\Host\host.exe");
        var webview = Process(11, @"C:\Shared\WebView\msedgewebview2.exe", parent: 10);
        var group = Assert.Single(new DiagnosticEngine().Analyze(Snapshot(0, [host, webview])).Groups);
        Assert.Equal(host.Executable, group.Executable);
        var shell = Process(10, @"C:\Windows\explorer.exe");
        Assert.Equal(2, new DiagnosticEngine().Analyze(Snapshot(0, [shell, webview])).Groups.Count);
    }

    [Fact]
    public void ExplicitDifferentProfilesAreNotLostThroughParentAncestry()
    {
        var parent = Process(10, profile: "Profile 1");
        var child = Process(11, parent: 10, profile: "Profile 2");
        Assert.Equal(2, new DiagnosticEngine().Analyze(Snapshot(0, [parent, child])).Groups.Count);
    }

    [Fact]
    public void IndependentRuntimeJobsStaySeparateWhileTheirWorkersAndApplicationHostsGroup()
    {
        var node = @"C:\Program Files\nodejs\node.exe";
        var first = Process(10, node);
        var worker = Process(11, node, parent: 10);
        var independent = Process(12, node);
        var groups = new DiagnosticEngine().Analyze(Snapshot(0, [first, worker, independent])).Groups;
        Assert.Equal(2, groups.Count);
        Assert.Contains(groups, g => g.Processes.Count == 2 && g.Name.Contains("job 10"));
        var code = Process(20);
        var hosted = Process(21, node, parent: 20);
        var hostGroup = Assert.Single(new DiagnosticEngine().Analyze(Snapshot(0, [code, hosted])).Groups);
        Assert.Equal(code.Executable, hostGroup.Executable);
        var shell = Process(20, @"C:\Program Files\PowerShell\pwsh.exe");
        Assert.Equal(2, new DiagnosticEngine().Analyze(Snapshot(0, [shell, hosted])).Groups.Count);
    }

    [Fact]
    public void UnknownIdentitiesStaySeparateAndIncompleteMetricsRemainUnavailable()
    {
        var unknown = Process(10, path: "", owner: "", cpu: null, memory: null);
        var groups = new DiagnosticEngine().Analyze(Snapshot(0, [unknown, unknown with { Identity = new(11, 111) }],
            disk: 0, latency: 0)).Groups;
        Assert.Equal(2, groups.Count);
        Assert.All(groups, g =>
        {
            Assert.Null(g.CpuPercent);
            Assert.Null(g.PrivateBytes);
            Assert.Null(g.DiskBytesPerSecond);
            Assert.Contains("Partial", g.Impact.Confidence);
        });
    }

    [Fact]
    public void ANewUnavailableMemberDoesNotBecomeAFalseMeasuredZero()
    {
        var group = Assert.Single(new DiagnosticEngine().Analyze(Snapshot(0,
            [Process(10), Process(11, cpu: null)])).Groups);
        Assert.Null(group.CpuPercent);
        Assert.Equal(2 * GiB, group.PrivateBytes);
    }

    [Fact]
    public void HealthyMachineDoesNotVilifyLargeApplications()
    {
        var engine = new DiagnosticEngine();
        Diagnosis? result = null;
        for (var i = 0; i < 20; i++)
            result = engine.Analyze(Snapshot(i, [Process(10, cpu: 20, memory: 8 * GiB)], cpu: 30, memoryPressure: .6));
        Assert.Equal("No significant pressure", result!.Headline);
        Assert.Equal(0, Assert.Single(result.Groups).Impact.Score);
        Assert.Contains("Consumption alone", result.Groups[0].Impact.Reason);
    }

    [Fact]
    public void SustainedCpuPressureRanksContributorsAndOutweighsABriefSpike()
    {
        var engine = new DiagnosticEngine();
        var processes = new[] { Process(10, cpu: 65), Process(11, @"C:\Other\app.exe", cpu: 2) };
        var first = engine.Analyze(Snapshot(0, processes, cpu: 90));
        Diagnosis result = first;
        for (var i = 1; i < 20; i++) result = engine.Analyze(Snapshot(i, processes, cpu: 90));
        Assert.True(result.Groups[0].Impact.Score > first.Groups[0].Impact.Score * 3);
        Assert.True(result.Groups[0].Impact.Score > result.Groups[1].Impact.Score);
        Assert.Contains("CPU share", result.Groups[0].Impact.Reason);
    }

    [Fact]
    public void MemoryPressureAndMeasuredDiskLatencyContributeWithoutExceedingBound()
    {
        var engine = new DiagnosticEngine();
        var expensive = Process(10, cpu: 60, memory: 8 * GiB) with { DiskBytesPerSecond = 2_000_000 };
        Diagnosis? result = null;
        for (var i = 0; i < 40; i++)
            result = engine.Analyze(Snapshot(i, [expensive], cpu: 90, memoryPressure: .94, disk: 2_000_000, latency: 45));
        var impact = Assert.Single(result!.Groups).Impact;
        Assert.InRange(impact.Score, 90, 100);
        Assert.Contains("memory pressure", impact.Reason);
        Assert.Contains("disk traffic", impact.Reason);
    }

    [Fact]
    public void HighNetworkTrafficAloneIsNotEvidenceOfSlowdown()
    {
        var process = Process(10) with { NetworkBytesPerSecond = 1_000_000_000 };
        var group = Assert.Single(new DiagnosticEngine().Analyze(Snapshot(0, [process])).Groups);
        Assert.Equal(0, group.Impact.Score);
    }

    [Fact]
    public void RefreshRemovesExitedGroupsAndDoesNotKeepTheirScoreOnReappearance()
    {
        var engine = new DiagnosticEngine();
        engine.Analyze(Snapshot(0, [Process(10)], cpu: 95));
        Assert.Empty(engine.Analyze(Snapshot(1, [])).Groups);
        var replacement = Assert.Single(engine.Analyze(Snapshot(2, [Process(10)], cpu: 20)).Groups);
        Assert.Equal(0, replacement.Impact.Score);
        Assert.Single(replacement.History);
    }

    [Fact]
    public void ArenaIsBoundedAndPinnedTargetsSurviveAChallenger()
    {
        var groups = new DiagnosticEngine().Analyze(Snapshot(0, Enumerable.Range(1, 25)
            .Select(i => Process(i, $@"C:\App{i}\app.exe", cpu: i)).ToArray())).Groups;
        var selection = new ArenaSelection();
        var first = selection.Select(groups.Take(24).ToArray(), ResourceMode.CPU);
        var pinned = first.MinBy(g => g.CpuPercent)!;
        var challenger = groups.Last() with { Key = "challenger", CpuPercent = 100 };
        var next = selection.Select([.. groups.Take(24), challenger], ResourceMode.CPU, pinned.Key);
        Assert.Equal(24, next.Count);
        Assert.Contains(next, g => g.Key == pinned.Key);
        Assert.Contains(next, g => g.Key == "challenger");
    }

    [Fact]
    public void ArenaDoesNotReplaceTargetsForATinySpike()
    {
        var engine = new DiagnosticEngine();
        var groups = engine.Analyze(Snapshot(0, [Process(10, cpu: 10), Process(11, @"C:\Other\app.exe", cpu: 10.1)])).Groups;
        var selection = new ArenaSelection();
        var first = selection.Select([groups[0]], ResourceMode.CPU, limit: 1);
        var next = selection.Select([groups[0], groups[1]], ResourceMode.CPU, limit: 1);
        Assert.Equal(first[0].Key, next[0].Key);
    }
}
