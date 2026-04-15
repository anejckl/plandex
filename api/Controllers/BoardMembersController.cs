using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Plandex.Api.DTOs;
using Plandex.Api.Extensions;
using Plandex.Api.Services;

namespace Plandex.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/boards/{boardId:int}/members")]
public class BoardMembersController : ControllerBase
{
    private readonly IBoardMemberService _members;
    public BoardMembersController(IBoardMemberService members) => _members = members;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BoardMemberDto>>> List(int boardId)
    {
        var members = await _members.ListAsync(boardId, User.GetUserId());
        return members is null ? NotFound() : Ok(members);
    }

    [HttpPost]
    public async Task<ActionResult<BoardMemberDto>> Add(int boardId, AddBoardMemberDto dto)
    {
        var (result, member) = await _members.AddAsync(boardId, User.GetUserId(), dto.Email);
        return result switch
        {
            AddMemberResult.Ok => CreatedAtAction(nameof(List), new { boardId }, member),
            AddMemberResult.BoardNotFound => NotFound(),
            AddMemberResult.NotOwner => Forbid(),
            AddMemberResult.UserNotFound => NotFound(new { error = "No registered user with that email." }),
            AddMemberResult.AlreadyMember => Conflict(new { error = "User is already a member of this board." }),
            _ => StatusCode(500),
        };
    }

    [HttpDelete("{userId:int}")]
    public async Task<IActionResult> Remove(int boardId, int userId)
    {
        var result = await _members.RemoveAsync(boardId, userId, User.GetUserId());
        return result switch
        {
            RemoveMemberResult.Ok => NoContent(),
            RemoveMemberResult.BoardNotFound => NotFound(),
            RemoveMemberResult.NotAMember => NotFound(),
            RemoveMemberResult.NotAuthorized => Forbid(),
            RemoveMemberResult.CannotRemoveLastOwner => Conflict(new { error = "Cannot remove the last owner of a board." }),
            _ => StatusCode(500),
        };
    }
}
