using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Plandex.Api.Extensions;
using Plandex.Api.Services;

namespace Plandex.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/boards")]
public class BoardEventsController : ControllerBase
{
    private readonly IBoardEventBus _bus;
    private readonly IBoardService _boards;
    private static readonly JsonSerializerOptions JsonOpts =
        new(JsonSerializerDefaults.Web);

    public BoardEventsController(IBoardEventBus bus, IBoardService boards)
    {
        _bus = bus;
        _boards = boards;
    }

    [HttpGet("{boardId:int}/events")]
    public async Task Stream(int boardId, CancellationToken ct)
    {
        // Verify ownership
        var board = await _boards.GetAsync(boardId, User.GetUserId());
        if (board is null)
        {
            Response.StatusCode = 404;
            return;
        }

        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        var reader = _bus.Subscribe(boardId);
        try
        {
            // Send a keep-alive comment immediately
            await WriteLineAsync(":\n\n", ct);

            while (!ct.IsCancellationRequested)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                linkedCts.CancelAfter(TimeSpan.FromSeconds(25)); // keep-alive heartbeat

                try
                {
                    var ev = await reader.ReadAsync(linkedCts.Token);
                    var payload = JsonSerializer.Serialize(ev.Payload, JsonOpts);
                    var data = $"event: {ev.Type}\ndata: {payload}\n\n";
                    await WriteLineAsync(data, ct);
                    await Response.Body.FlushAsync(ct);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    // Heartbeat timeout — send keep-alive comment
                    await WriteLineAsync(":\n\n", ct);
                    await Response.Body.FlushAsync(ct);
                }
            }
        }
        finally
        {
            _bus.Unsubscribe(boardId, reader);
        }
    }

    private Task WriteLineAsync(string text, CancellationToken ct)
        => Response.Body.WriteAsync(Encoding.UTF8.GetBytes(text), ct).AsTask();
}
