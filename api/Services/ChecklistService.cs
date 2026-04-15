using Microsoft.EntityFrameworkCore;
using Plandex.Api.Data;
using Plandex.Api.DTOs;
using Plandex.Api.Models;

namespace Plandex.Api.Services;

public interface IChecklistService
{
    Task<ChecklistDto?> CreateAsync(int cardId, int userId, CreateChecklistDto dto);
    Task<bool> DeleteAsync(int checklistId, int userId);
    Task<ChecklistItemDto?> AddItemAsync(int checklistId, int userId, CreateChecklistItemDto dto);
    Task<ChecklistItemDto?> UpdateItemAsync(int itemId, int userId, UpdateChecklistItemDto dto);
    Task<bool> DeleteItemAsync(int itemId, int userId);
}

public class ChecklistService : IChecklistService
{
    private readonly PlandexDbContext _db;
    public ChecklistService(PlandexDbContext db) => _db = db;

    public async Task<ChecklistDto?> CreateAsync(int cardId, int userId, CreateChecklistDto dto)
    {
        var card = await _db.Cards
            .Include(c => c.List).ThenInclude(l => l.Board)
            .FirstOrDefaultAsync(c => c.Id == cardId);
        if (card is null || card.List.Board.OwnerId != userId) return null;

        var cl = new Checklist { CardId = cardId, Title = dto.Title.Trim() };
        _db.Checklists.Add(cl);
        await _db.SaveChangesAsync();
        return new ChecklistDto(cl.Id, cl.CardId, cl.Title, new List<ChecklistItemDto>());
    }

    public async Task<bool> DeleteAsync(int checklistId, int userId)
    {
        var cl = await _db.Checklists
            .Include(c => c.Card).ThenInclude(c => c.List).ThenInclude(l => l.Board)
            .FirstOrDefaultAsync(c => c.Id == checklistId);
        if (cl is null || cl.Card.List.Board.OwnerId != userId) return false;

        _db.Checklists.Remove(cl);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<ChecklistItemDto?> AddItemAsync(int checklistId, int userId, CreateChecklistItemDto dto)
    {
        var cl = await _db.Checklists
            .Include(c => c.Card).ThenInclude(c => c.List).ThenInclude(l => l.Board)
            .FirstOrDefaultAsync(c => c.Id == checklistId);
        if (cl is null || cl.Card.List.Board.OwnerId != userId) return null;

        var count = await _db.ChecklistItems.CountAsync(i => i.ChecklistId == checklistId);
        var pos = dto.Position ?? count;
        if (pos < 0) pos = 0;
        if (pos > count) pos = count;

        await _db.ChecklistItems
            .Where(i => i.ChecklistId == checklistId && i.Position >= pos)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.Position, i => i.Position + 1));

        var item = new ChecklistItem
        {
            ChecklistId = checklistId,
            Text = dto.Text.Trim(),
            Position = pos,
            IsDone = false
        };
        _db.ChecklistItems.Add(item);
        await _db.SaveChangesAsync();
        return new ChecklistItemDto(item.Id, item.ChecklistId, item.Text, item.IsDone, item.Position);
    }

    public async Task<ChecklistItemDto?> UpdateItemAsync(int itemId, int userId, UpdateChecklistItemDto dto)
    {
        var item = await _db.ChecklistItems
            .Include(i => i.Checklist).ThenInclude(cl => cl.Card).ThenInclude(c => c.List).ThenInclude(l => l.Board)
            .FirstOrDefaultAsync(i => i.Id == itemId);
        if (item is null || item.Checklist.Card.List.Board.OwnerId != userId) return null;

        if (dto.Text is not null) item.Text = dto.Text.Trim();
        if (dto.IsDone.HasValue) item.IsDone = dto.IsDone.Value;
        // Position change within a checklist is simple for MVP: just set it, callers rarely reorder checklist items
        if (dto.Position.HasValue) item.Position = dto.Position.Value;

        await _db.SaveChangesAsync();
        return new ChecklistItemDto(item.Id, item.ChecklistId, item.Text, item.IsDone, item.Position);
    }

    public async Task<bool> DeleteItemAsync(int itemId, int userId)
    {
        var item = await _db.ChecklistItems
            .Include(i => i.Checklist).ThenInclude(cl => cl.Card).ThenInclude(c => c.List).ThenInclude(l => l.Board)
            .FirstOrDefaultAsync(i => i.Id == itemId);
        if (item is null || item.Checklist.Card.List.Board.OwnerId != userId) return false;

        var checklistId = item.ChecklistId;
        var oldPos = item.Position;
        _db.ChecklistItems.Remove(item);
        await _db.SaveChangesAsync();

        await _db.ChecklistItems
            .Where(i => i.ChecklistId == checklistId && i.Position > oldPos)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.Position, i => i.Position - 1));

        return true;
    }
}
