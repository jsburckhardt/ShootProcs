using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ShootProcs.Core;

namespace ShootProcs.App;

public sealed class ArenaControl : Canvas
{
    private readonly Dictionary<string, ArenaTarget> _targets = [];
    private readonly List<Burst> _bursts = [];
    private readonly ArenaSelection _selection = new();
    private DateTime _started = DateTime.UtcNow;
    private readonly DispatcherTimer _animation = new(DispatcherPriority.Background)
    { Interval = TimeSpan.FromMilliseconds(33) };
    private ResourceMode _mode;
    private string? _pinned;
    private bool _frozen;
    private BitmapSource? _backdrop;
    private bool _reducedMotion = !SystemParameters.ClientAreaAnimation;
    public bool ReducedMotion
    {
        get => _reducedMotion;
        set { _reducedMotion = value; UpdateAnimation(); }
    }
    public int TargetCount => _targets.Count;
    internal bool HasActiveEffects => _bursts.Count > 0;
    public bool Frozen { get => _frozen; set { _frozen = value; UpdateAnimation(); } }
    public event Action<ApplicationGroup>? Inspected;
    public event Action<ApplicationGroup>? ShotRequested;

    private sealed record Burst(Point Center, double Radius, DateTime Started);

    public ArenaControl()
    {
        ClipToBounds = true;
        Background = new SolidColorBrush(Color.FromRgb(12, 21, 33));
        _animation.Tick += RenderFrame;
        Loaded += (_, _) => UpdateAnimation();
        Unloaded += (_, _) => _animation.Stop();
        IsVisibleChanged += (_, _) => UpdateAnimation();
        SizeChanged += (_, _) =>
        {
            CacheBackdrop();
            ArrangeTargets();
            InvalidateVisual();
        };
        Focusable = false;
        AutomationProperties.SetName(this, "Live application Arena");
    }

    private void UpdateAnimation()
    {
        if (IsLoaded && IsVisible && !ReducedMotion && !Frozen) _animation.Start();
        else _animation.Stop();
    }

    public void Update(IReadOnlyList<ApplicationGroup> groups, ResourceMode mode)
    {
        _mode = mode;
        var selected = _selection.Select(groups, mode, _pinned);
        var keys = selected.Select(g => g.Key).ToHashSet();
        foreach (var key in _targets.Keys.Where(k => !keys.Contains(k)).ToArray())
        {
            Children.Remove(_targets[key]);
            _targets.Remove(key);
        }
        var maxWeight = Math.Max(1, selected.Select(g => ArenaSelection.Weight(g, mode)).DefaultIfEmpty(1).Max());
        foreach (var group in selected)
        {
            if (!_targets.TryGetValue(group.Key, out var target))
            {
                target = new ArenaTarget(group)
                {
                    Slot = Enumerable.Range(0, 24).First(slot => _targets.Values.All(t => t.Slot != slot))
                };
                target.MouseEnter += (_, _) => Inspect(target);
                target.GotKeyboardFocus += (_, _) => Inspect(target);
                target.Click += (_, _) => { Inspect(target); if (target.Group.CanTerminate) ShotRequested?.Invoke(target.Group); };
                _targets[group.Key] = target;
                Children.Add(target);
            }
            target.Refresh(group, mode);
            target.DesiredDiameter = 64 + 92 * Math.Sqrt(ArenaSelection.Weight(group, mode) / maxWeight);
            AutomationProperties.SetName(target, $"{group.Name}, {group.Processes.Count} processes, " +
                $"{Display.Percent(group.CpuPercent)} CPU, impact {group.Impact.Score:0}. " +
                (group.CanTerminate ? "Activate to shoot." : "Protected; inspection only."));
        }
        ArrangeTargets();
    }

    private void Inspect(ArenaTarget target)
    {
        _pinned = target.Group.Key;
        foreach (var other in _targets.Values) SetZIndex(other, other == target ? 1 : 0);
        Inspected?.Invoke(target.Group);
    }

    public void Celebrate(string key)
    {
        if (ReducedMotion) return;
        if (!_targets.TryGetValue(key, out var target)) return;
        _bursts.Add(new(new(GetLeft(target) + target.Motion.X + target.ActualWidth / 2,
            GetTop(target) + target.Motion.Y + target.ActualHeight / 2),
            Math.Max(target.ActualWidth, target.ActualHeight) / 2 + 4, DateTime.UtcNow));
        InvalidateVisual();
    }

