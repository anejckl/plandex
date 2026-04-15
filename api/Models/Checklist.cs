namespace Plandex.Api.Models;

public class Checklist
{
    public int Id { get; set; }
    public int CardId { get; set; }
    public string Title { get; set; } = null!;

    public Card Card { get; set; } = null!;
    public ICollection<ChecklistItem> Items { get; set; } = new List<ChecklistItem>();
}
