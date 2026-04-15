using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Plandex.Api.DTOs;
using Xunit;

namespace Plandex.Api.Tests;

public class CardAssigneesTests : IClassFixture<PlandexAppFactory>, IAsyncLifetime
{
    private readonly PlandexAppFactory _factory;
    public CardAssigneesTests(PlandexAppFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(HttpClient owner, HttpClient member, AuthResponseDto memberAuth, int boardId, int cardId)> SetupSharedBoardWithCardAsync()
    {
        var owner = _factory.CreateClient();
        await owner.RegisterAsync("a-owner@test.com");

        var board = await (await owner.PostAsJsonAsync("/api/boards",
            new CreateBoardDto("Shared"))).ReadJsonAsync<BoardSummaryDto>();
        var list = await (await owner.PostAsJsonAsync($"/api/boards/{board!.Id}/lists",
            new CreateListDto("L", null))).ReadJsonAsync<ListDto>();
        var card = await (await owner.PostAsJsonAsync($"/api/lists/{list!.Id}/cards",
            new CreateCardDto("C", null, null, null))).ReadJsonAsync<CardDto>();

        var member = _factory.CreateClient();
        var (_, memberAuth) = await member.RegisterAsync("a-member@test.com");
        (await owner.PostAsJsonAsync($"/api/boards/{board.Id}/members",
            new AddBoardMemberDto("a-member@test.com"))).EnsureSuccessStatusCode();

        return (owner, member, memberAuth, board.Id, card!.Id);
    }

    [Fact]
    public async Task Member_can_assign_self_to_card()
    {
        var (_, member, memberAuth, boardId, cardId) = await SetupSharedBoardWithCardAsync();

        var resp = await member.PostAsJsonAsync($"/api/cards/{cardId}/assignees",
            new AddCardAssigneeDto(memberAuth.User.Id));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await member.GetJsonAsync<CardDetailDto>($"/api/cards/{cardId}");
        detail!.Assignees.Should().ContainSingle(a => a.UserId == memberAuth.User.Id);
    }

    [Fact]
    public async Task Owner_can_assign_another_member_to_card()
    {
        var (owner, _, memberAuth, boardId, cardId) = await SetupSharedBoardWithCardAsync();

        var resp = await owner.PostAsJsonAsync($"/api/cards/{cardId}/assignees",
            new AddCardAssigneeDto(memberAuth.User.Id));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Cannot_assign_user_who_is_not_a_board_member()
    {
        var (owner, _, _, boardId, cardId) = await SetupSharedBoardWithCardAsync();

        var outsider = _factory.CreateClient();
        var (_, outsiderAuth) = await outsider.RegisterAsync("a-outsider@test.com");

        var resp = await owner.PostAsJsonAsync($"/api/cards/{cardId}/assignees",
            new AddCardAssigneeDto(outsiderAuth.User.Id));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Non_member_cannot_assign_anyone_to_the_card()
    {
        var (_, _, memberAuth, boardId, cardId) = await SetupSharedBoardWithCardAsync();

        var outsider = _factory.CreateClient();
        await outsider.RegisterAsync("a-stranger@test.com");

        var resp = await outsider.PostAsJsonAsync($"/api/cards/{cardId}/assignees",
            new AddCardAssigneeDto(memberAuth.User.Id));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Duplicate_assign_returns_conflict()
    {
        var (_, member, memberAuth, boardId, cardId) = await SetupSharedBoardWithCardAsync();

        (await member.PostAsJsonAsync($"/api/cards/{cardId}/assignees",
            new AddCardAssigneeDto(memberAuth.User.Id))).EnsureSuccessStatusCode();

        var resp = await member.PostAsJsonAsync($"/api/cards/{cardId}/assignees",
            new AddCardAssigneeDto(memberAuth.User.Id));
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Unassign_removes_assignment_and_appears_in_detail()
    {
        var (_, member, memberAuth, boardId, cardId) = await SetupSharedBoardWithCardAsync();

        (await member.PostAsJsonAsync($"/api/cards/{cardId}/assignees",
            new AddCardAssigneeDto(memberAuth.User.Id))).EnsureSuccessStatusCode();

        (await member.DeleteAsync($"/api/cards/{cardId}/assignees/{memberAuth.User.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await member.GetJsonAsync<CardDetailDto>($"/api/cards/{cardId}");
        detail!.Assignees.Should().BeEmpty();
    }

    [Fact]
    public async Task Removing_member_from_board_also_removes_their_card_assignments()
    {
        var (owner, _, memberAuth, boardId, cardId) = await SetupSharedBoardWithCardAsync();

        (await owner.PostAsJsonAsync($"/api/cards/{cardId}/assignees",
            new AddCardAssigneeDto(memberAuth.User.Id))).EnsureSuccessStatusCode();

        // Owner removes member
        (await owner.DeleteAsync($"/api/boards/{boardId}/members/{memberAuth.User.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Card detail (as owner) should show no assignees
        var detail = await owner.GetJsonAsync<CardDetailDto>($"/api/cards/{cardId}");
        detail!.Assignees.Should().BeEmpty();
    }

    [Fact]
    public async Task Assignee_persists_after_card_moves_between_lists()
    {
        var (owner, _, memberAuth, boardId, cardId) = await SetupSharedBoardWithCardAsync();

        // Assign member to the card.
        (await owner.PostAsJsonAsync($"/api/cards/{cardId}/assignees",
            new AddCardAssigneeDto(memberAuth.User.Id))).EnsureSuccessStatusCode();

        // Create a second list on the same board.
        var secondList = await (await owner.PostAsJsonAsync($"/api/boards/{boardId}/lists",
            new CreateListDto("L2", null))).ReadJsonAsync<ListDto>();

        // Move the card to the new list.
        var moveResp = await owner.PutAsJsonAsync($"/api/cards/{cardId}",
            new UpdateCardDto(null, null, null, ListId: secondList!.Id, Position: 0));
        moveResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Card should still carry the assignee after the move.
        var detail = await owner.GetJsonAsync<CardDetailDto>($"/api/cards/{cardId}");
        detail!.ListId.Should().Be(secondList.Id);
        detail.Assignees.Should().ContainSingle(a => a.UserId == memberAuth.User.Id);
    }

    [Fact]
    public async Task Assignee_persists_through_archive_and_restore()
    {
        var (owner, _, memberAuth, _, cardId) = await SetupSharedBoardWithCardAsync();

        // Assign.
        (await owner.PostAsJsonAsync($"/api/cards/{cardId}/assignees",
            new AddCardAssigneeDto(memberAuth.User.Id))).EnsureSuccessStatusCode();

        // Archive then restore.
        (await owner.DeleteAsync($"/api/cards/{cardId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await owner.PostAsJsonAsync($"/api/cards/{cardId}/restore", new { }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assignee should still be there.
        var detail = await owner.GetJsonAsync<CardDetailDto>($"/api/cards/{cardId}");
        detail!.Assignees.Should().ContainSingle(a => a.UserId == memberAuth.User.Id);
    }

    [Fact]
    public async Task Multiple_users_can_be_assigned_to_same_card()
    {
        var owner = _factory.CreateClient();
        var (_, ownerAuth) = await owner.RegisterAsync("multi-owner@test.com");
        var board = await (await owner.PostAsJsonAsync("/api/boards",
            new CreateBoardDto("Multi"))).ReadJsonAsync<BoardSummaryDto>();
        var list = await (await owner.PostAsJsonAsync($"/api/boards/{board!.Id}/lists",
            new CreateListDto("L", null))).ReadJsonAsync<ListDto>();
        var card = await (await owner.PostAsJsonAsync($"/api/lists/{list!.Id}/cards",
            new CreateCardDto("Shared task", null, null, null))).ReadJsonAsync<CardDto>();

        var member = _factory.CreateClient();
        var (_, memberAuth) = await member.RegisterAsync("multi-member@test.com");
        (await owner.PostAsJsonAsync($"/api/boards/{board.Id}/members",
            new AddBoardMemberDto("multi-member@test.com"))).EnsureSuccessStatusCode();

        // Owner assigns themselves.
        (await owner.PostAsJsonAsync($"/api/cards/{card!.Id}/assignees",
            new AddCardAssigneeDto(ownerAuth.User.Id))).EnsureSuccessStatusCode();
        // Owner assigns the member.
        (await owner.PostAsJsonAsync($"/api/cards/{card.Id}/assignees",
            new AddCardAssigneeDto(memberAuth.User.Id))).EnsureSuccessStatusCode();

        var detail = await owner.GetJsonAsync<CardDetailDto>($"/api/cards/{card.Id}");
        detail!.Assignees.Should().HaveCount(2);
        detail.Assignees.Select(a => a.UserId)
            .Should().BeEquivalentTo(new[] { ownerAuth.User.Id, memberAuth.User.Id });

        // And they also appear on the board summary for the card.
        var boardDetail = await owner.GetJsonAsync<BoardDetailDto>($"/api/boards/{board.Id}");
        var cardSummary = boardDetail!.Lists.Single().Cards.Single();
        cardSummary.Assignees.Should().HaveCount(2);
    }
}
