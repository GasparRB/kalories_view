using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace Kalories.Server.Services;

public class ClaudeMealAnalyzer(
    HttpClient http,
    IOptions<AiOptions> options,
    ILogger<ClaudeMealAnalyzer> logger) : IMealAnalyzer
{
    private const string ApiVersion = "2026-10-21";
    private const string DefaultModel = "claude-sonnet-5-5";

    public async Task<MealAnalysis> AnalyzeAsync(byte[] jpeg, CancellationToken ct)
    {
        var config = options.Value;
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            logger.LogError("Falta la clave de la IA (Ai:ApiKey).");
            throw new MealAnalysisException("El análisis de platos no está configurado.", 500);
        }

        var body = new JsonObject
        {
            ["model"] = string.IsNullOrWhiteSpace(config.Model) ? DefaultModel : config.Model,
            ["max_tokens"] = 2000,
            ["system"] = MealPrompt.System,
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "image",
                            ["source"] = new JsonObject
                            {
                                ["type"] = "base64",
                                ["media_type"] = "image/jpeg",
                                ["data"] = Convert.ToBase64String(jpeg)
                            }
                        },
                        new JsonObject
                        {
                            ["type"] = "text",
                            ["text"] = "Analiza este plato."
                        }
                    }
                }
            },
            ["output_config"] = new JsonObject
            {
                ["format"] = new JsonObject
                {
                    ["type"] = "json_schema",
                    ["schema"] = JsonNode.Parse(MealPrompt.SchemaJson)
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", config.ApiKey);
        request.Headers.Add("anthropic-version", ApiVersion);

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
                    (int)response.StatusCode, MealPrompt.Truncate(text, 500));

                throw response.StatusCode switch
                {
                    HttpStatusCode.TooManyRequests =>
                        new MealAnalysisException("El servicio de IA está saturado. Inténtalo de nuevo en un minuto.", 503),
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                        new MealAnalysisException("El análisis de platos no está disponible ahora mismo.", 500),
                    HttpStatusCode.BadRequest =>
                        new MealAnalysisException("No se pudo analizar la imagen.", 500),
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

            if (root.TryGetProperty("usage", out var usage)
                && usage.TryGetProperty("input_tokens", out var tokensIn)
                && usage.TryGetProperty("output_tokens", out var tokensOut))
            {
                logger.LogInformation("Análisis de plato: {In} tokens de entrada y {Out} de salida.",
                    tokensIn.GetInt32(), tokensOut.GetInt32());
            }

            var stop = root.TryGetProperty("stop_reason", out var s) ? s.GetString() : null;
            if (stop == "refusal")
                throw new MealAnalysisException("La IA no pudo analizar esta imagen.", 422);
            if (stop == "max_tokens")
                throw new MealAnalysisException("El análisis ha salido incompleto. Inténtalo de nuevo.", 502);

            string? json = null;
            foreach (var block in root.GetProperty("content").EnumerateArray())
            {
                if (block.GetProperty("type").GetString() == "text")
                {
                    json = block.GetProperty("text").GetString();
                    break;
                }
            }

            var analysis = json is null ? null : MealPrompt.ReadAnalysis(json);
            if (analysis is null)
                throw new MealAnalysisException("Respuesta no válida de la IA.", 502);

            return analysis;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            logger.LogWarning(ex, "No se pudo interpretar la respuesta de la IA.");
            throw new MealAnalysisException("Respuesta no válida de la IA.", 502);
        }
    }
}