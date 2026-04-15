using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Plandex.Api.DTOs;
using Plandex.Api.Extensions;
using Plandex.Api.Services;

namespace Plandex.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/boards")]
public class BoardsController : ControllerBase
{
    private readonly IBoardService _boards;
    public BoardsController(IBoardService boards) => _boards = boards;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BoardSummaryDto>>> List()
        => Ok(await _boards.ListAsync(User.GetUserId()));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BoardDetailDto>> Get(int id)
    {
        var board = await _boards.GetAsync(id, User.GetUserId());
        return board is null ? NotFound() : Ok(board);
    }

    [HttpPost]
    public async Task<ActionResult<BoardSummaryDto>> Create(CreateBoardDto dto)
    {
        var board = await _boards.CreateAsync(User.GetUserId(), dto.Name);
        return CreatedAtAction(nameof(Get), new { id = board.Id }, board);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateBoardDto dto)
    {
        var ok = await _boards.RenameAsync(id, User.GetUserId(), dto.Name);
        return ok ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await _boards.DeleteAsync(id, User.GetUserId());
        return ok ? NoContent() : NotFound();
    }
}
