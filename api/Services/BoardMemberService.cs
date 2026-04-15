using Microsoft.EntityFrameworkCore;
using Plandex.Api.Data;
using Plandex.Api.DTOs;
using Plandex.Api.Models;

namespace Plandex.Api.Services;

public enum AddMemberResult { Ok, BoardNotFound, NotOwner, UserNotFound, AlreadyMember }
public enum RemoveMemberResult { Ok, BoardNotFound, NotAuthorized, NotAMember, CannotRemoveLastOwner }

public interface IBoardMemberService
{
    Task<IReadOnlyList<BoardMemberDto>?> ListAsync(int boardId, int userId);
    Task<(AddMemberResult result, BoardMemberDto? member)> AddAsync(int boardId, int requesterId, string email);
    Task<RemoveMemberResult> RemoveAsync(int boardId, int targetUserId, int requesterId);
}

public class BoardMemberService : IBoardMemberService
{
    private readonly PlandexDbContext _db;
    private readonly IBoardEventBus _bus;

    public BoardMemberService(PlandexDbContext db, IBoardEventBus bus)
    {
        _db = db;
        _bus = bus;
    }

    public async Task<IReadOnlyList<BoardMemberDto>?> ListAsync(int boardId, int userId)
    {
        // Only members of the board may list other members.
        var isMember = await _db.BoardMembers.AnyAsync(m => m.BoardId == boardId && m.UserId == userId);
        if (!isMember) return null;

        return await _db.BoardMembers
            .Where(m => m.BoardId == boardId)
            .Include(m => m.User)
            .OrderBy(m => m.Role)
            .ThenBy(m => m.User.Name)
            .Select(m => new BoardMemberDto(m.UserId, m.User.Email, m.User.Name, m.Role.ToString(), m.AddedAt))
            .ToListAsync();
    }

    public async Task<(AddMemberResult, BoardMemberDto?)> AddAsync(int boardId, int requesterId, string email)
    {
        var board = await _db.Boards
            .Include(b => b.Members)
            .FirstOrDefaultAsync(b => b.Id == boardId);
        if (board is null) return (AddMemberResult.BoardNotFound, null);

        var requesterIsOwner = board.Members.Any(m => m.UserId == requesterId && m.Role == BoardRole.Owner);
        if (!requesterIsOwner)
        {
            // Don't leak board existence to non-members — return BoardNotFound instead of NotOwner
            // unless the requester is at least a member (in which case NotOwner is fair).
            if (!board.Members.Any(m => m.UserId == requesterId)) return (AddMemberResult.BoardNotFound, null);
            return (AddMemberResult.NotOwner, null);
        }

        var normalized = email.Trim().ToLowerInvariant();
        var target = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalized);
        if (target is null) return (AddMemberResult.UserNotFound, null);

        if (board.Members.Any(m => m.UserId == target.Id))
            return (AddMemberResult.AlreadyMember, null);

        var now = DateTime.UtcNow;
        var member = new BoardMember
        {
            BoardId = boardId,
            UserId = target.Id,
            Role = BoardRole.Member,
            AddedAt = now,
        };
        _db.BoardMembers.Add(member);
        await _db.SaveChangesAsync();

        var dto = new BoardMemberDto(target.Id, target.Email, target.Name, member.Role.ToString(), now);
        _bus.Publish(boardId, new BoardEvent("member-added", dto));
        return (AddMemberResult.Ok, dto);
    }

    public async Task<RemoveMemberResult> RemoveAsync(int boardId, int targetUserId, int requesterId)
    {
        var board = await _db.Boards
            .Include(b => b.Members)
            .FirstOrDefaultAsync(b => b.Id == boardId);
        if (board is null) return RemoveMemberResult.BoardNotFound;

        var requesterMembership = board.Members.FirstOrDefault(m => m.UserId == requesterId);
        if (requesterMembership is null) return RemoveMemberResult.BoardNotFound;

        var targetMembership = board.Members.FirstOrDefault(m => m.UserId == targetUserId);
        if (targetMembership is null) return RemoveMemberResult.NotAMember;

        // Allowed when: requester is owner, OR requester is target (self-leave).
        var requesterIsOwner = requesterMembership.Role == BoardRole.Owner;
        var isSelf = requesterId == targetUserId;
        if (!requesterIsOwner && !isSelf) return RemoveMemberResult.NotAuthorized;

        // Never leave a board without an owner.
        if (targetMembership.Role == BoardRole.Owner)
        {
            var ownerCount = board.Members.Count(m => m.Role == BoardRole.Owner);
            if (ownerCount <= 1) return RemoveMemberResult.CannotRemoveLastOwner;
        }

        _db.BoardMembers.Remove(targetMembership);

        // Removed member loses any card assignments on this board's cards.
        await _db.CardAssignees
            .Where(a => a.UserId == targetUserId && a.Card.List.BoardId == boardId)
            .ExecuteDeleteAsync();

        await _db.SaveChangesAsync();

        _bus.Publish(boardId, new BoardEvent("member-removed", new { userId = targetUserId }));
        return RemoveMemberResult.Ok;
    }
}
