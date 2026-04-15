using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Plandex.Api.DTOs;
using Plandex.Api.Extensions;
using Plandex.Api.Services;

namespace Plandex.Api.Controllers;

[ApiController]
[Authorize]
public class ChecklistsController : ControllerBase
{
    private readonly IChecklistService _checklists;
    public ChecklistsController(IChecklistService checklists) => _checklists = checklists;

    [HttpPost("api/cards/{cardId:int}/checklists")]
    public async Task<ActionResult<ChecklistDto>> Create(int cardId, CreateChecklistDto dto)
    {
        var cl = await _checklists.CreateAsync(cardId, User.GetUserId(), dto);
        return cl is null ? NotFound() : Ok(cl);
    }

    [HttpDelete("api/checklists/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await _checklists.DeleteAsync(id, User.GetUserId());
        return ok ? NoContent() : NotFound();
    }

    [HttpPost("api/checklists/{checklistId:int}/items")]
    public async Task<ActionResult<ChecklistItemDto>> AddItem(int checklistId, CreateChecklistItemDto dto)
    {
        var item = await _checklists.AddItemAsync(checklistId, User.GetUserId(), dto);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPut("api/checklist-items/{id:int}")]
    public async Task<ActionResult<ChecklistItemDto>> UpdateItem(int id, UpdateChecklistItemDto dto)
    {
        var item = await _checklists.UpdateItemAsync(id, User.GetUserId(), dto);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpDelete("api/checklist-items/{id:int}")]
    public async Task<IActionResult> DeleteItem(int id)
    {
        var ok = await _checklists.DeleteItemAsync(id, User.GetUserId());
        return ok ? NoContent() : NotFound();
    }
}
