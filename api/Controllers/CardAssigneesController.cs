using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Plandex.Api.DTOs;
using Plandex.Api.Extensions;
using Plandex.Api.Services;

namespace Plandex.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/cards/{cardId:int}/assignees")]
public class CardAssigneesController : ControllerBase
{
    private readonly ICardAssigneeService _assignees;
    public CardAssigneesController(ICardAssigneeService assignees) => _assignees = assignees;

    [HttpPost]
    public async Task<ActionResult<AssigneeDto>> Assign(int cardId, AddCardAssigneeDto dto)
    {
        var (result, assignee) = await _assignees.AssignAsync(cardId, User.GetUserId(), dto.UserId);
        return result switch
        {
            AssignResult.Ok => Ok(assignee),
            AssignResult.CardNotFound => NotFound(),
            AssignResult.TargetNotBoardMember => BadRequest(new { error = "User is not a member of this board." }),
            AssignResult.AlreadyAssigned => Conflict(new { error = "User is already assigned to this card." }),
            _ => StatusCode(500),
        };
    }

    [HttpDelete("{userId:int}")]
    public async Task<IActionResult> Unassign(int cardId, int userId)
    {
        var result = await _assignees.UnassignAsync(cardId, User.GetUserId(), userId);
        return result switch
        {
            UnassignResult.Ok => NoContent(),
            UnassignResult.CardNotFound => NotFound(),
            UnassignResult.NotAssigned => NotFound(),
            _ => StatusCode(500),
        };
    }
}