    private void ArrangeTargets()
    {
        if (_targets.Count == 0 || ActualWidth < 1 || ActualHeight < 1) return;
        var slots = _targets.Values.Max(t => t.Slot) + 1;
        var columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(slots * ActualWidth / ActualHeight)));
        var rows = (int)Math.Ceiling((double)slots / columns);
        var cellWidth = ActualWidth / columns;
        var cellHeight = Math.Max(1, (ActualHeight - 65) / rows);
        foreach (var target in _targets.Values)
        {
            var index = target.Slot;
            var column = index % columns;
            var row = index / columns;
            target.Anchor = new((column + .5) * cellWidth, 32 + (row + .5) * cellHeight);
            if (!_frozen && target.Group.Key != _pinned && !target.IsMouseOver && !target.IsKeyboardFocused)
            {
                var cellDiameter = Math.Min(190, Math.Max(44, Math.Min(cellWidth - 18, cellHeight - 20)));
                var diameter = cellDiameter * (.48 + .52 * Math.Clamp((target.DesiredDiameter - 64) / 92, 0, 1));
                if (double.IsNaN(target.Width) || Math.Abs(target.Width - diameter) > 1.5)
                    target.Width = target.Height = diameter;
                SetLeft(target, target.Anchor.X - target.Width / 2);
                SetTop(target, target.Anchor.Y - target.Height / 2);
            }
        }
    }

    private void RenderFrame(object? sender, EventArgs e)
    {
        if (!IsVisible) return;
        var seconds = (DateTime.UtcNow - _started).TotalSeconds;
        var i = 0;
        foreach (var target in _targets.Values)
        {
            if (!_frozen && target.Group.Key != _pinned && !target.IsMouseOver && !target.IsKeyboardFocused)
            {
                var offsetX = ReducedMotion ? 0 : Math.Sin(seconds * .36 + i * 2.4) * 9;
                var offsetY = ReducedMotion ? 0 : Math.Cos(seconds * .29 + i * 1.9) * 7;
                target.Motion.X = offsetX;
                target.Motion.Y = offsetY;
            }
            i++;
        }
        var hadBursts = _bursts.Count > 0;
        _bursts.RemoveAll(b => (DateTime.UtcNow - b.Started).TotalSeconds > .8);
        if (hadBursts) InvalidateVisual();
    }

    private void CacheBackdrop()
    {
        if (ActualWidth < 1 || ActualHeight < 1) return;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(22, 107, 171, 210)), .5);
            for (var x = 24d; x < ActualWidth; x += 38)
                for (var y = 24d; y < ActualHeight; y += 38)
                    dc.DrawEllipse(gridPen.Brush, null, new(x, y), .9, .9);
            var center = new Point(ActualWidth / 2, ActualHeight / 2);
            var radarPen = new Pen(new SolidColorBrush(Color.FromArgb(15, 107, 244, 197)), 1);
            for (var ring = 1; ring <= 3; ring++)
                dc.DrawEllipse(null, radarPen, center, ring * ActualWidth / 7, ring * ActualWidth / 7);
            var label = new FormattedText("LIVE  /  APPLICATION FIELD", CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, new Typeface("Cascadia Mono"), 10,
                new SolidColorBrush(Color.FromRgb(91, 128, 150)), VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(label, new(18, 14));
        }
        var dpi = VisualTreeHelper.GetDpi(this);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(ActualWidth * dpi.DpiScaleX),
            (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY), 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        _backdrop = bitmap;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (_backdrop != null) dc.DrawImage(_backdrop, new(0, 0, ActualWidth, ActualHeight));
        foreach (var burst in _bursts)
        {
            var progress = Math.Clamp((DateTime.UtcNow - burst.Started).TotalSeconds / .8, 0, 1);
            var brush = new SolidColorBrush(Color.FromArgb((byte)(180 * (1 - progress)), 107, 244, 197));
            dc.DrawEllipse(null, new Pen(brush, 2), burst.Center, burst.Radius + progress * 80, burst.Radius + progress * 80);
            for (var ray = 0; ray < 12; ray++)
            {
                var angle = ray * Math.PI / 6;
                var distance = burst.Radius + 5 + progress * 100;
                dc.DrawLine(new Pen(brush, 2),
                    new(burst.Center.X + Math.Cos(angle) * distance, burst.Center.Y + Math.Sin(angle) * distance),
                    new(burst.Center.X + Math.Cos(angle) * (distance + 10), burst.Center.Y + Math.Sin(angle) * (distance + 10)));
            }
        }
    }
}

public sealed class ArenaTarget : Button
{
    public ApplicationGroup Group { get; set; }
    public ResourceMode Mode { get; set; }
    public Point Anchor { get; set; }
    public int Slot { get; set; }
    public double DesiredDiameter { get; set; }
    public TranslateTransform Motion { get; } = new();
    private string? _paintKey;
    private static readonly Typeface Typeface = new("Segoe UI");

