using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;

namespace ShootProcs.Windows;

internal sealed class TraceTelemetry : IDisposable
{
    internal readonly record struct Counters(double Disk, double Network, long EarliestFileTime);
    internal sealed record Batch(bool Available, Dictionary<int, Counters> Processes,
        double DiskBytes, double NetworkBytes, double? LatencyMs);
    private readonly object _gate = new();
    private Dictionary<int, Counters> _counters = [];
    private double _disk, _network, _latency;
    private int _operations;
    private TraceEventSession? _session;
    private Task? _reader;
    private volatile bool _available;
    public string Status { get; private set; } = "Disk/network unavailable. Use Enable tracing to restart elevated.";

    internal TraceTelemetry()
    {
        if (!WindowsMonitor.IsElevated) return;
        try
        {
            _session = new TraceEventSession($"ShootProcs-{Environment.ProcessId}");
            _session.StopOnDispose = true;
            // Accessing Source starts the session, so kernel flags must come first.
            _session.EnableKernelProvider(KernelTraceEventParser.Keywords.DiskIO
                | KernelTraceEventParser.Keywords.DiskIOInit | KernelTraceEventParser.Keywords.NetworkTCPIP
                | KernelTraceEventParser.Keywords.Process | KernelTraceEventParser.Keywords.Thread);
            var kernel = _session.Source.Kernel;
            kernel.DiskIORead += data => Record(data.ProcessID, data.TransferSize, 0, data.ElapsedTimeMSec, data.TimeStamp.ToFileTimeUtc());
            kernel.DiskIOWrite += data => Record(data.ProcessID, data.TransferSize, 0, data.ElapsedTimeMSec, data.TimeStamp.ToFileTimeUtc());
            kernel.TcpIpSend += data => Record(data.ProcessID, 0, data.size, null, data.TimeStamp.ToFileTimeUtc());
            kernel.TcpIpRecv += data => Record(data.ProcessID, 0, data.size, null, data.TimeStamp.ToFileTimeUtc());
            kernel.TcpIpSendIPV6 += data => Record(data.ProcessID, 0, data.size, null, data.TimeStamp.ToFileTimeUtc());
            kernel.TcpIpRecvIPV6 += data => Record(data.ProcessID, 0, data.size, null, data.TimeStamp.ToFileTimeUtc());
            kernel.UdpIpSend += data => Record(data.ProcessID, 0, data.size, null, data.TimeStamp.ToFileTimeUtc());
            kernel.UdpIpRecv += data => Record(data.ProcessID, 0, data.size, null, data.TimeStamp.ToFileTimeUtc());
            kernel.UdpIpSendIPV6 += data => Record(data.ProcessID, 0, data.size, null, data.TimeStamp.ToFileTimeUtc());
            kernel.UdpIpRecvIPV6 += data => Record(data.ProcessID, 0, data.size, null, data.TimeStamp.ToFileTimeUtc());
            _available = true;
            Status = "Live kernel tracing: physical disk I/O and TCP/UDP bytes. Network activity is not a contention score.";
            _reader = Task.Run(() =>
            {
                try { _session.Source.Process(); }
                catch (Exception ex)
                {
                    _available = false;
                    Status = "Tracing unavailable: " + ex.Message;
                }
            });
        }
        catch (Exception ex)
        {
            _available = false;
            Status = "Tracing unavailable: " + ex.Message;
            _session?.Dispose();
            _session = null;
        }
    }

    private void Record(int pid, double disk, double network, double? latency, long timestamp)
    {
        lock (_gate)
        {
            _counters.TryGetValue(pid, out var current);
            _counters[pid] = new(current.Disk + disk, current.Network + network,
                current.EarliestFileTime == 0 ? timestamp : Math.Min(timestamp, current.EarliestFileTime));
            _disk += disk;
            _network += network;
            if (latency is { } value && double.IsFinite(value)) { _latency += value; _operations++; }
        }
    }

    internal Batch Drain(double seconds)
    {
        lock (_gate)
        {
            if (_session?.Source.EventsLost > 0)
            {
                _available = false;
                Status = "Tracing lost events; disk/network attribution disabled. Restart tracing to recover.";
            }
            var batch = new Batch(_available, _counters, _disk, _network,
                _available && _operations > 0 ? _latency / _operations : _available ? 0 : null);
            _counters = [];
            _disk = _network = _latency = 0;
            _operations = 0;
            return batch;
        }
    }

    public void Dispose()
    {
        _available = false;
        _session?.Dispose();
        try { _reader?.Wait(TimeSpan.FromSeconds(2)); }
        catch (AggregateException) { }
    }
}
