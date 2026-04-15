namespace Plandex.Api.Models;

public class BoardList
{
    public int Id { get; set; }
    public int BoardId { get; set; }
    public string Name { get; set; } = null!;
    public int Position { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Board Board { get; set; } = null!;
    public ICollection<Card> Cards { get; set; } = new List<Card>();
}
