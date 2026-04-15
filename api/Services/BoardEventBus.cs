using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace Plandex.Api.Services;

public record BoardEvent(string Type, object? Payload = null);

public interface IBoardEventBus
{
    ChannelReader<BoardEvent> Subscribe(int boardId);
    void Unsubscribe(int boardId, ChannelReader<BoardEvent> reader);
    void Publish(int boardId, BoardEvent ev);
}

public class BoardEventBus : IBoardEventBus
{
    private readonly ConcurrentDictionary<int, List<Channel<BoardEvent>>> _channels = new();
    private readonly object _lock = new();

    public ChannelReader<BoardEvent> Subscribe(int boardId)
    {
        var ch = Channel.CreateBounded<BoardEvent>(new BoundedChannelOptions(50)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });
        lock (_lock)
        {
            _channels.GetOrAdd(boardId, _ => new()).Add(ch);
        }
        return ch.Reader;
    }

    public void Unsubscribe(int boardId, ChannelReader<BoardEvent> reader)
    {
        lock (_lock)
        {
            if (_channels.TryGetValue(boardId, out var list))
            {
                list.RemoveAll(ch => ch.Reader == reader);
                if (list.Count == 0) _channels.TryRemove(boardId, out _);
            }
        }
    }

    public void Publish(int boardId, BoardEvent ev)
    {
        List<Channel<BoardEvent>> snapshot;
        lock (_lock)
        {
            if (!_channels.TryGetValue(boardId, out var list)) return;
            snapshot = list.ToList();
        }
        foreach (var ch in snapshot)
            ch.Writer.TryWrite(ev);
    }
}
