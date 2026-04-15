using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Plandex.Api.DTOs;
using Xunit;

namespace Plandex.Api.Tests;

// Verifies the query-string token workaround for EventSource, which cannot
// set the Authorization header. Implemented in api/Program.cs via
// JwtBearerEvents.OnMessageReceived, scoped to /api/boards/{id}/events only.
public class SseAuthTests : IClassFixture<PlandexAppFactory>, IAsyncLifetime
{
    private readonly PlandexAppFactory _factory;
    public SseAuthTests(PlandexAppFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(int boardId, string accessToken)> CreateBoardAsOwnerAsync(string email)
    {
        var client = _factory.CreateClient();
        var (_, auth) = await client.RegisterAsync(email);
        var board = await (await client.PostAsJsonAsync("/api/boards",
            new CreateBoardDto("SSE Test"))).ReadJsonAsync<BoardSummaryDto>();
        return (board!.Id, auth.AccessToken);
    }

    [Fact]
    public async Task Sse_endpoint_rejects_request_without_any_token()
    {
        var (boardId, _) = await CreateBoardAsOwnerAsync("sse-anon@test.com");

        // Fresh client with no Authorization header.
        var anon = _factory.CreateClient();
        var resp = await anon.GetAsync($"/api/boards/{boardId}/events");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Sse_endpoint_rejects_request_with_invalid_query_token()
    {
        var (boardId, _) = await CreateBoardAsOwnerAsync("sse-badtoken@test.com");

        var anon = _factory.CreateClient();
        var resp = await anon.GetAsync($"/api/boards/{boardId}/events?access_token=not-a-real-jwt");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Sse_endpoint_accepts_valid_query_token()
    {
        var (boardId, token) = await CreateBoardAsOwnerAsync("sse-good@test.com");

        var anon = _factory.CreateClient();
        // Use ResponseHeadersRead so we don't block waiting for the long-lived
        // stream body. We only need to verify the 200 + text/event-stream response.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        using var resp = await anon.GetAsync(
            $"/api/boards/{boardId}/events?access_token={token}",
            HttpCompletionOption.ResponseHeadersRead,
            cts.Token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType?.MediaType.Should().Be("text/event-stream");
    }

    [Fact]
    public async Task Query_token_is_ignored_on_non_sse_endpoints()
    {
        // Security boundary: the query-token workaround is scoped in Program.cs
        // to paths ending in /events. A valid token passed as ?access_token=
        // to any other endpoint should NOT authenticate the request.
        var (_, token) = await CreateBoardAsOwnerAsync("sse-scope@test.com");

        var anon = _factory.CreateClient();
        var resp = await anon.GetAsync($"/api/boards?access_token={token}");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
