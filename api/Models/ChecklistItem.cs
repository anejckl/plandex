namespace Plandex.Api.Models;

public class ChecklistItem
{
    public int Id { get; set; }
    public int ChecklistId { get; set; }
    public string Text { get; set; } = null!;
    public bool IsDone { get; set; }
    public int Position { get; set; }

    public Checklist Checklist { get; set; } = null!;
}
