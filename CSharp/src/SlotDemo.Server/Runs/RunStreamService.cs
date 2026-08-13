using System.Collections.Concurrent;
using System.Threading.Channels;

namespace SlotDemo.Server.Runs;

/// <summary>
/// SSE fan-out for run events, separate from the log stream so a page can watch the run
/// without parsing log lines for numbers it needs as data.
///
/// Each subscriber receives a bounded, drop-oldest channel, as in the log relay. A slow
/// browser may miss intermediate chart points, but it cannot slow the workers or change
/// the run's final totals.
/// </summary>
public sealed class RunStreamService
{
    private const int PerClientBuffer = 256;

    private readonly ConcurrentDictionary<Guid, Channel<string>> _subscribers = new();

    public void Publish(string jsonEvent)
    {
        foreach (var channel in _subscribers.Values)
            channel.Writer.TryWrite(jsonEvent);
    }

    public (Guid Id, ChannelReader<string> Reader) Subscribe()
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(PerClientBuffer)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });
        _subscribers[id] = channel;
        return (id, channel.Reader);
    }

    public void Unsubscribe(Guid id)
    {
        if (_subscribers.TryRemove(id, out var channel))
            channel.Writer.TryComplete();
    }
}
