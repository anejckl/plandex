using Microsoft.EntityFrameworkCore;
using Plandex.Api.Models;

namespace Plandex.Api.Data;

// Shared IQueryable helpers so every service enforces board access the same way.
// Accessible = user is a member (which includes Owner, since owners get a
// BoardMember row with Role=Owner when the board is created and on backfill).
// Owned = user holds the Owner role (used for destructive/admin actions only).
public static class BoardAccessQueries
{
    public static IQueryable<Board> AccessibleBy(this IQueryable<Board> boards, int userId)
        => boards.Where(b => b.Members.Any(m => m.UserId == userId));

    public static IQueryable<Board> OwnedBy(this IQueryable<Board> boards, int userId)
        => boards.Where(b => b.Members.Any(m => m.UserId == userId && m.Role == BoardRole.Owner));

    public static IQueryable<BoardList> AccessibleBy(this IQueryable<BoardList> lists, int userId)
        => lists.Where(l => l.Board.Members.Any(m => m.UserId == userId));

    public static IQueryable<Card> AccessibleBy(this IQueryable<Card> cards, int userId)
        => cards.Where(c => c.List.Board.Members.Any(m => m.UserId == userId));
}
