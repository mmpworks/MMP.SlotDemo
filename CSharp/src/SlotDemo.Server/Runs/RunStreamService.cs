using System.Collections.Concurrent;
using System.Threading.Channels;

namespace SlotDemo.Server.Runs;

/// <summary>
/// Distributes run events to the browsers watching <c>/api/run/stream</c>.
///
/// This class does not write the SSE response. It moves JSON strings from the run
/// coordinator into one queue per browser. The endpoint reads those queues and writes the
/// <c>data: ...</c> frames required by SSE.
///
/// A browser can fall behind without holding up a simulation. Its queue keeps the newest
/// 256 events and discards older ones when full. The final totals belong to the run itself,
/// so dropping an intermediate chart point does not change the result.
/// </summary>
public sealed class RunStreamService
{
    private const int PerClientBuffer = 256;

    // ConcurrentDictionary allows a run thread to publish while browsers connect or leave.
    // Each browser gets its own channel; readers do not compete for one shared event.
    private readonly ConcurrentDictionary<Guid, Channel<string>> _subscribers = new();

    /// <summary>
    /// Offers one serialized run event to every browser currently subscribed.
    /// <see cref="ChannelWriter{T}.TryWrite(T)"/> returns immediately, including when a
    /// slow browser's full queue has to discard its oldest event.
    /// </summary>
    public void Publish(string jsonEvent)
    {
        foreach (var channel in _subscribers.Values)
            channel.Writer.TryWrite(jsonEvent);
    }

    /// <summary>
    /// Opens a private event queue for one browser connection. The returned ID is the
    /// handle used to remove that queue later; the endpoint reads events from
    /// <c>Reader</c> until the browser disconnects.
    /// </summary>
    public (Guid Id, ChannelReader<string> Reader) Subscribe()
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(PerClientBuffer)
        {
            // Chart updates are snapshots of progress. Keeping recent samples is more useful
            // than making the simulation wait for a browser that cannot read fast enough.
            FullMode = BoundedChannelFullMode.DropOldest,

            // RunEndpoints is the sole reader for this browser's channel.
            SingleReader = true,
        });
        _subscribers[id] = channel;
        return (id, channel.Reader);
    }

    /// <summary>
    /// Removes a browser's queue and marks it complete. Completion releases a reader that
    /// may still be waiting for another event. Calling this more than once is harmless.
    /// </summary>
    public void Unsubscribe(Guid id)
    {
        if (_subscribers.TryRemove(id, out var channel))
            channel.Writer.TryComplete();
    }
}
