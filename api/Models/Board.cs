namespace Plandex.Api.Models;

public class Board
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int OwnerId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User Owner { get; set; } = null!;
    public ICollection<BoardList> Lists { get; set; } = new List<BoardList>();
    public ICollection<Label> Labels { get; set; } = new List<Label>();
}
