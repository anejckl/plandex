namespace Plandex.Api.Models;

public class TimeEntry
{
    public int Id { get; set; }
    public int CardId { get; set; }
    public int UserId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int? DurationSeconds { get; set; }

    public Card Card { get; set; } = null!;
    public User User { get; set; } = null!;
}
