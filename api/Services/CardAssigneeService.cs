using Microsoft.EntityFrameworkCore;
using Plandex.Api.Data;
using Plandex.Api.DTOs;
using Plandex.Api.Models;

namespace Plandex.Api.Services;

public enum AssignResult { Ok, CardNotFound, TargetNotBoardMember, AlreadyAssigned }
public enum UnassignResult { Ok, CardNotFound, NotAssigned }

public interface ICardAssigneeService
{
    Task<(AssignResult result, AssigneeDto? assignee)> AssignAsync(int cardId, int requesterId, int targetUserId);
    Task<UnassignResult> UnassignAsync(int cardId, int requesterId, int targetUserId);
}

public class CardAssigneeService : ICardAssigneeService
{
    private readonly PlandexDbContext _db;
    private readonly IBoardEventBus _bus;

    public CardAssigneeService(PlandexDbContext db, IBoardEventBus bus)
    {
        _db = db;
        _bus = bus;
    }

    public async Task<(AssignResult, AssigneeDto?)> AssignAsync(int cardId, int requesterId, int targetUserId)
    {
        var card = await _db.Cards
            .AccessibleBy(requesterId)
            .Include(c => c.List).ThenInclude(l => l.Board).ThenInclude(b => b.Members)
            .Include(c => c.Assignees)
            .FirstOrDefaultAsync(c => c.Id == cardId && c.ArchivedAt == null);
        if (card is null) return (AssignResult.CardNotFound, null);

        var targetIsMember = card.List.Board.Members.Any(m => m.UserId == targetUserId);
        if (!targetIsMember) return (AssignResult.TargetNotBoardMember, null);

        if (card.Assignees.Any(a => a.UserId == targetUserId))
            return (AssignResult.AlreadyAssigned, null);

        _db.CardAssignees.Add(new CardAssignee
        {
            CardId = cardId,
            UserId = targetUserId,
            AssignedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var user = await _db.Users.FirstAsync(u => u.Id == targetUserId);
        var dto = new AssigneeDto(user.Id, user.Name, user.Email);
        _bus.Publish(card.List.Board.Id, new BoardEvent("card-assigned", new { cardId, assignee = dto }));
        return (AssignResult.Ok, dto);
    }

    public async Task<UnassignResult> UnassignAsync(int cardId, int requesterId, int targetUserId)
    {
        var card = await _db.Cards
            .AccessibleBy(requesterId)
            .Include(c => c.List).ThenInclude(l => l.Board)
            .Include(c => c.Assignees)
            .FirstOrDefaultAsync(c => c.Id == cardId);
        if (card is null) return UnassignResult.CardNotFound;

        var assignment = card.Assignees.FirstOrDefault(a => a.UserId == targetUserId);
        if (assignment is null) return UnassignResult.NotAssigned;

        _db.CardAssignees.Remove(assignment);
        await _db.SaveChangesAsync();

        _bus.Publish(card.List.Board.Id, new BoardEvent("card-unassigned", new { cardId, userId = targetUserId }));
        return UnassignResult.Ok;
    }
}
