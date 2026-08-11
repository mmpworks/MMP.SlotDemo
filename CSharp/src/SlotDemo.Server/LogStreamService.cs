using System.Collections.Concurrent;
using System.Threading.Channels;

namespace SlotDemo.Server;

/// <summary>
/// In-process fan-out from the Herald HttpJson sink to every connected SSE client.
/// Each subscriber gets a bounded channel; a slow client drops its own oldest
/// events instead of stalling the ingest path or other clients.
/// </summary>
public sealed class LogStreamService
{
    private const int PerClientBuffer = 1000;

    private readonly ConcurrentDictionary<Guid, Channel<string>> _subscribers = new();

    public void Publish(string jsonEvent)
    {
        foreach (var channel in _subscribers.Values)
            channel.Writer.TryWrite(jsonEvent); // bounded DropOldest — never blocks
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
