using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Plandex.Api.DTOs;
using Xunit;

namespace Plandex.Api.Tests;

public class BoardMembersTests : IClassFixture<PlandexAppFactory>, IAsyncLifetime
{
    private readonly PlandexAppFactory _factory;
    public BoardMembersTests(PlandexAppFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // Helper: create owner, create board, return (ownerClient, boardId).
    private async Task<(HttpClient owner, BoardSummaryDto board)> CreateOwnerWithBoardAsync(string email)
    {
        var owner = _factory.CreateClient();
        await owner.RegisterAsync(email);
        var board = await (await owner.PostAsJsonAsync("/api/boards",
            new CreateBoardDto("Shared"))).ReadJsonAsync<BoardSummaryDto>();
        return (owner, board!);
    }

    [Fact]
    public async Task Owner_can_add_existing_user_by_email()
    {
        var (owner, board) = await CreateOwnerWithBoardAsync("owner1@test.com");

        var member = _factory.CreateClient();
        await member.RegisterAsync("member1@test.com");

        var resp = await owner.PostAsJsonAsync(
            $"/api/boards/{board.Id}/members",
            new AddBoardMemberDto("member1@test.com"));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);

        var added = await resp.ReadJsonAsync<BoardMemberDto>();
        added!.Email.Should().Be("member1@test.com");
        added.Role.Should().Be("Member");
    }

