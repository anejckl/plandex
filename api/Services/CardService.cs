using Microsoft.EntityFrameworkCore;
using Plandex.Api.Data;
using Plandex.Api.DTOs;
using Plandex.Api.Models;

namespace Plandex.Api.Services;

public interface ICardService
{
    Task<CardDto?> CreateAsync(int listId, int userId, CreateCardDto dto);
    Task<CardDetailDto?> GetAsync(int cardId, int userId);
    Task<CardDetailDto?> UpdateAsync(int cardId, int userId, UpdateCardDto dto);
    Task<bool> DeleteAsync(int cardId, int userId);      // soft-delete (archive)
    Task<IReadOnlyList<CardDto>> ListArchivedAsync(int boardId, int userId);
    Task<bool> RestoreAsync(int cardId, int userId);
    Task<bool> PurgeAsync(int cardId, int userId);       // hard delete
}

public class CardService : ICardService
{
    private readonly PlandexDbContext _db;
    private readonly IBoardEventBus _bus;
    public CardService(PlandexDbContext db, IBoardEventBus bus) { _db = db; _bus = bus; }

    public async Task<CardDto?> CreateAsync(int listId, int userId, CreateCardDto dto)
    {
        var list = await _db.Lists
            .AccessibleBy(userId)
            .Include(l => l.Board)
            .FirstOrDefaultAsync(l => l.Id == listId);
        if (list is null) return null;

        var count = await _db.Cards.CountAsync(c => c.ListId == listId && c.ArchivedAt == null);
        var pos = dto.Position ?? count;
        if (pos < 0) pos = 0;
        if (pos > count) pos = count;

        await _db.Cards
            .Where(c => c.ListId == listId && c.Position >= pos)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Position, c => c.Position + 1));

        var card = new Card
        {
            ListId = listId,
            Title = dto.Title.Trim(),
            Description = dto.Description,
            DueDate = dto.DueDate,
            Position = pos
        };
        _db.Cards.Add(card);
        await _db.SaveChangesAsync();

        var cardDto = new CardDto(card.Id, card.ListId, card.Title, card.Description, card.Position,
            card.DueDate, new List<LabelDto>(), 0, 0, 0, null, new List<AssigneeDto>());
        _bus.Publish(list.Board.Id, new BoardEvent("card-created", cardDto));
        return cardDto;
    }

    public async Task<CardDetailDto?> GetAsync(int cardId, int userId)
    {
        var card = await _db.Cards
            .AccessibleBy(userId)
            .Include(c => c.List)
            .Include(c => c.CardLabels).ThenInclude(cl => cl.Label)
            .Include(c => c.Checklists).ThenInclude(ch => ch.Items)
            .Include(c => c.TimeEntries)
            .Include(c => c.Assignees).ThenInclude(a => a.User)
            .FirstOrDefaultAsync(c => c.Id == cardId && c.ArchivedAt == null);

        return card is null ? null : MapDetail(card, userId);
    }

    public async Task<CardDetailDto?> UpdateAsync(int cardId, int userId, UpdateCardDto dto)
    {
        var card = await _db.Cards
            .AccessibleBy(userId)
            .Include(c => c.List).ThenInclude(l => l.Board)
            .Include(c => c.CardLabels).ThenInclude(cl => cl.Label)
            .Include(c => c.Checklists).ThenInclude(ch => ch.Items)
            .Include(c => c.TimeEntries)
            .Include(c => c.Assignees).ThenInclude(a => a.User)
            .FirstOrDefaultAsync(c => c.Id == cardId && c.ArchivedAt == null);
        if (card is null) return null;

        if (dto.Title is not null) card.Title = dto.Title.Trim();
        if (dto.Description is not null) card.Description = dto.Description;
        if (dto.ClearDueDate) card.DueDate = null;
        else if (dto.DueDate.HasValue) card.DueDate = dto.DueDate;

        var targetListId = dto.ListId ?? card.ListId;
        if (dto.ListId.HasValue && dto.ListId.Value != card.ListId)
        {
            // Target list must belong to a board the user can access (and in
            // practice to the same board as the current list).
            var targetList = await _db.Lists
                .AccessibleBy(userId)
                .Include(l => l.Board)
                .FirstOrDefaultAsync(l => l.Id == dto.ListId.Value);
            if (targetList is null) return null;
        }

        if (dto.Position.HasValue || targetListId != card.ListId)
        {
            var oldListId = card.ListId;
            var oldPos = card.Position;
            var newPos = dto.Position ?? await _db.Cards.CountAsync(c => c.ListId == targetListId);

            if (targetListId != oldListId)
            {
                // Remove from source list
                await _db.Cards
                    .Where(c => c.ListId == oldListId && c.Position > oldPos)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.Position, c => c.Position - 1));

                var targetCount = await _db.Cards.CountAsync(c => c.ListId == targetListId);
                if (newPos < 0) newPos = 0;
                if (newPos > targetCount) newPos = targetCount;

                await _db.Cards
                    .Where(c => c.ListId == targetListId && c.Position >= newPos)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.Position, c => c.Position + 1));

                card.ListId = targetListId;
                card.Position = newPos;
            }
            else if (newPos != oldPos)
            {
                var count = await _db.Cards.CountAsync(c => c.ListId == targetListId);
                if (newPos < 0) newPos = 0;
                if (newPos >= count) newPos = count - 1;

                if (newPos < oldPos)
                {
                    await _db.Cards
                        .Where(c => c.ListId == targetListId && c.Id != card.Id
                                    && c.Position >= newPos && c.Position < oldPos)
                        .ExecuteUpdateAsync(s => s.SetProperty(c => c.Position, c => c.Position + 1));
                }
                else
                {
                    await _db.Cards
                        .Where(c => c.ListId == targetListId && c.Id != card.Id
                                    && c.Position > oldPos && c.Position <= newPos)
                        .ExecuteUpdateAsync(s => s.SetProperty(c => c.Position, c => c.Position - 1));
                }
                card.Position = newPos;
            }
        }

        await _db.SaveChangesAsync();
        await _db.Entry(card).ReloadAsync();
        var detail = MapDetail(card, userId);
        _bus.Publish(card.List.Board.Id, new BoardEvent("card-updated", detail));
        return detail;
    }

    public async Task<bool> DeleteAsync(int cardId, int userId)
    {
        var card = await _db.Cards
            .AccessibleBy(userId)
            .Include(c => c.List).ThenInclude(l => l.Board)
            .FirstOrDefaultAsync(c => c.Id == cardId && c.ArchivedAt == null);
        if (card is null) return false;

        var listId = card.ListId;
        var oldPos = card.Position;
        var boardId = card.List.Board.Id;
        card.ArchivedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _db.Cards
            .Where(c => c.ListId == listId && c.ArchivedAt == null && c.Position > oldPos)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Position, c => c.Position - 1));

        _bus.Publish(boardId, new BoardEvent("card-deleted", new { cardId, listId }));
        return true;
    }

    public async Task<IReadOnlyList<CardDto>> ListArchivedAsync(int boardId, int userId)
    {
        var cards = await _db.Cards
            .AccessibleBy(userId)
            .Include(c => c.List).ThenInclude(l => l.Board)
            .Include(c => c.CardLabels).ThenInclude(cl => cl.Label)
            .Include(c => c.Assignees).ThenInclude(a => a.User)
            .Where(c => c.List.Board.Id == boardId && c.ArchivedAt != null)
            .OrderByDescending(c => c.ArchivedAt)
            .ToListAsync();

        return cards.Select(c => new CardDto(
            c.Id, c.ListId, c.Title, c.Description, c.Position, c.DueDate,
            c.CardLabels.Select(cl => new LabelDto(cl.LabelId, cl.Label.BoardId, cl.Label.Name, cl.Label.Color)).ToList(),
            0, 0, 0, null,
            c.Assignees.Select(a => new AssigneeDto(a.UserId, a.User.Name, a.User.Email)).ToList())).ToList();
    }

    public async Task<bool> RestoreAsync(int cardId, int userId)
    {
        var card = await _db.Cards
            .AccessibleBy(userId)
            .Include(c => c.List).ThenInclude(l => l.Board)
            .Include(c => c.Assignees).ThenInclude(a => a.User)
            .FirstOrDefaultAsync(c => c.Id == cardId && c.ArchivedAt != null);
        if (card is null) return false;

        var newPos = await _db.Cards.CountAsync(c => c.ListId == card.ListId && c.ArchivedAt == null);
        card.Position = newPos;
        card.ArchivedAt = null;
        await _db.SaveChangesAsync();

        _bus.Publish(card.List.Board.Id, new BoardEvent("card-created",
            new CardDto(card.Id, card.ListId, card.Title, card.Description, card.Position,
                card.DueDate, new List<LabelDto>(), 0, 0, 0, null,
                card.Assignees.Select(a => new AssigneeDto(a.UserId, a.User.Name, a.User.Email)).ToList())));
        return true;
    }

    public async Task<bool> PurgeAsync(int cardId, int userId)
    {
        // Hard-delete is owner-only — purging someone else's archived cards is
        // destructive enough that members shouldn't be able to do it.
        var card = await _db.Cards
            .Include(c => c.List).ThenInclude(l => l.Board).ThenInclude(b => b.Members)
            .FirstOrDefaultAsync(c => c.Id == cardId && c.ArchivedAt != null);
        if (card is null) return false;
        if (!card.List.Board.Members.Any(m => m.UserId == userId && m.Role == BoardRole.Owner)) return false;

        _db.Cards.Remove(card);
        await _db.SaveChangesAsync();
        return true;
    }

    private static CardDetailDto MapDetail(Card c, int userId)
    {
        var totalClosed = c.TimeEntries.Where(t => t.DurationSeconds.HasValue).Sum(t => t.DurationSeconds!.Value);
        var active = c.TimeEntries.FirstOrDefault(t => t.UserId == userId && t.EndedAt == null);

        return new CardDetailDto(
            c.Id,
            c.ListId,
            c.Title,
            c.Description,
            c.Position,
            c.DueDate,
            c.CardLabels.Select(cl => new LabelDto(cl.LabelId, cl.Label.BoardId, cl.Label.Name, cl.Label.Color)).ToList(),
            c.Checklists.Select(ch => new ChecklistDto(
                ch.Id, ch.CardId, ch.Title,
                ch.Items.OrderBy(i => i.Position)
                    .Select(i => new ChecklistItemDto(i.Id, i.ChecklistId, i.Text, i.IsDone, i.Position))
                    .ToList())).ToList(),
            c.TimeEntries.OrderByDescending(t => t.StartedAt)
                .Select(t => new TimeEntryDto(t.Id, t.CardId, t.UserId, t.StartedAt, t.EndedAt, t.DurationSeconds))
                .ToList(),
            totalClosed,
            active?.StartedAt,
            c.Assignees.Select(a => new AssigneeDto(a.UserId, a.User.Name, a.User.Email)).ToList());
    }
}
