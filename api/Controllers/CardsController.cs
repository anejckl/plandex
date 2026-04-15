using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Plandex.Api.DTOs;
using Plandex.Api.Extensions;
using Plandex.Api.Services;

namespace Plandex.Api.Controllers;

[ApiController]
[Authorize]
public class CardsController : ControllerBase
{
    private readonly ICardService _cards;
    public CardsController(ICardService cards) => _cards = cards;

    [HttpPost("api/lists/{listId:int}/cards")]
    public async Task<ActionResult<CardDto>> Create(int listId, CreateCardDto dto)
    {
        var card = await _cards.CreateAsync(listId, User.GetUserId(), dto);
        return card is null ? NotFound() : Ok(card);
    }

    [HttpGet("api/cards/{id:int}")]
    public async Task<ActionResult<CardDetailDto>> Get(int id)
    {
        var card = await _cards.GetAsync(id, User.GetUserId());
        return card is null ? NotFound() : Ok(card);
    }

    [HttpPut("api/cards/{id:int}")]
    public async Task<ActionResult<CardDetailDto>> Update(int id, UpdateCardDto dto)
    {
        var card = await _cards.UpdateAsync(id, User.GetUserId(), dto);
        return card is null ? NotFound() : Ok(card);
    }

    [HttpDelete("api/cards/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await _cards.DeleteAsync(id, User.GetUserId());
        return ok ? NoContent() : NotFound();
    }

    [HttpGet("api/boards/{boardId:int}/archived-cards")]
    public async Task<ActionResult<IReadOnlyList<CardDto>>> ListArchived(int boardId)
        => Ok(await _cards.ListArchivedAsync(boardId, User.GetUserId()));

    [HttpPost("api/cards/{id:int}/restore")]
    public async Task<IActionResult> Restore(int id)
    {
        var ok = await _cards.RestoreAsync(id, User.GetUserId());
        return ok ? NoContent() : NotFound();
    }

    [HttpDelete("api/cards/{id:int}/purge")]
    public async Task<IActionResult> Purge(int id)
    {
        var ok = await _cards.PurgeAsync(id, User.GetUserId());
        return ok ? NoContent() : NotFound();
    }
}
