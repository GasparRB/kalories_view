namespace Kalories.Server.Data;

public enum Sex { NoIndicado = 0, Hombre = 1, Mujer = 2 }
public enum ActivityLevel { Sedentario = 0, Ligero = 1, Moderado = 2, Alto = 3 }
public enum Goal { Mantener = 0, Perder = 1, Ganar = 2 }

public class UserProfile
{
    public int Id { get; set; }

    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;

    public string Name { get; set; } = "";
    public DateOnly? BirthDate { get; set; }
    public Sex Sex { get; set; }
    public decimal? HeightCm { get; set; }
    public ActivityLevel ActivityLevel { get; set; }
    public Goal Goal { get; set; }
    public string? AvatarPath { get; set; }
}