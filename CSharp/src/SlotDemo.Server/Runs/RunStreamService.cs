using System.Collections.Concurrent;
using System.Threading.Channels;

namespace SlotDemo.Server.Runs;

/// <summary>
/// Publishes serialized run events to every subscribed SSE client. Run telemetry has a
/// separate stream from application logs because the payloads and retention rules differ.
///
/// Each subscriber has a bounded, drop-oldest channel. A slow browser may miss
/// intermediate chart points, but publishing never waits for that browser.
/// </summary>
public sealed class RunStreamService
{
    private const int EventsPerSubscriber = 256;

    private readonly ConcurrentDictionary<Guid, Channel<string>> _subscriberChannels = new();

    public void PublishRunEvent(string serializedEvent)
    {
        foreach (var channel in _subscriberChannels.Values)
            channel.Writer.TryWrite(serializedEvent);
    }

    public (Guid Id, ChannelReader<string> Reader) SubscribeToRunEvents()
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(EventsPerSubscriber)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });
        _subscriberChannels[id] = channel;
        return (id, channel.Reader);
    }

    public void UnsubscribeFromRunEvents(Guid id)
    {
        if (_subscriberChannels.TryRemove(id, out var channel))
            channel.Writer.TryComplete();
    }
}
