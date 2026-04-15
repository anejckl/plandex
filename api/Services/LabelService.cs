using Microsoft.EntityFrameworkCore;
using Plandex.Api.Data;
using Plandex.Api.DTOs;
using Plandex.Api.Models;

namespace Plandex.Api.Services;

public interface ILabelService
{
    Task<LabelDto?> CreateAsync(int boardId, int userId, CreateLabelDto dto);
    Task<bool> DeleteAsync(int labelId, int userId);
    Task<bool> AssignAsync(int cardId, int labelId, int userId);
    Task<bool> UnassignAsync(int cardId, int labelId, int userId);
}

public class LabelService : ILabelService
{
    private readonly PlandexDbContext _db;
    private readonly IBoardEventBus _bus;
    public LabelService(PlandexDbContext db, IBoardEventBus bus) { _db = db; _bus = bus; }

    public async Task<LabelDto?> CreateAsync(int boardId, int userId, CreateLabelDto dto)
    {
        var board = await _db.Boards.AccessibleBy(userId).FirstOrDefaultAsync(b => b.Id == boardId);
        if (board is null) return null;

        var label = new Label { BoardId = boardId, Name = dto.Name.Trim(), Color = dto.Color };
        _db.Labels.Add(label);
        await _db.SaveChangesAsync();
        var labelDto = new LabelDto(label.Id, label.BoardId, label.Name, label.Color);
        _bus.Publish(boardId, new BoardEvent("label-created", labelDto));
        return labelDto;
    }

    public async Task<bool> DeleteAsync(int labelId, int userId)
    {
        var label = await _db.Labels
            .Include(l => l.Board).ThenInclude(b => b.Members)
            .FirstOrDefaultAsync(l => l.Id == labelId);
        if (label is null || !label.Board.Members.Any(m => m.UserId == userId)) return false;

        var boardId = label.BoardId;
        _db.Labels.Remove(label);
        await _db.SaveChangesAsync();
        _bus.Publish(boardId, new BoardEvent("label-deleted", new { labelId }));
        return true;
    }

    public async Task<bool> AssignAsync(int cardId, int labelId, int userId)
    {
        var card = await _db.Cards
            .AccessibleBy(userId)
            .Include(c => c.List).ThenInclude(l => l.Board)
            .FirstOrDefaultAsync(c => c.Id == cardId);
        var label = await _db.Labels.FirstOrDefaultAsync(l => l.Id == labelId);
        if (card is null || label is null) return false;
        if (card.List.Board.Id != label.BoardId) return false;

        var existing = await _db.CardLabels.FindAsync(cardId, labelId);
        if (existing is null)
        {
            _db.CardLabels.Add(new CardLabel { CardId = cardId, LabelId = labelId });
            await _db.SaveChangesAsync();
        }
        _bus.Publish(card.List.Board.Id, new BoardEvent("label-assigned",
            new { cardId, labelId = new LabelDto(label.Id, label.BoardId, label.Name, label.Color) }));
        return true;
    }

    public async Task<bool> UnassignAsync(int cardId, int labelId, int userId)
    {
        var card = await _db.Cards
            .AccessibleBy(userId)
            .Include(c => c.List).ThenInclude(l => l.Board)
            .FirstOrDefaultAsync(c => c.Id == cardId);
        if (card is null) return false;

        var link = await _db.CardLabels.FindAsync(cardId, labelId);
        if (link is null) return true;

        _db.CardLabels.Remove(link);
        await _db.SaveChangesAsync();
        _bus.Publish(card.List.Board.Id, new BoardEvent("label-removed", new { cardId, labelId }));
        return true;
    }
}
