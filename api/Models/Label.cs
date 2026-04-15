namespace Plandex.Api.Models;

public class Label
{
    public int Id { get; set; }
    public int BoardId { get; set; }
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;

    public Board Board { get; set; } = null!;
    public ICollection<CardLabel> CardLabels { get; set; } = new List<CardLabel>();
}
