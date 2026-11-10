using System.Text.Json;

namespace Kalories.Server.Services;

public static class MealPrompt
{
    public const string System = """
        Eres un nutricionista que estima las calorías y los macronutrientes de un plato a partir de una foto.

        - Identifica cada alimento distinto que se vea en el plato.
        - Estima la ración de cada uno en gramos usando las pistas visibles (tamaño del plato, cubiertos, envases, manos).
        - Da las kcal, proteínas, hidratos y grasas de la ración estimada, no por 100 g.
        - Incluye el aceite, las salsas y los aliños solo si se ven o son muy probables, y menciónalo en las notas.
        - Si la foto no muestra comida ni bebida, responde con is_food en false y la lista de alimentos vacía.
        - confidence: "high" solo si la foto es nítida y las raciones son fáciles de estimar; "low" si está borrosa, hay ingredientes ocultos o las raciones son muy inciertas; "medium" en el resto de casos.
        - dish_name y notes van en español. notes son una o dos frases cortas sobre las suposiciones principales.
        """;

    public const string SchemaJson = """
        {
          "type": "object",
          "properties": {
            "is_food": { "type": "boolean" },
            "dish_name": { "type": "string" },
            "confidence": { "type": "string", "enum": ["low", "medium", "high"] },
            "notes": { "type": "string" },
            "items": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "name": { "type": "string" },
                  "grams": { "type": "number" },
                  "kcal": { "type": "number" },
                  "protein_g": { "type": "number" },
                  "carbs_g": { "type": "number" },
                  "fat_g": { "type": "number" }
                },
                "required": ["name", "grams", "kcal", "protein_g", "carbs_g", "fat_g"],
                "additionalProperties": false
              }
            }
          },
          "required": ["is_food", "dish_name", "confidence", "notes", "items"],
          "additionalProperties": false
        }
        """;

    private static readonly JsonSerializerOptions SnakeCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    // Lee el JSON que devuelve la IA y acota los valores y los textos:
    // no nos fiamos de lo que viene de fuera.
    public static MealAnalysis? ReadAnalysis(string json)
    {
        var a = JsonSerializer.Deserialize<MealAnalysis>(json, SnakeCase);
        if (a is null) return null;

        var confidence = a.Confidence is "low" or "medium" or "high" ? a.Confidence : "medium";

        var items = (a.Items ?? [])
            .Take(20)
            .Select(i => new AnalyzedItem(
                Clean(i.Name, 100),
                Limit(i.Grams, 3000),
                Limit(i.Kcal, 5000),
                Limit(i.ProteinG, 500),
                Limit(i.CarbsG, 800),
                Limit(i.FatG, 500)))
            .Where(i => i.Name.Length > 0 && i.Grams > 0)
            .ToList();

        return new MealAnalysis(a.IsFood, Clean(a.DishName, 150), confidence, Clean(a.Notes, 1000), items);
    }

    private static decimal Limit(decimal value, decimal max) => Math.Round(Math.Clamp(value, 0, max), 1);

    private static string Clean(string? s, int max)
    {
        var t = (s ?? "").Trim();
        return t.Length <= max ? t : t[..max];
    }

    public static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}