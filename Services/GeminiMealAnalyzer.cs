using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace Kalories.Server.Services;

public class GeminiMealAnalyzer(
    HttpClient http,
    IOptions<AiOptions> options,
    ILogger<GeminiMealAnalyzer> logger) : IMealAnalyzer
{
    private const string DefaultModel = "gemini-2.5-flash";

    public async Task<MealAnalysis> AnalyzeAsync(byte[] jpeg, CancellationToken ct)
    {
        var config = options.Value;
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            logger.LogError("Falta la clave de la IA (Ai:ApiKey).");
            throw new MealAnalysisException("El análisis de platos no está configurado.", 500);
        }

        var model = string.IsNullOrWhiteSpace(config.Model) ? DefaultModel : config.Model;

        // Gemini no admite todas las palabras clave del esquema (por ejemplo additionalProperties).
        var schema = JsonNode.Parse(MealPrompt.SchemaJson);
        RemoveKey(schema, "additionalProperties");

        var body = new JsonObject
        {
            ["systemInstruction"] = new JsonObject
            {
                ["parts"] = new JsonArray { new JsonObject { ["text"] = MealPrompt.System } }
            },
            ["contents"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["parts"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["inlineData"] = new JsonObject
                            {
                                ["mimeType"] = "image/jpeg",
                                ["data"] = Convert.ToBase64String(jpeg)
                            }
                        },
                        new JsonObject { ["text"] = "Analiza este plato." }
                    }
                }
            },
            ["generationConfig"] = new JsonObject
            {
                ["responseMimeType"] = "application/json",
                ["responseJsonSchema"] = schema,
                ["maxOutputTokens"] = 4096
            }
        };

        // La clave va en una cabecera, no en la URL, para que no acabe en los logs.
        using var request = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{model}:generateContent")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-goog-api-key", config.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "No se pudo contactar con la API de IA.");
            throw new MealAnalysisException("No se pudo contactar con el servicio de IA.", 503);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new MealAnalysisException("El servicio de IA ha tardado demasiado.", 504);
        }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("La API de IA respondió {Status}: {Body}",
                    (int)response.StatusCode, MealPrompt.Truncate(text, 800));

                throw response.StatusCode switch
                {
                    HttpStatusCode.TooManyRequests =>
                        new MealAnalysisException("Se ha superado el límite del servicio de IA. Inténtalo de nuevo en un minuto.", 503),
                    HttpStatusCode.BadRequest or HttpStatusCode.Forbidden or HttpStatusCode.NotFound =>
                        new MealAnalysisException("El análisis de platos no está disponible ahora mismo.", 500),
                    _ => new MealAnalysisException("El servicio de IA no está disponible. Inténtalo más tarde.", 503)
                };
            }

            return Parse(text);
        }
    }

    private MealAnalysis Parse(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            if (root.TryGetProperty("usageMetadata", out var usage)
                && usage.TryGetProperty("promptTokenCount", out var tokensIn)
                && usage.TryGetProperty("candidatesTokenCount", out var tokensOut))
            {
                logger.LogInformation("Análisis de plato: {In} tokens de entrada y {Out} de salida.",
                    tokensIn.GetInt32(), tokensOut.GetInt32());
            }

            if (root.TryGetProperty("promptFeedback", out var feedback) && feedback.TryGetProperty("blockReason", out _))
                throw new MealAnalysisException("La IA no pudo analizar esta imagen.", 422);

            var candidate = root.GetProperty("candidates")[0];

            var finish = candidate.TryGetProperty("finishReason", out var f) ? f.GetString() : null;
            if (finish == "MAX_TOKENS")
                throw new MealAnalysisException("El análisis ha salido incompleto. Inténtalo de nuevo.", 502);
            if (finish is "SAFETY" or "PROHIBITED_CONTENT" or "BLOCKLIST" or "IMAGE_SAFETY")
                throw new MealAnalysisException("La IA no pudo analizar esta imagen.", 422);

            string? json = null;
            foreach (var part in candidate.GetProperty("content").GetProperty("parts").EnumerateArray())
            {
                var isThought = part.TryGetProperty("thought", out var th) && th.ValueKind == JsonValueKind.True;
                if (!isThought && part.TryGetProperty("text", out var t))
                {
                    json = t.GetString();
                    break;
                }
            }

            var analysis = json is null ? null : MealPrompt.ReadAnalysis(json);
            if (analysis is null)
                throw new MealAnalysisException("Respuesta no válida de la IA.", 502);

            return analysis;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException
                                       or InvalidOperationException or IndexOutOfRangeException)
        {
            logger.LogWarning(ex, "No se pudo interpretar la respuesta de la IA: {Body}",
                MealPrompt.Truncate(text, 800));
            throw new MealAnalysisException("Respuesta no válida de la IA.", 502);
        }
    }

    private static void RemoveKey(JsonNode? node, string key)
    {
        switch (node)
        {
            case JsonObject obj:
                obj.Remove(key);
                foreach (var child in obj.Select(p => p.Value).ToList())
                    RemoveKey(child, key);
                break;
            case JsonArray arr:
                foreach (var child in arr)
                    RemoveKey(child, key);
                break;
        }
    }
}