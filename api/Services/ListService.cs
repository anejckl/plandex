using Microsoft.EntityFrameworkCore;
using Plandex.Api.Data;
using Plandex.Api.DTOs;
using Plandex.Api.Models;

namespace Plandex.Api.Services;

public interface IListService
{
    Task<ListDto?> CreateAsync(int boardId, int userId, CreateListDto dto);
    Task<ListDto?> UpdateAsync(int listId, int userId, UpdateListDto dto);
    Task<bool> DeleteAsync(int listId, int userId);
}

public class ListService : IListService
{
    private readonly PlandexDbContext _db;
    private readonly IBoardEventBus _bus;
    public ListService(PlandexDbContext db, IBoardEventBus bus) { _db = db; _bus = bus; }

    public async Task<ListDto?> CreateAsync(int boardId, int userId, CreateListDto dto)
    {
        var board = await _db.Boards.FirstOrDefaultAsync(b => b.Id == boardId && b.OwnerId == userId);
        if (board is null) return null;

        var count = await _db.Lists.CountAsync(l => l.BoardId == boardId);
        var pos = dto.Position ?? count;
        if (pos < 0) pos = 0;
        if (pos > count) pos = count;

        await _db.Lists
            .Where(l => l.BoardId == boardId && l.Position >= pos)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.Position, l => l.Position + 1));

        var list = new BoardList { BoardId = boardId, Name = dto.Name.Trim(), Position = pos };
        _db.Lists.Add(list);
        await _db.SaveChangesAsync();

        var listDto = new ListDto(list.Id, list.BoardId, list.Name, list.Position, new List<CardDto>());
        _bus.Publish(boardId, new BoardEvent("list-created", listDto));
        return listDto;
    }

    public async Task<ListDto?> UpdateAsync(int listId, int userId, UpdateListDto dto)
    {
        var list = await _db.Lists
            .Include(l => l.Board)
            .FirstOrDefaultAsync(l => l.Id == listId);
        if (list is null || list.Board.OwnerId != userId) return null;

        if (dto.Name is not null) list.Name = dto.Name.Trim();

        if (dto.Position.HasValue && dto.Position.Value != list.Position)
        {
            var oldPos = list.Position;
            var newPos = dto.Position.Value;
            var count = await _db.Lists.CountAsync(l => l.BoardId == list.BoardId);
            if (newPos < 0) newPos = 0;
            if (newPos >= count) newPos = count - 1;

            if (newPos < oldPos)
            {
                await _db.Lists
                    .Where(l => l.BoardId == list.BoardId && l.Id != list.Id
                                && l.Position >= newPos && l.Position < oldPos)
                    .ExecuteUpdateAsync(s => s.SetProperty(l => l.Position, l => l.Position + 1));
            }
            else
            {
                await _db.Lists
                    .Where(l => l.BoardId == list.BoardId && l.Id != list.Id
                                && l.Position > oldPos && l.Position <= newPos)
                    .ExecuteUpdateAsync(s => s.SetProperty(l => l.Position, l => l.Position - 1));
            }
            list.Position = newPos;
        }

        await _db.SaveChangesAsync();
        var listDto = new ListDto(list.Id, list.BoardId, list.Name, list.Position, new List<CardDto>());
        _bus.Publish(list.Board.Id, new BoardEvent("list-updated", listDto));
        return listDto;
    }

    public async Task<bool> DeleteAsync(int listId, int userId)
    {
        var list = await _db.Lists
            .Include(l => l.Board)
            .FirstOrDefaultAsync(l => l.Id == listId);
        if (list is null || list.Board.OwnerId != userId) return false;

        var boardId = list.BoardId;
        var oldPos = list.Position;
        _db.Lists.Remove(list);
        await _db.SaveChangesAsync();

        await _db.Lists
            .Where(l => l.BoardId == boardId && l.Position > oldPos)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.Position, l => l.Position - 1));

        _bus.Publish(boardId, new BoardEvent("list-deleted", new { listId }));
        return true;
    }
}
