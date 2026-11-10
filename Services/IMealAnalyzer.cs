namespace Kalories.Server.Services;

public record AnalyzedItem(string Name, decimal Grams, decimal Kcal, decimal ProteinG, decimal CarbsG, decimal FatG);

public record MealAnalysis(bool IsFood, string DishName, string Confidence, string Notes, List<AnalyzedItem> Items);

public class MealAnalysisException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

// Si algún día cambias de proveedor de IA, solo hay que escribir otra clase que implemente esto.
public interface IMealAnalyzer
{
    Task<MealAnalysis> AnalyzeAsync(byte[] jpeg, CancellationToken ct);
}