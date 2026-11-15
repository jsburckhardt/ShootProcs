using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using ShootProcs.Core;

namespace ShootProcs.App;

public sealed class GroupRow(ApplicationGroup group) : INotifyPropertyChanged
{
    public ApplicationGroup Group { get; private set; } = group;
    public string Name => Group.Name;
    public string Subtext => $"{Group.Processes.Count} processes  /  {(Group.CanTerminate ? "application" : "protected")}";
    public ImageSource? Icon => IconCache.Get(Group.Executable);
    public double Cpu => Group.CpuPercent ?? -1;
    public long Memory => Group.PrivateBytes ?? -1;
    public double Disk => Group.DiskBytesPerSecond ?? -1;
    public double Network => Group.NetworkBytesPerSecond ?? -1;
    public double Score => Group.Impact.Score;
    public string CpuText => Display.Percent(Group.CpuPercent);
    public string MemoryText => Display.Bytes(Group.PrivateBytes);
    public string DiskText => Display.Rate(Group.DiskBytesPerSecond);
    public string NetworkText => Display.Rate(Group.NetworkBytesPerSecond);
    public string ScoreText => $"{Score:0}";
    public string Explanation => Group.Impact.Reason;
    public IReadOnlyList<ProcessSample> Processes => Group.Processes;
    public double[] History => Group.History;
    public bool CanTerminate => Group.CanTerminate;
    public void Update(ApplicationGroup next)
    {
        var old = Group;
        Group = next;
        if (old.Name != next.Name) OnPropertyChanged(nameof(Name));
        if (old.Processes.Count != next.Processes.Count || old.CanTerminate != next.CanTerminate)
            OnPropertyChanged(nameof(Subtext));
        if (old.CpuPercent != next.CpuPercent) { OnPropertyChanged(nameof(Cpu)); OnPropertyChanged(nameof(CpuText)); }
        if (old.PrivateBytes != next.PrivateBytes) { OnPropertyChanged(nameof(Memory)); OnPropertyChanged(nameof(MemoryText)); }
        if (old.DiskBytesPerSecond != next.DiskBytesPerSecond) { OnPropertyChanged(nameof(Disk)); OnPropertyChanged(nameof(DiskText)); }
        if (old.NetworkBytesPerSecond != next.NetworkBytesPerSecond) { OnPropertyChanged(nameof(Network)); OnPropertyChanged(nameof(NetworkText)); }
        if (old.Impact.Score != next.Impact.Score) { OnPropertyChanged(nameof(Score)); OnPropertyChanged(nameof(ScoreText)); }
        if (old.Impact.Reason != next.Impact.Reason) OnPropertyChanged(nameof(Explanation));
        OnPropertyChanged(nameof(Processes));
        OnPropertyChanged(nameof(History));
        OnPropertyChanged(nameof(CanTerminate));
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new(name));
}
