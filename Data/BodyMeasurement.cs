namespace Kalories.Server.Data;

public class BodyMeasurement
{
    public int Id { get; set; }

    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;

    public DateTime MeasuredAt { get; set; } = DateTime.UtcNow;
    public decimal WeightKg { get; set; }
    public decimal? WaistCm { get; set; }
    public decimal? HipCm { get; set; }
    public decimal? ChestCm { get; set; }
}