    public ArenaTarget(ApplicationGroup group)
    {
        Group = group;
        RenderTransform = Motion;
        CacheMode = new BitmapCache { EnableClearType = false, SnapsToDevicePixels = true };
        Background = Brushes.Transparent;
        BorderThickness = new(0);
        Padding = new(0);
        Template = new ControlTemplate(typeof(Button));
        Cursor = Cursors.Cross;
        FocusVisualStyle = null;
        MouseEnter += (_, _) => InvalidateVisual();
        MouseLeave += (_, _) => InvalidateVisual();
        GotKeyboardFocus += (_, _) => InvalidateVisual();
        LostKeyboardFocus += (_, _) => InvalidateVisual();
        ToolTip = "Hover to inspect. Click or press Enter / Space to shoot. Protected targets are read-only.";
        ToolTipService.SetInitialShowDelay(this, 1500);
    }

    public void Refresh(ApplicationGroup group, ResourceMode mode)
    {
        Group = group;
        Mode = mode;
        var paintKey = $"{group.Name}|{MetricText()}|{group.CanTerminate}|{mode}|{group.Impact.Score >= 35}";
        if (paintKey == _paintKey) return;
        _paintKey = paintKey;
        InvalidateVisual();
    }

    private string MetricText() => Mode switch
    {
        ResourceMode.CPU => Display.Percent(Group.CpuPercent),
        ResourceMode.Memory => Display.Bytes(Group.PrivateBytes),
        ResourceMode.Disk => Group.DiskBytesPerSecond is null ? "N/A" : Display.Rate(Group.DiskBytesPerSecond),
        ResourceMode.Network => Group.NetworkBytesPerSecond is null ? "N/A" : Display.Rate(Group.NetworkBytesPerSecond),
        _ => $"{Group.Impact.Score:0} impact"
    };

    protected override void OnRender(DrawingContext dc)
    {
        var radius = Math.Max(5, Math.Min(ActualWidth, ActualHeight) / 2 - 4);
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var active = IsMouseOver || IsKeyboardFocused;
        var color = !Group.CanTerminate ? Color.FromRgb(118, 143, 167)
            : Group.Impact.Score >= 35 ? Color.FromRgb(255, 139, 107)
            : Mode == ResourceMode.Memory ? Color.FromRgb(172, 151, 255)
            : Color.FromRgb(107, 244, 197);
        dc.DrawEllipse(new RadialGradientBrush(Color.FromArgb(active ? (byte)80 : (byte)38, color.R, color.G, color.B),
            Color.FromArgb(0, color.R, color.G, color.B)), null, center, radius + 8, radius + 8);
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(20, 33, 48)),
            new Pen(new SolidColorBrush(Color.FromArgb(active ? (byte)255 : (byte)110, color.R, color.G, color.B)),
                active ? 2 : 1), center, radius, radius);
        var trackPen = new Pen(new SolidColorBrush(Color.FromArgb(35, color.R, color.G, color.B)), 3);
        dc.DrawEllipse(null, trackPen, center, radius - 4, radius - 4);
        var notch = new Pen(new SolidColorBrush(color), active ? 2 : 1);
        for (var i = 0; i < 4; i++)
        {
            var angle = i * Math.PI / 2;
            dc.DrawLine(notch, new(center.X + Math.Cos(angle) * (radius - 2), center.Y + Math.Sin(angle) * (radius - 2)),
                new(center.X + Math.Cos(angle) * (radius + 4), center.Y + Math.Sin(angle) * (radius + 4)));
        }
        var icon = IconCache.Get(Group.Executable);
        var iconSize = Math.Clamp(radius * .58, 18, 36);
        if (icon != null) dc.DrawImage(icon, new(center.X - iconSize / 2, center.Y - iconSize * .85, iconSize, iconSize));
        else DrawText(dc, Group.Name[..1].ToUpperInvariant(), 24, Brushes.White, center.X, center.Y - 27, radius * 1.7);
        var value = MetricText();
        DrawText(dc, Group.Name, Math.Clamp(radius * .2, 9, 12), Brushes.White, center.X, center.Y + 9, radius * 1.7);
        if (radius > 32)
            DrawText(dc, value + (Group.CanTerminate ? "" : " / locked"), 9, new SolidColorBrush(color),
                center.X, center.Y + 25, radius * 1.75);
    }

    private void DrawText(DrawingContext dc, string text, double size, Brush brush, double x, double y, double width)
    {
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            Typeface, size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxTextWidth = Math.Max(1, width),
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Center
        };
        dc.DrawText(formatted, new(x - width / 2, y));
    }

    protected override HitTestResult? HitTestCore(PointHitTestParameters parameters)
    {
        var delta = parameters.HitPoint - new Point(ActualWidth / 2, ActualHeight / 2);
        return delta.Length <= ActualWidth / 2 ? new PointHitTestResult(this, parameters.HitPoint) : null;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OnClick();
            e.Handled = true;
        }
        else base.OnKeyDown(e);
    }
}
