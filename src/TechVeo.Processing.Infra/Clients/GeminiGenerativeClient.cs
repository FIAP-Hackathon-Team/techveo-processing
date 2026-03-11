using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TechVeo.Processing.Application.Clients;

namespace TechVeo.Processing.Infra.Clients;

[ExcludeFromCodeCoverage]
public class GeminiGenerativeClient : IGenerativeClient
{
    private readonly string _apiKey;
    private readonly string _modelId;
    private readonly ILogger<GeminiGenerativeClient> _logger;
    private readonly Client _genaiClient;

    public GeminiGenerativeClient(IConfiguration configuration, ILogger<GeminiGenerativeClient> logger)
    {
        _logger = logger;
        _apiKey = configuration["Gemini:ApiKey"] ?? string.Empty;
        _modelId = configuration["Gemini:ModelId"] ?? "gemini-3-flash-preview";

        _genaiClient = new Client(apiKey: _apiKey);
    }

    public async Task<IReadOnlyList<(double Second, string Summary)>> ExtractKeyMomentsAsync(string videoKey, string prompt, CancellationToken cancellationToken = default)
    {
        var contents = new List<Content>
        {
            new() { Role = "user", Parts = [new Part { Text = $"Video key: {videoKey}" }] },
            new() { Role = "user", Parts = [new Part { Text = prompt }] },
            new() { Role = "user", Parts = [new Part { Text = "Por favor, retorne apenas JSON com um array de objetos contendo 'second' (numero em segundos) e 'summary' (texto curto). Ex: [{\"second\": 12.5, \"summary\": \"cena X\"}]" }] }
        };

        try
        {
            var config = new GenerateContentConfig { ResponseMimeType = "application/json" };

            var response = await _genaiClient.Models.GenerateContentAsync(model: _modelId, contents: contents, config: config);

            var combined = string.Concat(response.Candidates!
                .SelectMany(c => c.Content?.Parts ?? Enumerable.Empty<Part>())
                .Select(p => p.Text ?? string.Empty));

            if (string.IsNullOrWhiteSpace(combined))
            {
                _logger.LogWarning("Generative API returned empty content for video {VideoKey}", videoKey);
                return Array.Empty<(double, string)>();
            }

            return KeyMomentsParser.Parse(combined);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Generative API call failed for video {VideoKey}", videoKey);
            return Array.Empty<(double, string)>();
        }
    }
}
