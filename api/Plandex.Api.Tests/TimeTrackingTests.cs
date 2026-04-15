using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Plandex.Api.DTOs;
using Plandex.Api.Services;
using Xunit;

namespace Plandex.Api.Tests;

public class TimeTrackingTests : IClassFixture<PlandexAppFactory>, IAsyncLifetime
{
    private readonly PlandexAppFactory _factory;
    public TimeTrackingTests(PlandexAppFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(HttpClient client, int cardA, int cardB)> SetupTwoCardsAsync()
    {
        var client = _factory.CreateClient();
        await client.RegisterAsync("t@test.com");
        var board = await (await client.PostAsJsonAsync("/api/boards",
            new CreateBoardDto("B"))).ReadJsonAsync<BoardSummaryDto>();
        var list = await (await client.PostAsJsonAsync($"/api/boards/{board!.Id}/lists",
            new CreateListDto("L", null))).ReadJsonAsync<ListDto>();
        var a = await (await client.PostAsJsonAsync($"/api/lists/{list!.Id}/cards",
            new CreateCardDto("A", null, null, null))).ReadJsonAsync<CardDto>();
        var b = await (await client.PostAsJsonAsync($"/api/lists/{list.Id}/cards",
            new CreateCardDto("B", null, null, null))).ReadJsonAsync<CardDto>();
        return (client, a!.Id, b!.Id);
    }

    [Fact]
    public async Task Starting_timer_creates_open_entry()
    {
        var (client, a, _) = await SetupTwoCardsAsync();

        var entry = await (await client.PostAsync($"/api/cards/{a}/timer/start", null))
            .ReadJsonAsync<TimeEntryDto>();

        entry!.EndedAt.Should().BeNull();
        entry.DurationSeconds.Should().BeNull();

        var active = await client.GetJsonAsync<ActiveTimerDto>("/api/timer/active");
        active!.CardId.Should().Be(a);
    }

    [Fact]
    public async Task Starting_second_timer_auto_stops_first()
    {
        var (client, a, b) = await SetupTwoCardsAsync();

        await client.PostAsync($"/api/cards/{a}/timer/start", null);
        await Task.Delay(100);
        await client.PostAsync($"/api/cards/{b}/timer/start", null);

        var aEntries = await client.GetJsonAsync<List<TimeEntryDto>>($"/api/cards/{a}/time-entries");
        aEntries!.Should().ContainSingle();
        aEntries[0].EndedAt.Should().NotBeNull();
        aEntries[0].DurationSeconds.Should().NotBeNull();

        var bEntries = await client.GetJsonAsync<List<TimeEntryDto>>($"/api/cards/{b}/time-entries");
        bEntries!.Should().ContainSingle();
        bEntries[0].EndedAt.Should().BeNull();
    }

    [Fact]
    public async Task Only_one_active_timer_per_user_ever()
    {
        var (client, a, b) = await SetupTwoCardsAsync();

        await client.PostAsync($"/api/cards/{a}/timer/start", null);
        await client.PostAsync($"/api/cards/{b}/timer/start", null);
        await client.PostAsync($"/api/cards/{a}/timer/start", null);
        await client.PostAsync($"/api/cards/{b}/timer/start", null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Data.PlandexDbContext>();
        var openCount = db.TimeEntries.Count(t => t.EndedAt == null);
        openCount.Should().Be(1);
    }

    [Fact]
    public async Task Stopping_timer_sets_ended_at_and_duration()
    {
        var (client, a, _) = await SetupTwoCardsAsync();
        await client.PostAsync($"/api/cards/{a}/timer/start", null);
        await Task.Delay(200);
        var stopped = await (await client.PostAsync($"/api/cards/{a}/timer/stop", null))
            .ReadJsonAsync<TimeEntryDto>();

        stopped!.EndedAt.Should().NotBeNull();
        stopped.DurationSeconds.Should().NotBeNull();
        stopped.DurationSeconds.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Card_total_logged_seconds_sums_closed_entries()
    {
        var (client, a, _) = await SetupTwoCardsAsync();

        // First session
        await client.PostAsync($"/api/cards/{a}/timer/start", null);
        await Task.Delay(150);
        await client.PostAsync($"/api/cards/{a}/timer/stop", null);

        // Second session
        await client.PostAsync($"/api/cards/{a}/timer/start", null);
        await Task.Delay(150);
        await client.PostAsync($"/api/cards/{a}/timer/stop", null);

        var detail = await client.GetJsonAsync<CardDetailDto>($"/api/cards/{a}");
        detail!.TimeEntries.Should().HaveCount(2);
        detail.TimeEntries.All(e => e.EndedAt.HasValue).Should().BeTrue();
    }

    [Fact]
    public async Task Parallel_starts_for_same_user_result_in_single_open_entry()
    {
        var (client, a, b) = await SetupTwoCardsAsync();

        var tasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < 10; i++)
        {
            var cardId = i % 2 == 0 ? a : b;
            tasks.Add(client.PostAsync($"/api/cards/{cardId}/timer/start", null));
        }
        await Task.WhenAll(tasks);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Data.PlandexDbContext>();
        var openCount = db.TimeEntries.Count(t => t.EndedAt == null);
        openCount.Should().Be(1);
    }

    [Fact]
    public async Task Cannot_start_timer_on_other_users_card()
    {
        var (_, a, _) = await SetupTwoCardsAsync();

        var clientB = _factory.CreateClient();
        await clientB.RegisterAsync("b2@test.com");
        var resp = await clientB.PostAsync($"/api/cards/{a}/timer/start", null);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
