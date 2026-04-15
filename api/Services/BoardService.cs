using Microsoft.EntityFrameworkCore;
using Plandex.Api.Data;
using Plandex.Api.DTOs;
using Plandex.Api.Models;

namespace Plandex.Api.Services;

public interface IBoardService
{
    Task<IReadOnlyList<BoardSummaryDto>> ListAsync(int userId);
    Task<BoardDetailDto?> GetAsync(int boardId, int userId);
    Task<BoardSummaryDto> CreateAsync(int userId, string name);
    Task<bool> RenameAsync(int boardId, int userId, string name);
    Task<bool> DeleteAsync(int boardId, int userId);
}

public class BoardService : IBoardService
{
    private readonly PlandexDbContext _db;
    private readonly IBoardEventBus _bus;

    public BoardService(PlandexDbContext db, IBoardEventBus bus)
    {
        _db = db;
        _bus = bus;
    }

    public async Task<IReadOnlyList<BoardSummaryDto>> ListAsync(int userId)
    {
        return await _db.Boards
            .AccessibleBy(userId)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BoardSummaryDto(b.Id, b.Name, b.CreatedAt))
            .ToListAsync();
    }

    public async Task<BoardDetailDto?> GetAsync(int boardId, int userId)
    {
        var board = await _db.Boards
            .AccessibleBy(userId)
            .Include(b => b.Labels)
            .Include(b => b.Members).ThenInclude(m => m.User)
            .Include(b => b.Lists).ThenInclude(l => l.Cards).ThenInclude(c => c.CardLabels).ThenInclude(cl => cl.Label)
            .Include(b => b.Lists).ThenInclude(l => l.Cards).ThenInclude(c => c.Checklists).ThenInclude(ch => ch.Items)
            .Include(b => b.Lists).ThenInclude(l => l.Cards).ThenInclude(c => c.TimeEntries)
            .Include(b => b.Lists).ThenInclude(l => l.Cards).ThenInclude(c => c.Assignees).ThenInclude(a => a.User)
            .FirstOrDefaultAsync(b => b.Id == boardId);

        if (board is null) return null;

        return new BoardDetailDto(
            board.Id,
            board.Name,
            board.CreatedAt,
            board.Lists
                .OrderBy(l => l.Position)
                .Select(l => new ListDto(
                    l.Id,
                    l.BoardId,
                    l.Name,
                    l.Position,
                    l.Cards.Where(c => c.ArchivedAt == null).OrderBy(c => c.Position).Select(c => MapCard(c, userId)).ToList()))
                .ToList(),
            board.Labels
                .Select(lb => new LabelDto(lb.Id, lb.BoardId, lb.Name, lb.Color))
                .ToList(),
            board.Members
                .OrderBy(m => m.Role)
                .ThenBy(m => m.User.Name)
                .Select(m => new BoardMemberDto(m.UserId, m.User.Email, m.User.Name, m.Role.ToString(), m.AddedAt))
                .ToList());
    }

    public static CardDto MapCard(Card c, int userId)
    {
        var totalClosed = c.TimeEntries
            .Where(t => t.DurationSeconds.HasValue)
            .Sum(t => t.DurationSeconds!.Value);
        var active = c.TimeEntries.FirstOrDefault(t => t.UserId == userId && t.EndedAt == null);
        var checklistItems = c.Checklists.SelectMany(cl => cl.Items).ToList();
        return new CardDto(
            c.Id,
            c.ListId,
            c.Title,
            c.Description,
            c.Position,
            c.DueDate,
            c.CardLabels.Select(cl => new LabelDto(cl.LabelId, cl.Label.BoardId, cl.Label.Name, cl.Label.Color)).ToList(),
            checklistItems.Count,
            checklistItems.Count(i => i.IsDone),
            totalClosed,
            active?.StartedAt,
            c.Assignees.Select(a => new AssigneeDto(a.UserId, a.User.Name, a.User.Email)).ToList());
    }

    public async Task<BoardSummaryDto> CreateAsync(int userId, string name)
    {
        var board = new Board { Name = name.Trim(), OwnerId = userId };
        _db.Boards.Add(board);
        await _db.SaveChangesAsync();

        // Owner is a BoardMember with role Owner — the single source of truth for
        // access checks. OwnerId on Board is kept for "who created it" semantics.
        _db.BoardMembers.Add(new BoardMember
        {
            BoardId = board.Id,
            UserId = userId,
            Role = BoardRole.Owner,
            AddedAt = board.CreatedAt,
        });
        await _db.SaveChangesAsync();

        return new BoardSummaryDto(board.Id, board.Name, board.CreatedAt);
    }

    public async Task<bool> RenameAsync(int boardId, int userId, string name)
    {
        var board = await _db.Boards.OwnedBy(userId).FirstOrDefaultAsync(b => b.Id == boardId);
        if (board is null) return false;
        board.Name = name.Trim();
        await _db.SaveChangesAsync();
        _bus.Publish(boardId, new BoardEvent("board-updated", new { id = boardId, name = board.Name }));
        return true;
    }

    public async Task<bool> DeleteAsync(int boardId, int userId)
    {
        var board = await _db.Boards.OwnedBy(userId).FirstOrDefaultAsync(b => b.Id == boardId);
        if (board is null) return false;
        _db.Boards.Remove(board);
        await _db.SaveChangesAsync();
        return true;
    }
}
