using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Plandex.Api.DTOs;
using Plandex.Api.Extensions;
using Plandex.Api.Services;

namespace Plandex.Api.Controllers;

[ApiController]
[Authorize]
public class ListsController : ControllerBase
{
    private readonly IListService _lists;
    public ListsController(IListService lists) => _lists = lists;

    [HttpPost("api/boards/{boardId:int}/lists")]
    public async Task<ActionResult<ListDto>> Create(int boardId, CreateListDto dto)
    {
        var list = await _lists.CreateAsync(boardId, User.GetUserId(), dto);
        return list is null ? NotFound() : Ok(list);
    }

    [HttpPut("api/lists/{id:int}")]
    public async Task<ActionResult<ListDto>> Update(int id, UpdateListDto dto)
    {
        var list = await _lists.UpdateAsync(id, User.GetUserId(), dto);
        return list is null ? NotFound() : Ok(list);
    }

    [HttpDelete("api/lists/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await _lists.DeleteAsync(id, User.GetUserId());
        return ok ? NoContent() : NotFound();
    }
}
