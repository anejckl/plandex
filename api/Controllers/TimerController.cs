using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Plandex.Api.DTOs;
using Plandex.Api.Extensions;
using Plandex.Api.Services;

namespace Plandex.Api.Controllers;

[ApiController]
[Authorize]
public class TimerController : ControllerBase
{
    private readonly ITimeTrackingService _time;
    public TimerController(ITimeTrackingService time) => _time = time;

    [HttpPost("api/cards/{cardId:int}/timer/start")]
    public async Task<ActionResult<TimeEntryDto>> Start(int cardId)
    {
        var entry = await _time.StartAsync(cardId, User.GetUserId());
        return entry is null ? NotFound() : Ok(entry);
    }

    [HttpPost("api/cards/{cardId:int}/timer/stop")]
    public async Task<ActionResult<TimeEntryDto>> Stop(int cardId)
    {
        var entry = await _time.StopAsync(cardId, User.GetUserId());
        return entry is null ? NotFound() : Ok(entry);
    }

    [HttpGet("api/cards/{cardId:int}/time-entries")]
    public async Task<ActionResult<IReadOnlyList<TimeEntryDto>>> ListForCard(int cardId)
    {
        var entries = await _time.ListForCardAsync(cardId, User.GetUserId());
        return entries is null ? NotFound() : Ok(entries);
    }

    [HttpDelete("api/time-entries/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await _time.DeleteAsync(id, User.GetUserId());
        return ok ? NoContent() : NotFound();
    }

    [HttpGet("api/timer/active")]
    public async Task<ActionResult<ActiveTimerDto?>> Active()
    {
        var active = await _time.GetActiveAsync(User.GetUserId());
        return active is null ? NoContent() : Ok(active);
    }
}
