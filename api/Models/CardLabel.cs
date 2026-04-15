namespace Plandex.Api.Models;

public class CardLabel
{
    public int CardId { get; set; }
    public int LabelId { get; set; }

    public Card Card { get; set; } = null!;
    public Label Label { get; set; } = null!;
}
