using System.Collections.Concurrent;
using System.Threading.Channels;

namespace SlotDemo.Server.Runs;

/// <summary>
/// SSE fan-out for run events, separate from the log stream so a page can watch the run
/// without parsing log lines for numbers it needs as data.
///
/// Same posture as the log relay: one bounded channel per subscriber, drop-oldest. A
/// browser that cannot keep up loses chart points, never accuracy, and never slows the
/// workers down. That is the lossy lane doing its job.
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
