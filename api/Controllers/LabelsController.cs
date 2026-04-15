using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Plandex.Api.DTOs;
using Plandex.Api.Extensions;
using Plandex.Api.Services;

namespace Plandex.Api.Controllers;

[ApiController]
[Authorize]
public class LabelsController : ControllerBase
{
    private readonly ILabelService _labels;
    public LabelsController(ILabelService labels) => _labels = labels;

    [HttpPost("api/boards/{boardId:int}/labels")]
    public async Task<ActionResult<LabelDto>> Create(int boardId, CreateLabelDto dto)
    {
        var label = await _labels.CreateAsync(boardId, User.GetUserId(), dto);
        return label is null ? NotFound() : Ok(label);
    }

    [HttpDelete("api/labels/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await _labels.DeleteAsync(id, User.GetUserId());
        return ok ? NoContent() : NotFound();
    }

    [HttpPost("api/cards/{cardId:int}/labels/{labelId:int}")]
    public async Task<IActionResult> Assign(int cardId, int labelId)
    {
        var ok = await _labels.AssignAsync(cardId, labelId, User.GetUserId());
        return ok ? NoContent() : NotFound();
    }

    [HttpDelete("api/cards/{cardId:int}/labels/{labelId:int}")]
    public async Task<IActionResult> Unassign(int cardId, int labelId)
    {
        var ok = await _labels.UnassignAsync(cardId, labelId, User.GetUserId());
        return ok ? NoContent() : NotFound();
    }
}