    [Fact]
    public async Task Adding_unknown_email_returns_404()
    {
        var (owner, board) = await CreateOwnerWithBoardAsync("owner2@test.com");

        var resp = await owner.PostAsJsonAsync(
            $"/api/boards/{board.Id}/members",
            new AddBoardMemberDto("nobody@test.com"));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Adding_existing_member_returns_409()
    {
        var (owner, board) = await CreateOwnerWithBoardAsync("owner3@test.com");
        var member = _factory.CreateClient();
        await member.RegisterAsync("dupe@test.com");

        (await owner.PostAsJsonAsync($"/api/boards/{board.Id}/members",
            new AddBoardMemberDto("dupe@test.com"))).EnsureSuccessStatusCode();

        var resp = await owner.PostAsJsonAsync($"/api/boards/{board.Id}/members",
            new AddBoardMemberDto("dupe@test.com"));
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Non_owner_member_cannot_add_other_members()
    {
        var (owner, board) = await CreateOwnerWithBoardAsync("owner4@test.com");
        var member = _factory.CreateClient();
        await member.RegisterAsync("mem@test.com");
        (await owner.PostAsJsonAsync($"/api/boards/{board.Id}/members",
            new AddBoardMemberDto("mem@test.com"))).EnsureSuccessStatusCode();

        var third = _factory.CreateClient();
        await third.RegisterAsync("third@test.com");

        var resp = await member.PostAsJsonAsync($"/api/boards/{board.Id}/members",
            new AddBoardMemberDto("third@test.com"));
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Non_member_cannot_see_board_or_members()
    {
        var (_, board) = await CreateOwnerWithBoardAsync("owner5@test.com");

        var outsider = _factory.CreateClient();
        await outsider.RegisterAsync("outsider@test.com");

        (await outsider.GetAsync($"/api/boards/{board.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await outsider.GetAsync($"/api/boards/{board.Id}/members")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Added_member_sees_board_in_list_and_can_edit_cards()
    {
        var (owner, board) = await CreateOwnerWithBoardAsync("owner6@test.com");
        var list = await (await owner.PostAsJsonAsync($"/api/boards/{board.Id}/lists",
            new CreateListDto("L", null))).ReadJsonAsync<ListDto>();
        var card = await (await owner.PostAsJsonAsync($"/api/lists/{list!.Id}/cards",
            new CreateCardDto("C", null, null, null))).ReadJsonAsync<CardDto>();

        var member = _factory.CreateClient();
        await member.RegisterAsync("shared@test.com");
        (await owner.PostAsJsonAsync($"/api/boards/{board.Id}/members",
            new AddBoardMemberDto("shared@test.com"))).EnsureSuccessStatusCode();

        // Member sees board in their list.
        var mine = await member.GetJsonAsync<List<BoardSummaryDto>>("/api/boards");
        mine.Should().Contain(b => b.Id == board.Id);

        // Member can read detail and edit card.
        var detail = await member.GetJsonAsync<BoardDetailDto>($"/api/boards/{board.Id}");
        detail!.Members.Should().HaveCount(2);

        var edit = await member.PutAsJsonAsync($"/api/cards/{card!.Id}",
            new UpdateCardDto("Renamed by member", null, null));
        edit.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Member_cannot_rename_or_delete_board()
    {
        var (owner, board) = await CreateOwnerWithBoardAsync("owner7@test.com");
        var member = _factory.CreateClient();
        await member.RegisterAsync("ro@test.com");
        (await owner.PostAsJsonAsync($"/api/boards/{board.Id}/members",
            new AddBoardMemberDto("ro@test.com"))).EnsureSuccessStatusCode();

        (await member.PutAsJsonAsync($"/api/boards/{board.Id}",
            new UpdateBoardDto("Hijacked"))).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await member.DeleteAsync($"/api/boards/{board.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Owner_can_remove_member_and_removed_member_loses_access()
    {
        var (owner, board) = await CreateOwnerWithBoardAsync("owner8@test.com");
        var list = await (await owner.PostAsJsonAsync($"/api/boards/{board.Id}/lists",
            new CreateListDto("L", null))).ReadJsonAsync<ListDto>();
        var card = await (await owner.PostAsJsonAsync($"/api/lists/{list!.Id}/cards",
            new CreateCardDto("C", null, null, null))).ReadJsonAsync<CardDto>();

        var member = _factory.CreateClient();
        var (_, auth) = await member.RegisterAsync("kicked@test.com");
        (await owner.PostAsJsonAsync($"/api/boards/{board.Id}/members",
            new AddBoardMemberDto("kicked@test.com"))).EnsureSuccessStatusCode();

        var kick = await owner.DeleteAsync($"/api/boards/{board.Id}/members/{auth.User.Id}");
        kick.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await member.GetAsync($"/api/boards/{board.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await member.GetAsync($"/api/cards/{card!.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Member_can_self_leave_but_owner_cannot_leave_own_board_alone()
    {
        var (owner, board) = await CreateOwnerWithBoardAsync("owner9@test.com");

        var member = _factory.CreateClient();
        var (_, auth) = await member.RegisterAsync("leaver@test.com");
        (await owner.PostAsJsonAsync($"/api/boards/{board.Id}/members",
            new AddBoardMemberDto("leaver@test.com"))).EnsureSuccessStatusCode();

        // Member self-leave.
        (await member.DeleteAsync($"/api/boards/{board.Id}/members/{auth.User.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Owner tries to leave own board (last owner) — blocked.
        // Identify owner's user id by reading the board detail as owner.
        var detail = await owner.GetJsonAsync<BoardDetailDto>($"/api/boards/{board.Id}");
        var ownerId = detail!.Members.Single(m => m.Role == "Owner").UserId;
        var resp = await owner.DeleteAsync($"/api/boards/{board.Id}/members/{ownerId}");
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Member_can_create_list_card_and_label_in_shared_board()
    {
        // Regression for the AccessibleBy refactor in every nested service.
        // Before the swap, only the board owner could create lists/cards/labels.
        var (owner, board) = await CreateOwnerWithBoardAsync("owner10@test.com");
        var member = _factory.CreateClient();
        await member.RegisterAsync("creator@test.com");
        (await owner.PostAsJsonAsync($"/api/boards/{board.Id}/members",
            new AddBoardMemberDto("creator@test.com"))).EnsureSuccessStatusCode();

        // Member creates a list on the shared board.
        var listResp = await member.PostAsJsonAsync($"/api/boards/{board.Id}/lists",
            new CreateListDto("Member-created list", null));
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await listResp.ReadJsonAsync<ListDto>();

        // Member creates a card in that list.
        var cardResp = await member.PostAsJsonAsync($"/api/lists/{list!.Id}/cards",
            new CreateCardDto("Member-created card", "Body", null, null));
        cardResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Member creates a label on the shared board.
        var labelResp = await member.PostAsJsonAsync($"/api/boards/{board.Id}/labels",
            new CreateLabelDto("Member label", "#123456"));
        labelResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Owner now sees all three on the shared board.
        var detail = await owner.GetJsonAsync<BoardDetailDto>($"/api/boards/{board.Id}");
        detail!.Lists.Should().Contain(l => l.Name == "Member-created list");
        detail.Lists.SelectMany(l => l.Cards).Should().Contain(c => c.Title == "Member-created card");
        detail.Labels.Should().Contain(l => l.Name == "Member label");
    }

    [Fact]
    public async Task Member_cannot_purge_archived_card()
    {
        var (owner, board) = await CreateOwnerWithBoardAsync("owner11@test.com");
        var list = await (await owner.PostAsJsonAsync($"/api/boards/{board.Id}/lists",
            new CreateListDto("L", null))).ReadJsonAsync<ListDto>();
        var card = await (await owner.PostAsJsonAsync($"/api/lists/{list!.Id}/cards",
            new CreateCardDto("Doomed", null, null, null))).ReadJsonAsync<CardDto>();
        (await owner.DeleteAsync($"/api/cards/{card!.Id}")).EnsureSuccessStatusCode(); // archive

        var member = _factory.CreateClient();
        await member.RegisterAsync("purger@test.com");
        (await owner.PostAsJsonAsync($"/api/boards/{board.Id}/members",
            new AddBoardMemberDto("purger@test.com"))).EnsureSuccessStatusCode();

        // Member attempting hard-delete: forbidden. PurgeAsync currently
        // returns false (→ 404) when the caller is not the board owner.
        var purgeResp = await member.DeleteAsync($"/api/cards/{card.Id}/purge");
        purgeResp.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Owner can still purge.
        (await owner.DeleteAsync($"/api/cards/{card.Id}/purge"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Removing_member_drops_assignments_across_multiple_cards()
    {
        var (owner, board) = await CreateOwnerWithBoardAsync("owner12@test.com");
        var list = await (await owner.PostAsJsonAsync($"/api/boards/{board.Id}/lists",
            new CreateListDto("L", null))).ReadJsonAsync<ListDto>();

        // Create three cards.
        var cardIds = new List<int>();
        for (int i = 0; i < 3; i++)
        {
            var c = await (await owner.PostAsJsonAsync($"/api/lists/{list!.Id}/cards",
                new CreateCardDto($"Card {i}", null, null, null))).ReadJsonAsync<CardDto>();
            cardIds.Add(c!.Id);
        }

        var member = _factory.CreateClient();
        var (_, auth) = await member.RegisterAsync("triple@test.com");
        (await owner.PostAsJsonAsync($"/api/boards/{board.Id}/members",
            new AddBoardMemberDto("triple@test.com"))).EnsureSuccessStatusCode();

        // Assign member to all three cards.
        foreach (var cardId in cardIds)
        {
            (await owner.PostAsJsonAsync($"/api/cards/{cardId}/assignees",
                new AddCardAssigneeDto(auth.User.Id))).EnsureSuccessStatusCode();
        }

        // Sanity check: all three cards have the assignee.
        var detailBefore = await owner.GetJsonAsync<BoardDetailDto>($"/api/boards/{board.Id}");
        detailBefore!.Lists.Single().Cards
            .Should().OnlyContain(c => c.Assignees.Any(a => a.UserId == auth.User.Id));

        // Remove member from the board.
        (await owner.DeleteAsync($"/api/boards/{board.Id}/members/{auth.User.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Every card's assignees should now be empty.
        var detailAfter = await owner.GetJsonAsync<BoardDetailDto>($"/api/boards/{board.Id}");
        detailAfter!.Lists.Single().Cards
            .Should().OnlyContain(c => !c.Assignees.Any());
    }
}
