namespace Plandex.Api.Models;

public class CardAssignee
{
    public int CardId { get; set; }
    public int UserId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public Card Card { get; set; } = null!;
    public User User { get; set; } = null!;
}
