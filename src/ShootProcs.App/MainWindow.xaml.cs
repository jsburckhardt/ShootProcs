using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ShootProcs.Core;
using ShootProcs.Windows;

namespace ShootProcs.App;

public partial class MainWindow : Window
{
    private readonly DiagnosticEngine _engine = new();
    private readonly TerminationController _termination = new(new WindowsTerminator());
    private readonly ObservableCollection<GroupRow> _rows = [];
    private readonly CancellationTokenSource _stop = new();
    private readonly Queue<double> _cpuHistory = [];
    private readonly Queue<double> _memoryHistory = [];
    private readonly Queue<double> _sampleDurations = [];
    private Task? _polling;
    private Diagnosis? _diagnosis;
    private ResourceMode _mode;
    private ApplicationGroup? _selected;
    private TerminationRequest? _pending;
    private string? _pendingKey;
    private DateTime _notificationUntil;
    private bool _executing;
    private int _sampleCount;
    private double _lastSamplingMs;
    private bool _shutdownReady;
    private volatile bool _verificationPause;
    private double _lastApplyMs;
    private readonly bool _verification = Environment.GetCommandLineArgs().Contains("--verify-ui");

    public MainWindow()
    {
        if (_verification)
        {
            var logDirectory = Environment.GetEnvironmentVariable("SHOOTPROCS_VERIFY_DIR") ?? AppContext.BaseDirectory;
            Directory.CreateDirectory(logDirectory);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
            PresentationTraceSources.DataBindingSource.Listeners.Add(new TextWriterTraceListener(
                Path.Combine(logDirectory, "bindings.log")));
            Trace.AutoFlush = true;
        }
        InitializeComponent();
        ExplorerGrid.ItemsSource = _rows;
        var view = CollectionViewSource.GetDefaultView(_rows);
        view.Filter = MatchesFilter;
        view.SortDescriptions.Add(new(nameof(GroupRow.Score), ListSortDirection.Descending));
        if (view is ICollectionViewLiveShaping live)
        {
            live.IsLiveSorting = true;
            live.IsLiveFiltering = true;
            live.LiveFilteringProperties.Add(nameof(GroupRow.Memory));
            live.LiveFilteringProperties.Add(nameof(GroupRow.Cpu));
        }
        Arena.Inspected += Inspect;
        Arena.ShotRequested += RequestShot;
        MotionToggle.IsChecked = !SystemParameters.ClientAreaAnimation;
        Loaded += OnLoaded;
        Closing += OnClosing;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape && _pending != null) CancelPending();
        };
        SourceInitialized += (_, _) =>
        {
            var value = 1;
            DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref value, sizeof(int));
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ElevationButton.Content = WindowsMonitor.IsElevated ? "Running elevated" : "Enable tracing";
        ElevationButton.IsEnabled = !WindowsMonitor.IsElevated;
        _polling = Task.Run(async () =>
        {
            try
            {
                using var monitor = new WindowsMonitor();
                while (!_stop.IsCancellationRequested)
                {
                    if (_verificationPause)
                    {
                        await Task.Delay(200, _stop.Token);
                        continue;
                    }
                    var watch = Stopwatch.StartNew();
                    var snapshot = monitor.Sample();
                    var diagnosis = _engine.Analyze(snapshot);
                    var duration = watch.Elapsed.TotalMilliseconds;
                    await Dispatcher.InvokeAsync(() => Apply(diagnosis, duration));
                    await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(100, 1000 - watch.Elapsed.TotalMilliseconds)), _stop.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => Notify("Monitoring stopped: " + ex.Message, 60));
            }
        });
        if (_verification) _ = VerifyUi();
    }

    private void Apply(Diagnosis diagnosis, double samplingMs)
    {
        var applyWatch = Stopwatch.StartNew();
        _diagnosis = diagnosis;
        _sampleCount++;
        _lastSamplingMs = samplingMs;
        if (_sampleCount > 1) Append(_sampleDurations, samplingMs);
        Headline.Text = diagnosis.Headline + "  /  " + diagnosis.Detail;
        Headline.ToolTip = diagnosis.Detail;
        var machine = diagnosis.Machine;
        CpuValue.Text = $"{machine.CpuPercent:0.0}%";
        MemoryValue.Text = $"{machine.MemoryPressure:P0}";
        MemoryValue.ToolTip = $"{Display.Bytes(machine.TotalMemoryBytes - machine.AvailableMemoryBytes)} used of {Display.Bytes(machine.TotalMemoryBytes)} physical RAM";
        DiskValue.Text = Display.Rate(machine.DiskBytesPerSecond);
        DiskNote.Text = machine.DiskLatencyMs is { } latency
            ? $"{latency:0.0} ms average completion latency"
            : WindowsMonitor.IsElevated ? "Tracing unavailable; see status below" : "Kernel tracing requires elevation";
        DiskNote.ToolTip = machine.TelemetryStatus;
        NetworkValue.Text = Display.Rate(machine.NetworkBytesPerSecond);
        Append(_cpuHistory, machine.CpuPercent);
        Append(_memoryHistory, machine.MemoryPressure * 100);
        CpuChart.Values = _cpuHistory.ToArray();
        MemoryChart.Values = _memoryHistory.ToArray();
        GroupCount.Text = $"{diagnosis.Groups.Count} GROUPS  /  {diagnosis.Groups.Sum(g => g.Processes.Count)} PROCESSES";
        if (_pending == null) Arena.Update(diagnosis.Groups, _mode);
        var existing = _rows.ToDictionary(r => r.Group.Key);
        var alive = diagnosis.Groups.Select(g => g.Key).ToHashSet();
        foreach (var row in _rows.Where(r => !alive.Contains(r.Group.Key)).ToArray()) _rows.Remove(row);
        foreach (var group in diagnosis.Groups)
            if (existing.TryGetValue(group.Key, out var row)) row.Update(group);
            else _rows.Add(new(group));
        if (_selected == null && _pending == null && _sampleCount == 2 && diagnosis.Groups.Count > 0)
            Inspect(diagnosis.Groups.FirstOrDefault(g => g.IsForeground && g.CanTerminate) ?? diagnosis.Groups[0]);
        if (_selected != null && _pending == null)
        {
            var next = diagnosis.Groups.FirstOrDefault(g => g.Key == _selected.Key);
            if (next != null) Inspect(next);
            else
            {
                ShootButton.IsEnabled = false;
                InspectorProtection.Text = "This group exited. Select another target.";
                _selected = null;
            }
        }
        UpdateFieldNote();
        OverheadText.Text = $"SAMPLE {samplingMs:0} ms  /  {DateTime.Now:HH:mm:ss}";
        if (DateTime.UtcNow > _notificationUntil)
        {
            StatusText.Text = machine.TelemetryStatus;
            StatusText.ToolTip = machine.TelemetryStatus;
        }
        _lastApplyMs = applyWatch.Elapsed.TotalMilliseconds;
    }

    private static void Append(Queue<double> history, double value)
    {
        history.Enqueue(value);
        while (history.Count > 45) history.Dequeue();
    }

    private void Inspect(ApplicationGroup group)
    {
        if (_pending != null) return;
        _selected = group;
        InspectorIcon.Source = IconCache.Get(group.Executable);
        InspectorName.Text = group.Name;
        InspectorName.ToolTip = group.Executable;
        InspectorCount.Text = $"{group.Processes.Count} processes / {(group.IsForeground ? "windowed application" : "background group")}";
        InspectorScore.Text = $"{group.Impact.Score:0} / 100";
        InspectorChart.Values = group.History;
        InspectorCpu.Text = Display.Percent(group.CpuPercent);
        InspectorMemory.Text = Display.Bytes(group.PrivateBytes);
        InspectorMemory.ToolTip = "Private committed memory; not exclusive physical RAM. Shared working-set pages are not added as RAM ownership.";
        InspectorDisk.Text = Display.Rate(group.DiskBytesPerSecond);
        InspectorNetwork.Text = Display.Rate(group.NetworkBytesPerSecond);
        InspectorReason.Text = group.Impact.Reason;
        InspectorConfidence.Text = group.Impact.Confidence;
        InspectorProtection.Text = group.ProtectionReason ?? "All included windows and instances are in scope.";
        ShootButton.IsEnabled = group.CanTerminate && !_executing;
        ActionScope.Text = $"{group.Processes.Count} captured process identities. Direct termination can discard unsaved work.";
    }

    private void RequestShot(ApplicationGroup group) =>
        BeginShot(TerminationRequest.Capture(group, SafeToggle.IsChecked == true), group.Key);

    private void BeginShot(TerminationRequest request, string? key)
    {
        if (_executing || _pending != null) return;
        if (request.Processes.Any(p => p.ProtectionReason != null))
        {
            Notify("Protected: " + request.Processes.First(p => p.ProtectionReason != null).ProtectionReason);
            return;
        }
        if (request.SafeMode)
        {
            _pending = request;
            _pendingKey = key;
            Arena.Frozen = true;
            ConfirmationTitle.Text = $"End {request.Name}?";
            ConfirmationDetail.Text = $"Terminate exactly {request.Processes.Count} captured processes.\n\n" +
                "PIDs: " + string.Join(", ", request.Processes.Take(24).Select(p => p.Identity.Pid)) +
                (request.Processes.Count > 24 ? ", ..." : "");
            ConfirmationOverlay.Visibility = Visibility.Visible;
            CancelShotButton.Focus();
        }
        else _ = ExecuteShot(request, key, false);
    }

    private async Task ExecuteShot(TerminationRequest request, string? key, bool confirmed)
    {
        _executing = true;
        ShootButton.IsEnabled = false;
        try
        {
            var result = await Task.Run(() => _termination.Execute(request, confirmed));
            if (result.ConfirmationRequired) { Notify("Confirmation required; no processes terminated."); return; }
            var skipped = result.Processes.Count - result.Killed - result.Failed;
            var message = $"{request.Name}: {result.Killed} terminated, {skipped} exited/replaced, {result.Failed} failed.";
            if (result.Failed > 0) message += " " + string.Join("; ", result.Processes
                .Where(p => p.Outcome is TerminationOutcome.Protected or TerminationOutcome.Failed or TerminationOutcome.AccessDenied)
                .Select(p => $"PID {p.Identity.Pid}: {p.Detail}"));
            if (result.Killed > 0 && result.Failed == 0 && key != null) Arena.Celebrate(key);
            Notify(message, 25);
        }
        finally
        {
            _executing = false;
            if (_selected != null) ShootButton.IsEnabled = _selected.CanTerminate;
        }
    }

    private void CancelPending()
    {
        _pending = null;
        _pendingKey = null;
        Arena.Frozen = false;
        ConfirmationOverlay.Visibility = Visibility.Collapsed;
        if (_selected != null) ShootButton.Focus();
    }
    private void CancelShot(object sender, RoutedEventArgs e) => CancelPending();
    private void ConfirmShot(object sender, RoutedEventArgs e)
    {
        var request = _pending;
        var key = _pendingKey;
        CancelPending();
        if (request != null) _ = ExecuteShot(request, key, true);
    }
    private void ShootSelected(object sender, RoutedEventArgs e)
    {
        if (_selected != null) RequestShot(_selected);
    }
    private void EndProcess(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ProcessSample process })
            BeginShot(new(process.Name, [process], SafeToggle.IsChecked == true), null);
        e.Handled = true;
    }
    private void SafeChanged(object sender, RoutedEventArgs e)
    {
        if (SafeLabel == null) return;
        SafeLabel.Text = SafeToggle.IsChecked == true ? "SAFE MODE  /  ON" : "SAFE MODE  /  OFF - INSTANT";
        SafeLabel.Foreground = SafeToggle.IsChecked == true ? Brush("#6BF4C5") : Brush("#FFB797");
        UpdateFieldNote();
    }
    private void ModeChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button || button.Tag is not string mode) return;
        _mode = Enum.Parse<ResourceMode>(mode);
        foreach (var toggle in Modes.Children.OfType<ToggleButton>()) toggle.IsChecked = toggle == button;
        if (_diagnosis != null) Arena.Update(_diagnosis.Groups, _mode);
        UpdateFieldNote();
    }
    private void MotionChanged(object sender, RoutedEventArgs e)
    {
        if (Arena != null) Arena.ReducedMotion = MotionToggle.IsChecked == true;
    }
    private void ShowArena(object sender, RoutedEventArgs e) => SetView(false);
    private void ShowExplorer(object sender, RoutedEventArgs e) => SetView(true);
    private void SetView(bool explorer)
    {
        ArenaNav.IsChecked = !explorer;
        ExplorerNav.IsChecked = explorer;
        ArenaFrame.Visibility = ArenaToolbar.Visibility = explorer ? Visibility.Collapsed : Visibility.Visible;
        ExplorerFrame.Visibility = ExplorerToolbar.Visibility = explorer ? Visibility.Visible : Visibility.Collapsed;
        PageTitle.Text = explorer ? "Every process. In perspective." : "Your machine. Your move.";
        if (explorer) SearchBox.Focus();
    }
    private bool MatchesFilter(object item)
    {
        if (item is not GroupRow row) return false;
        var search = SearchBox?.Text.Trim() ?? "";
        var matches = search.Length == 0 || row.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
            || row.Group.Executable.Contains(search, StringComparison.OrdinalIgnoreCase)
            || row.Processes.Any(p => p.Identity.Pid.ToString() == search
                || p.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || p.Executable.Contains(search, StringComparison.OrdinalIgnoreCase));
        var relevant = BackgroundToggle?.IsChecked == true || row.Group.IsForeground
            || row.Memory >= 100 * 1024 * 1024 || row.Cpu >= 1 || search.Length > 0;
        return matches && relevant;
    }
    private void FilterChanged(object sender, RoutedEventArgs e)
    {
        if (ExplorerGrid?.ItemsSource != null)
            CollectionViewSource.GetDefaultView(_rows).Refresh();
    }
    private void ExplorerSelected(object sender, SelectionChangedEventArgs e)
    {
        if (ExplorerGrid.SelectedItem is GroupRow row) Inspect(row.Group);
    }
    private void ExpandGroup(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        var row = FindParent<DataGridRow>(button);
        if (row == null) return;
        var expand = row.DetailsVisibility != Visibility.Visible;
        row.DetailsVisibility = expand ? Visibility.Visible : Visibility.Collapsed;
        button.Content = expand ? "-" : "+";
        e.Handled = true;
    }
    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        for (var parent = VisualTreeHelper.GetParent(child); parent != null; parent = VisualTreeHelper.GetParent(parent))
            if (parent is T result) return result;
        return null;
    }
    private void Elevate(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = Environment.ProcessPath ?? throw new InvalidOperationException("Executable path unavailable.");
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, Verb = "runas" });
            Close();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        { Notify("Elevation was not completed: " + ex.Message); }
    }
    private void OpenRepository(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/jsburckhardt/ShootProcs")
            { UseShellExecute = true, Verb = "open" });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        { Notify("Could not open the repository: " + ex.Message); }
    }
    private void UpdateFieldNote()
    {
        if (FieldNote == null) return;
        var omitted = Math.Max(0, (_diagnosis?.Groups.Count ?? 0) - Arena.TargetCount);
        FieldNote.Text = $"{Arena.TargetCount} targets / {omitted} more in Explorer. " +
            (_mode is ResourceMode.Disk or ResourceMode.Network && _diagnosis?.Machine.DiskBytesPerSecond is null
                ? WindowsMonitor.IsElevated ? "Telemetry unavailable; see status below." : "Telemetry unavailable; enable tracing."
                : SafeToggle.IsChecked == true ? "Click to shoot; confirmation ON." : "Click to shoot; INSTANT termination.");
    }
    private void Notify(string message, int seconds = 12)
    {
        _notificationUntil = DateTime.UtcNow.AddSeconds(seconds);
        StatusText.Text = message;
        StatusText.ToolTip = message;
    }
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_shutdownReady) return;
        e.Cancel = true;
        IsEnabled = false;
        _stop.Cancel();
        if (_polling != null) await _polling;
        _shutdownReady = true;
        Close();
    }
    private static SolidColorBrush Brush(string hex) => (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;

    private async Task VerifyUi()
    {
        var output = Environment.GetEnvironmentVariable("SHOOTPROCS_VERIFY_DIR")
            ?? Path.Combine(AppContext.BaseDirectory, "verification");
        Directory.CreateDirectory(output);
        var checks = new Dictionary<string, object>();
        try
        {
            for (var i = 0; i < 80 && _sampleCount < 3; i++) await Task.Delay(250);
            checks["liveSamples"] = _sampleCount;
            checks["groups"] = _diagnosis?.Groups.Count ?? 0;
            checks["targets"] = Arena.TargetCount;
            checks["safeDefault"] = SafeToggle.IsChecked == true;
            checks["repositoryLinkRendered"] = RepositoryButton.IsVisible && RepositoryButton.IsEnabled
                && RepositoryButton.ActualWidth >= 24 && RepositoryButton.ActualHeight >= 24
                && RepositoryButton.Focusable
                && RepositoryButton.Content is Viewbox { Child: System.Windows.Shapes.Path { Data: not null } };
            checks["samplingMs"] = _lastSamplingMs;
            checks["nativeRenderTier"] = RenderCapability.Tier >> 16;
            checks["telemetry"] = _diagnosis?.Machine.TelemetryStatus ?? "";
            checks["cpuMeasured"] = _diagnosis?.Groups.Any(g => g.CpuPercent != null) == true;
            checks["memoryMeasured"] = _diagnosis?.Groups.Any(g => g.PrivateBytes > 0) == true;
            if (WindowsMonitor.IsElevated)
            {
                checks["elevatedDiskRendered"] = _diagnosis?.Machine.DiskBytesPerSecond != null
                    && DiskValue.Text != "Unavailable" && DiskNote.Text.Contains("ms average completion latency");
                checks["elevatedNetworkRendered"] = _diagnosis?.Machine.NetworkBytesPerSecond != null
                    && NetworkValue.Text != "Unavailable";
            }
            foreach (var toggle in Modes.Children.OfType<ToggleButton>())
                toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            checks["resourceModesSwitch"] = _mode == ResourceMode.Network
                && Arena.Children.OfType<ArenaTarget>().All(t => t.Mode == ResourceMode.Network);
            Modes.Children.OfType<ToggleButton>().First().RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            Capture(Path.Combine(output, "arena.png"));
            SetView(true);
            BackgroundToggle.IsChecked = true;
            ExplorerGrid.UpdateLayout();
            await Task.Delay(1200);
            checks["explorerRows"] = ExplorerGrid.Items.Count;
            if (_rows.Count > 0)
            {
                ExplorerGrid.SelectedItem = _rows[0];
                checks["inspectorSelection"] = InspectorName.Text == _rows[0].Name;
            }
            SearchBox.Text = "___no_such_application___";
            checks["filterExcludes"] = ExplorerGrid.Items.Count == 0;
            SearchBox.Text = Environment.ProcessId.ToString();
            checks["searchFindsProcessId"] = ExplorerGrid.Items.Cast<GroupRow>()
                .Any(r => r.Processes.Any(p => p.Identity.Pid == Environment.ProcessId));
            SearchBox.Text = "";
            checks["filterRestores"] = ExplorerGrid.Items.Count > 0;
            ExplorerGrid.Items.SortDescriptions.Clear();
            ExplorerGrid.Items.SortDescriptions.Add(new(nameof(GroupRow.Memory), ListSortDirection.Descending));
            ExplorerGrid.UpdateLayout();
            checks["sortMemory"] = ExplorerGrid.Items.Cast<GroupRow>().Select(r => r.Memory)
                .SequenceEqual(ExplorerGrid.Items.Cast<GroupRow>().Select(r => r.Memory).OrderDescending());
            await Task.Delay(600);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            var renderedRows = Descendants(ExplorerGrid).OfType<DataGridRow>()
                .Where(r => r.IsVisible && r.DataContext is GroupRow).Take(4).ToArray();
            checks["actualExplorerCellsRendered"] = renderedRows.Length >= 3 && renderedRows.All(row =>
            {
                var texts = Descendants(row).OfType<TextBlock>().Select(t => t.Text).ToArray();
                var model = (GroupRow)row.DataContext;
                return texts.Contains(model.Name) && texts.Contains(model.MemoryText);
            });
            checks["renderedExplorerText"] = Descendants(ExplorerGrid).OfType<TextBlock>()
                .Select(t => t.Text).Where(t => !string.IsNullOrEmpty(t)).Take(35).ToArray();
            checks["rowDataContexts"] = Descendants(ExplorerGrid).OfType<DataGridRow>()
                .Take(2).Select(r => r.DataContext?.GetType().FullName ?? "null").ToArray();
            var expandable = renderedRows.First();
            var expand = Descendants(expandable).OfType<Button>().First(b => b.Content?.ToString() == "+");
            expand.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ExplorerGrid.UpdateLayout();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            checks["expandedProcessesRendered"] = expandable.DetailsVisibility == Visibility.Visible
                && Descendants(expandable).OfType<Button>().Any(b => b.DataContext is ProcessSample);
            expand.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            checks["groupCollapses"] = expandable.DetailsVisibility == Visibility.Collapsed;
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            Capture(Path.Combine(output, "explorer.png"));
            SafeToggle.IsChecked = false;
            checks["safeOff"] = SafeLabel.Text.Contains("INSTANT");
            SafeToggle.IsChecked = true;
            MotionToggle.IsChecked = true;
            checks["reducedMotion"] = Arena.ReducedMotion;
            SetView(false);
            await VerifyTerminationUi(checks, output);
            MotionToggle.IsChecked = false;
            using (var self = Process.GetCurrentProcess())
            {
                var cpuStart = self.TotalProcessorTime;
                var wall = Stopwatch.StartNew();
                await Task.Delay(3000);
                self.Refresh();
                checks["postInteractionCpuMachinePercent"] = (self.TotalProcessorTime - cpuStart).TotalSeconds
                    / wall.Elapsed.TotalSeconds / Environment.ProcessorCount * 100;
                checks["postInteractionPrivateCommitBytes"] = self.PrivateMemorySize64;
                MotionToggle.IsChecked = true;
                self.Refresh();
                var reducedStart = self.TotalProcessorTime;
                wall.Restart();
                await Task.Delay(3000);
                self.Refresh();
                checks["reducedMotionCpuMachinePercent"] = (self.TotalProcessorTime - reducedStart).TotalSeconds
                    / wall.Elapsed.TotalSeconds / Environment.ProcessorCount * 100;
                _verificationPause = true;
                await Task.Delay(400);
                self.Refresh();
                var staticStart = self.TotalProcessorTime;
                wall.Restart();
                await Task.Delay(3000);
                self.Refresh();
                checks["staticUiCpuMachinePercent"] = (self.TotalProcessorTime - staticStart).TotalSeconds
                    / wall.Elapsed.TotalSeconds / Environment.ProcessorCount * 100;
                checks["lastUiApplyMs"] = _lastApplyMs;
                _verificationPause = false;
            }
            checks["meanWarmSampleMs"] = _sampleDurations.Count > 0 ? _sampleDurations.Average() : 0;
            checks["samplesObservedAtExit"] = _sampleCount;
            checks["passed"] = _sampleCount >= 3 && Arena.TargetCount is > 0 and <= 24
                && checks.Where(kv => kv.Value is bool && kv.Key != "passed").All(kv => (bool)kv.Value);
        }
        catch (Exception ex) { checks["passed"] = false; checks["error"] = ex.ToString(); }
        File.WriteAllText(Path.Combine(output, "ui-verification.json"), JsonSerializer.Serialize(checks,
            new JsonSerializerOptions { WriteIndented = true }));
        Close();
    }

    private async Task VerifyTerminationUi(Dictionary<string, object> checks, string output)
    {
        var fixture = Environment.GetEnvironmentVariable("SHOOTPROCS_FIXTURE_EXE");
        if (string.IsNullOrEmpty(fixture))
        {
            checks["terminationUi"] = "Not requested; set SHOOTPROCS_FIXTURE_EXE to the isolated validation executable.";
            return;
        }
        if (!File.Exists(fixture) || Path.GetFileName(fixture) != "ShootProcs.Validation.exe")
            throw new InvalidOperationException("The disposable UI fixture executable is invalid.");
        var workers = new List<Process>();
        Process Start()
        {
            var process = Process.Start(new ProcessStartInfo(fixture, "--worker")
            { UseShellExecute = false, CreateNoWindow = true })!;
            workers.Add(process);
            return process;
        }
        async Task WaitUntil(Func<bool> predicate)
        {
            for (var i = 0; i < 80; i++)
            {
                if (predicate()) return;
                await Task.Delay(150);
            }
            throw new TimeoutException("Timed out waiting for a disposable UI validation condition.");
        }
        try
        {
            var first = Start();
            var second = Start();
            await WaitUntil(() => _diagnosis?.Groups.Any(g =>
                g.Processes.Any(p => p.Identity.Pid == first.Id) && g.Processes.Count == 2) == true);
            var group = _diagnosis!.Groups.Single(g => g.Processes.Any(p => p.Identity.Pid == first.Id));
            if (!group.Processes.Select(p => p.Identity.Pid).ToHashSet().SetEquals(workers.Select(p => p.Id)))
                throw new InvalidOperationException("Fixture identity guard rejected a group containing unowned processes.");
            checks["fixturePids"] = workers.Select(p => p.Id).ToArray();
            Arena.Update([group], ResourceMode.Overall);
            MotionToggle.IsChecked = false;
            Arena.UpdateLayout();
            var target = Arena.Children.OfType<ArenaTarget>().Single();
            Activate();
            checks["keyboardInspection"] = target.Focus();
            checks["inspectorShowsLiveFixture"] = InspectorName.Text == group.Name;
            target.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0)
            { RoutedEvent = MouseEnterEvent });
            checks["hoverEventInspection"] = _selected?.Key == group.Key;
            checks["circularHitTesting"] = target.InputHitTest(new(0, 0)) != target
                && target.InputHitTest(new(target.ActualWidth / 2, target.ActualHeight / 2)) == target;
            var before = new Point(Canvas.GetLeft(target) + target.Motion.X, Canvas.GetTop(target) + target.Motion.Y);
            await Task.Delay(200);
            checks["focusedTargetStable"] = before == new Point(Canvas.GetLeft(target) + target.Motion.X, Canvas.GetTop(target) + target.Motion.Y);
            target.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            checks["safeShotShowsConfirmation"] = ConfirmationOverlay.Visibility == Visibility.Visible
                && _pending?.Processes.Count == 2 && !first.HasExited && !second.HasExited;
            checks["confirmationFreezesArena"] = Arena.Frozen;
            UpdateFieldNote();
            ConfirmationOverlay.UpdateLayout();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            var modalPoint = ConfirmShotButton.TranslatePoint(
                new(ConfirmShotButton.ActualWidth / 2, ConfirmShotButton.ActualHeight / 2), this);
            var modalHit = InputHitTest(modalPoint) as DependencyObject;
            checks["confirmationRenderedAndHitTestable"] = ConfirmationOverlay.IsVisible
                && modalHit != null && (modalHit == ConfirmShotButton || FindParent<Button>(modalHit) == ConfirmShotButton);
            var backgroundPoint = SafeToggle.TranslatePoint(
                new(SafeToggle.ActualWidth / 2, SafeToggle.ActualHeight / 2), this);
            checks["confirmationBlocksUnderlyingControls"] = InputHitTest(backgroundPoint) == ConfirmationOverlay;
            Capture(Path.Combine(output, "confirmation.png"));
            checks["readmeCaptureContainsOnlyOwnedFixture"] = Arena.Children.OfType<ArenaTarget>()
                .SelectMany(t => t.Group.Processes).Select(p => p.Identity.Pid).ToHashSet()
                .SetEquals(workers.Select(p => p.Id))
                && InspectorName.Text == group.Name;
            if (!(bool)checks["readmeCaptureContainsOnlyOwnedFixture"])
                throw new InvalidOperationException("README capture refused unowned application details.");
            try
            {
                ConfirmationOverlay.Visibility = Visibility.Collapsed;
                UpdateLayout();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                Capture(Path.Combine(output, "readme-arena.png"));
            }
            finally { ConfirmationOverlay.Visibility = Visibility.Visible; }
            CancelShotButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            checks["cancelKeepsApplicationAlive"] = _pending == null && !Arena.Frozen && !first.HasExited && !second.HasExited;
            RequestShot(group);
            var third = Start();
            var sample = _sampleCount;
            await WaitUntil(() => _sampleCount >= sample + 2);
            checks["confirmationMembershipStaysCaptured"] = _pending?.Processes.Count == 2;
            ConfirmShotButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitUntil(() => !_executing);
            checks["confirmedUiShotTerminatesCapturedGroup"] = first.HasExited && second.HasExited && !third.HasExited;
            checks["terminationOutcomeDisplayed"] = StatusText.Text.Contains("2 terminated");
            checks["successfulShotAnimates"] = Arena.HasActiveEffects;
            Arena.InvalidateVisual();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            Capture(Path.Combine(output, "destruction.png"));
            await WaitUntil(() => _diagnosis!.Groups.Any(g => g.Processes.Any(p => p.Identity.Pid == third.Id)));
            var remaining = _diagnosis!.Groups.Single(g => g.Processes.Any(p => p.Identity.Pid == third.Id));
            var process = remaining.Processes.Single(p => p.Identity.Pid == third.Id);
            SafeToggle.IsChecked = false;
            EndProcess(new Button { DataContext = process }, new RoutedEventArgs(Button.ClickEvent));
            checks["immediateUiShotHasNoDialog"] = _pending == null && ConfirmationOverlay.Visibility == Visibility.Collapsed;
            await WaitUntil(() => !_executing);
            checks["immediateUiShotTerminatesSingleProcess"] = third.HasExited;
            SafeToggle.IsChecked = true;
            var self = _diagnosis.Groups.Single(g => g.Processes.Any(p => p.Identity.Pid == Environment.ProcessId));
            Inspect(self);
            checks["protectedShootButtonDisabled"] = !ShootButton.IsEnabled;
            RequestShot(self);
            checks["protectedShotRejectedWithoutDialog"] = _pending == null && StatusText.Text.StartsWith("Protected:");
            await WaitUntil(() => !_diagnosis.Groups.Any(g => g.Processes.Any(p => workers.Any(w => w.Id == p.Identity.Pid))));
            checks["exitedFixtureRemovedOnRefresh"] = true;
            MotionToggle.IsChecked = true;
            var beforeSample = _sampleCount;
            await WaitUntil(() => _sampleCount > beforeSample);
            Arena.Update(_diagnosis.Groups, ResourceMode.Overall);
            Arena.UpdateLayout();
            var stable = Arena.Children.OfType<ArenaTarget>().First();
            await Task.Delay(60);
            var stationary = new Point(Canvas.GetLeft(stable) + stable.Motion.X, Canvas.GetTop(stable) + stable.Motion.Y);
            await Task.Delay(200);
            checks["reducedMotionStopsOrbit"] = stationary == new Point(Canvas.GetLeft(stable) + stable.Motion.X, Canvas.GetTop(stable) + stable.Motion.Y);
        }
        finally
        {
            CancelPending();
            foreach (var process in workers)
            {
                try { if (!process.HasExited) { process.Kill(); process.WaitForExit(3000); } }
                catch (InvalidOperationException) { }
                process.Dispose();
            }
        }
    }

    private void Capture(string path)
    {
        var bitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(this);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
