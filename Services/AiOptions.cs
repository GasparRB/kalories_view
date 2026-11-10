namespace Kalories.Server.Services;

public class AiOptions
{
    public string Provider { get; set; } = "Gemini"; // "Gemini" o "Claude"
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";          // vacío = el modelo por defecto del proveedor
    public int DailyLimit { get; set; } = 20;
}