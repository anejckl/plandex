namespace Plandex.Api.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string Name { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Board> Boards { get; set; } = new List<Board>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<TimeEntry> TimeEntries { get; set; } = new List<TimeEntry>();
    public ICollection<BoardMember> BoardMemberships { get; set; } = new List<BoardMember>();
    public ICollection<CardAssignee> CardAssignments { get; set; } = new List<CardAssignee>();
    public ICollection<Card> CreatedCards { get; set; } = new List<Card>();
}
