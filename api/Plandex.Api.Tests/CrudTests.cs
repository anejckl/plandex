using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Plandex.Api.DTOs;
using Xunit;

namespace Plandex.Api.Tests;

public class CrudTests : IClassFixture<PlandexAppFactory>, IAsyncLifetime
{
    private readonly PlandexAppFactory _factory;
    public CrudTests(PlandexAppFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Create_board_list_card_and_read_back()
    {
        var client = _factory.CreateClient();
        await client.RegisterAsync("crud@test.com");

        var board = await (await client.PostAsJsonAsync("/api/boards",
            new CreateBoardDto("Demo"))).ReadJsonAsync<BoardSummaryDto>();
        var list = await (await client.PostAsJsonAsync($"/api/boards/{board!.Id}/lists",
            new CreateListDto("To Do", null))).ReadJsonAsync<ListDto>();
        var card = await (await client.PostAsJsonAsync($"/api/lists/{list!.Id}/cards",
            new CreateCardDto("Card 1", null, null, null))).ReadJsonAsync<CardDto>();

        var detail = await client.GetJsonAsync<BoardDetailDto>($"/api/boards/{board.Id}");
        detail!.Lists.Should().HaveCount(1);
        detail.Lists[0].Cards.Should().ContainSingle(c => c.Id == card!.Id);
    }

    [Fact]
    public async Task Other_user_cannot_access_board()
    {
        var clientA = _factory.CreateClient();
        await clientA.RegisterAsync("a@test.com");
        var board = await (await clientA.PostAsJsonAsync("/api/boards",
            new CreateBoardDto("A's board"))).ReadJsonAsync<BoardSummaryDto>();

        var clientB = _factory.CreateClient();
        await clientB.RegisterAsync("b@test.com");
        var resp = await clientB.GetAsync($"/api/boards/{board!.Id}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Card_move_to_other_list_updates_positions()
    {
        var client = _factory.CreateClient();
        await client.RegisterAsync("move@test.com");

        var board = await (await client.PostAsJsonAsync("/api/boards",
            new CreateBoardDto("B"))).ReadJsonAsync<BoardSummaryDto>();
        var l1 = await (await client.PostAsJsonAsync($"/api/boards/{board!.Id}/lists",
            new CreateListDto("L1", null))).ReadJsonAsync<ListDto>();
        var l2 = await (await client.PostAsJsonAsync($"/api/boards/{board.Id}/lists",
            new CreateListDto("L2", null))).ReadJsonAsync<ListDto>();

        // Create three cards in l1
        for (int i = 0; i < 3; i++)
            await client.PostAsJsonAsync($"/api/lists/{l1!.Id}/cards",
                new CreateCardDto($"C{i}", null, null, null));

        // Move middle card (position 1) to l2 position 0
        var detailBefore = await client.GetJsonAsync<BoardDetailDto>($"/api/boards/{board.Id}");
        var middleCardId = detailBefore!.Lists.First(l => l.Id == l1!.Id).Cards[1].Id;

        var moveResp = await client.PutAsJsonAsync($"/api/cards/{middleCardId}",
            new UpdateCardDto(null, null, null, ListId: l2!.Id, Position: 0));
        moveResp.EnsureSuccessStatusCode();

        var detailAfter = await client.GetJsonAsync<BoardDetailDto>($"/api/boards/{board.Id}");
        var l1After = detailAfter!.Lists.First(l => l.Id == l1!.Id);
        var l2After = detailAfter.Lists.First(l => l.Id == l2.Id);

        l1After.Cards.Should().HaveCount(2);
        l1After.Cards.Select(c => c.Position).Should().BeEquivalentTo(new[] { 0, 1 });
        l2After.Cards.Should().HaveCount(1);
        l2After.Cards[0].Id.Should().Be(middleCardId);
        l2After.Cards[0].Position.Should().Be(0);
    }

    [Fact]
    public async Task Delete_board_cascades_lists_and_cards()
    {
        var client = _factory.CreateClient();
        await client.RegisterAsync("cas@test.com");

        var board = await (await client.PostAsJsonAsync("/api/boards",
            new CreateBoardDto("Cas"))).ReadJsonAsync<BoardSummaryDto>();
        var list = await (await client.PostAsJsonAsync($"/api/boards/{board!.Id}/lists",
            new CreateListDto("L", null))).ReadJsonAsync<ListDto>();
        var card = await (await client.PostAsJsonAsync($"/api/lists/{list!.Id}/cards",
            new CreateCardDto("C", null, null, null))).ReadJsonAsync<CardDto>();

        (await client.DeleteAsync($"/api/boards/{board.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetAsync($"/api/cards/{card!.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Card_create_blocked_at_50_active_cards_per_user()
    {
        // Dev-stage safeguard: each user can have at most 50 active cards.
        // The 51st POST must return 429. Archiving a card must free up a slot.
        var client = _factory.CreateClient();
        await client.RegisterAsync("limit@test.com");

        var board = await (await client.PostAsJsonAsync("/api/boards",
            new CreateBoardDto("Limit test"))).ReadJsonAsync<BoardSummaryDto>();
        var list = await (await client.PostAsJsonAsync($"/api/boards/{board!.Id}/lists",
            new CreateListDto("L", null))).ReadJsonAsync<ListDto>();

        // 50 cards — all succeed.
        var lastCardId = 0;
        for (int i = 0; i < 50; i++)
        {
            var resp = await client.PostAsJsonAsync($"/api/lists/{list!.Id}/cards",
                new CreateCardDto($"C{i}", null, null, null));
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            var card = await resp.ReadJsonAsync<CardDto>();
            lastCardId = card!.Id;
        }

        // 51st — blocked.
        var blockedResp = await client.PostAsJsonAsync($"/api/lists/{list!.Id}/cards",
            new CreateCardDto("over-limit", null, null, null));
        ((int)blockedResp.StatusCode).Should().Be(429);

        // Archiving a card frees a slot → next POST works.
        (await client.DeleteAsync($"/api/cards/{lastCardId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var afterArchive = await client.PostAsJsonAsync($"/api/lists/{list!.Id}/cards",
            new CreateCardDto("after-archive", null, null, null));
        afterArchive.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Checklist_items_track_progress()
    {
        var client = _factory.CreateClient();
        await client.RegisterAsync("ch@test.com");

        var board = await (await client.PostAsJsonAsync("/api/boards",
            new CreateBoardDto("B"))).ReadJsonAsync<BoardSummaryDto>();
        var list = await (await client.PostAsJsonAsync($"/api/boards/{board!.Id}/lists",
            new CreateListDto("L", null))).ReadJsonAsync<ListDto>();
        var card = await (await client.PostAsJsonAsync($"/api/lists/{list!.Id}/cards",
            new CreateCardDto("C", null, null, null))).ReadJsonAsync<CardDto>();
        var cl = await (await client.PostAsJsonAsync($"/api/cards/{card!.Id}/checklists",
            new CreateChecklistDto("TODO"))).ReadJsonAsync<ChecklistDto>();

        for (int i = 0; i < 3; i++)
            await client.PostAsJsonAsync($"/api/checklists/{cl!.Id}/items",
                new CreateChecklistItemDto($"item {i}", null));

        await client.PutAsJsonAsync("/api/checklist-items/1",
            new UpdateChecklistItemDto(null, true, null));

        var detail = await client.GetJsonAsync<BoardDetailDto>($"/api/boards/{board.Id}");
        var cardAfter = detail!.Lists[0].Cards[0];
        cardAfter.ChecklistTotal.Should().Be(3);
        cardAfter.ChecklistDone.Should().Be(1);
    }
}
