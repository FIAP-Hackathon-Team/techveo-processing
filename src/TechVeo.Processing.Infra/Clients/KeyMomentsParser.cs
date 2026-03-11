using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TechVeo.Processing.Infra.Clients;

internal static class KeyMomentsParser
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    internal static IReadOnlyList<(double Second, string Summary)> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<(double, string)>();

        try
        {
            var items = JsonSerializer.Deserialize<List<KeyMomentDto>>(json, JsonOptions);
            if (items == null)
                return Array.Empty<(double, string)>();

            return items
                .Select(it => (it.Second ?? 0, it.Summary ?? string.Empty))
                .ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<(double, string)>();
        }
    }
}

internal class KeyMomentDto
{
    [JsonPropertyName("second")]
    public double? Second { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }
}
