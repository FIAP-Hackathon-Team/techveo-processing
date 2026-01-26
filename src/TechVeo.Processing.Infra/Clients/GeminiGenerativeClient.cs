using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TechVeo.Processing.Application.Clients;

namespace TechVeo.Processing.Infra.Clients;

public class GeminiGenerativeClient : IGenerativeClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _modelId;
    private readonly ILogger<GeminiGenerativeClient> _logger;
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/";

    public GeminiGenerativeClient(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiGenerativeClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["Gemini:ApiKey"] ?? string.Empty;
        _modelId = configuration["Gemini:ModelId"] ?? "gemini-3-flash-preview";
    }

    public async Task<IReadOnlyList<(double Second, string Summary)>> ExtractKeyMomentsAsync(string videoKey, string prompt, CancellationToken cancellationToken = default)
    {
        // Build the request body based on provided sample. We will send a simplified JSON instructing the model to return seconds and short summary lines.
        var request = new
        {
            contents = new object[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new { text = $"Video key: {videoKey}" }
                    }
                },
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new { text = prompt }
                    }
                },
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new { text = "Por favor, retorne apenas JSON com um array de objetos contendo 'second' (numero em segundos) e 'summary' (texto curto). Ex: [{\"second\": 12.5, \"summary\": \"cena X\"}]" }
                    }
                }
            }
        };

        var url = $"{BaseUrl}/{_modelId}:streamGenerateContent?key={_apiKey}";

        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            // The response might be streaming or contain additional wrapper text. Try to find the first JSON array in the response.
            var jsonStart = text.IndexOf('[');
            if (jsonStart < 0)
            {
                _logger.LogWarning("Generative API did not return a JSON array. Response: {Response}", text);
                return Array.Empty<(double, string)>();
            }

            var json = text.Substring(jsonStart);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };

            var items = JsonSerializer.Deserialize<List<KeyMomentDto>>(json, options);
            if (items == null)
            {
                return Array.Empty<(double, string)>();
            }

            var result = new List<(double, string)>();
            foreach (var it in items)
            {
                if (double.TryParse(it.Second?.ToString() ?? string.Empty, out var sec))
                {
                    result.Add((sec, it.Summary ?? string.Empty));
                }
                else if (it.SecondNumber.HasValue)
                {
                    result.Add((it.SecondNumber.Value, it.Summary ?? string.Empty));
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Generative API call failed");
            return Array.Empty<(double, string)>();
        }
    }

    private class KeyMomentDto
    {
        [JsonPropertyName("second")]
        public object? Second { get; set; }

        [JsonPropertyName("second_number")]
        public double? SecondNumber { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }
    }
}
