namespace Kalories.Server.Data;

public class Meal
{
    public int Id { get; set; }

    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Title { get; set; } = "";
    public string Confidence { get; set; } = "medium"; // low, medium o high
    public string Notes { get; set; } = "";
    public string? PhotoPath { get; set; }

    public List<MealItem> Items { get; set; } = [];
}