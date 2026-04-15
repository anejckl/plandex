namespace Plandex.Api.Models;

public enum BoardRole
{
    Owner = 0,
    Member = 1,
}

public class BoardMember
{
    public int BoardId { get; set; }
    public int UserId { get; set; }
    public BoardRole Role { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public Board Board { get; set; } = null!;
    public User User { get; set; } = null!;
}
