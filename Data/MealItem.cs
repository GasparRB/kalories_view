namespace Kalories.Server.Data;

public class MealItem
{
    public int Id { get; set; }

    public int MealId { get; set; }
    public Meal Meal { get; set; } = null!;

    public string Name { get; set; } = "";
    public decimal Grams { get; set; }
    public decimal Kcal { get; set; }
    public decimal ProteinG { get; set; }
    public decimal CarbsG { get; set; }
    public decimal FatG { get; set; }
}
