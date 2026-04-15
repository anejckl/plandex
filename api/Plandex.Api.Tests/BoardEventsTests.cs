using System.Net.Http.Json;
using System.Threading.Channels;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Plandex.Api.DTOs;
using Plandex.Api.Services;
using Xunit;

namespace Plandex.Api.Tests;

// Targeted tests for SSE event publication from the service layer. Broader
// event delivery is covered implicitly by browser E2E; here we just verify
// the service actually calls _bus.Publish for the event types that used to
// be silently dropped (notably board-updated).
public class BoardEventsTests : IClassFixture<PlandexAppFactory>, IAsyncLifetime
{
    private readonly PlandexAppFactory _factory;
    public BoardEventsTests(PlandexAppFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Rename_board_publishes_board_updated_event()
    {
        var client = _factory.CreateClient();
        await client.RegisterAsync("rename-sse@test.com");

        var board = await (await client.PostAsJsonAsync("/api/boards",
            new CreateBoardDto("Original"))).ReadJsonAsync<BoardSummaryDto>();

        // BoardEventBus is a singleton in Program.cs, so subscribing through
        // any scope gives us the same channel the controller publishes to.
        var bus = _factory.Services.GetRequiredService<IBoardEventBus>();
        var reader = bus.Subscribe(board!.Id);

        try
        {
            var renameResp = await client.PutAsJsonAsync($"/api/boards/{board.Id}",
                new UpdateBoardDto("Renamed live"));
            renameResp.EnsureSuccessStatusCode();

            // Give up after 2s so a regression doesn't hang the test run.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var ev = await reader.ReadAsync(cts.Token);

            ev.Type.Should().Be("board-updated");
            ev.Payload.Should().NotBeNull();
            // Payload is an anonymous type { id, name }; serialise then parse
            // to confirm the name made it through.
            var json = System.Text.Json.JsonSerializer.Serialize(ev.Payload);
            json.Should().Contain("\"name\":\"Renamed live\"");
            json.Should().Contain($"\"id\":{board.Id}");
        }
        finally
        {
            bus.Unsubscribe(board.Id, reader);
        }
    }
}
