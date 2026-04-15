namespace Plandex.Api.Models;

public class Card
{
    public int Id { get; set; }
    public int ListId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public int Position { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ArchivedAt { get; set; }

    public BoardList List { get; set; } = null!;
    public ICollection<CardLabel> CardLabels { get; set; } = new List<CardLabel>();
    public ICollection<Checklist> Checklists { get; set; } = new List<Checklist>();
    public ICollection<TimeEntry> TimeEntries { get; set; } = new List<TimeEntry>();
}
