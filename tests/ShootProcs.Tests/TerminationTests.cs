using ShootProcs.Core;
using Xunit;

namespace ShootProcs.Tests;

public sealed class TerminationTests
{
    private sealed class FakeTerminator : IProcessTerminator
    {
        public readonly List<ProcessIdentity> Called = [];
        public Func<ProcessSample, TerminationOutcome> Outcome = _ => TerminationOutcome.Terminated;
        public ProcessTermination Terminate(ProcessSample captured)
        {
            Called.Add(captured.Identity);
            return new(captured.Identity, Outcome(captured), "test");
        }
    }

    [Fact]
    public void SafeModeRequiresConfirmationWithoutCallingWindows()
    {
        var adapter = new FakeTerminator();
        var controller = new TerminationController(adapter);
        var result = controller.Execute(new("test", [DiagnosticsTests.Process(10)], true));
        Assert.True(result.ConfirmationRequired);
        Assert.Empty(adapter.Called);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ConfirmedAndImmediateActionsTargetOnlyTheirCapturedIdentities(bool safe, bool confirmed)
    {
        var adapter = new FakeTerminator();
        var captured = DiagnosticsTests.Process(10);
        var result = new TerminationController(adapter).Execute(new("test", [captured, captured], safe), confirmed);
        Assert.Equal(1, result.Killed);
        Assert.Equal([captured.Identity], adapter.Called);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ProtectionAppliesEvenIfTheUserConfirmsOrDisablesSafeMode(bool safe)
    {
        var adapter = new FakeTerminator();
        var result = new TerminationController(adapter).Execute(
            new("test", [DiagnosticsTests.Process(10, protection: "critical")], safe), true);
        Assert.Empty(adapter.Called);
        Assert.Equal(TerminationOutcome.Protected, Assert.Single(result.Processes).Outcome);
        Assert.False(result.Complete);
    }

    [Fact]
    public void PartialFailureAndReplacedPidAreReportedWithoutRetry()
    {
        var adapter = new FakeTerminator
        {
            Outcome = p => p.Identity.Pid == 10 ? TerminationOutcome.AccessDenied : TerminationOutcome.Replaced
        };
        var result = new TerminationController(adapter).Execute(
            new("test", [DiagnosticsTests.Process(10), DiagnosticsTests.Process(11)], false));
        Assert.Equal(0, result.Killed);
        Assert.Equal(1, result.Failed);
        Assert.Equal(2, adapter.Called.Count);
        Assert.Contains(result.Processes, p => p.Outcome == TerminationOutcome.Replaced);
    }
}
