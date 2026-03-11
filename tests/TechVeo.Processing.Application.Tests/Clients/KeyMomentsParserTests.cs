using FluentAssertions;
using TechVeo.Processing.Infra.Clients;
using Xunit;

namespace TechVeo.Processing.Application.Tests.Clients;

public class KeyMomentsParserTests
{
    [Fact(DisplayName = "Should parse valid JSON with multiple moments")]
    [Trait("Infra", "KeyMomentsParser")]
    public void Parse_WithValidJson_ShouldReturnMoments()
    {
        // Arrange
        var json = """[{"second": 5.0, "summary": "Opening scene"}, {"second": 15.5, "summary": "Key moment"}]""";

        // Act
        var result = KeyMomentsParser.Parse(json);

        // Assert
        result.Should().HaveCount(2);
        result[0].Second.Should().Be(5.0);
        result[0].Summary.Should().Be("Opening scene");
        result[1].Second.Should().Be(15.5);
        result[1].Summary.Should().Be("Key moment");
    }

    [Fact(DisplayName = "Should return empty list for empty JSON array")]
    [Trait("Infra", "KeyMomentsParser")]
    public void Parse_WithEmptyArray_ShouldReturnEmpty()
    {
        // Act
        var result = KeyMomentsParser.Parse("[]");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact(DisplayName = "Should return empty list for null input")]
    [Trait("Infra", "KeyMomentsParser")]
    public void Parse_WithNullInput_ShouldReturnEmpty()
    {
        // Act
        var result = KeyMomentsParser.Parse(null);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact(DisplayName = "Should return empty list for empty string")]
    [Trait("Infra", "KeyMomentsParser")]
    public void Parse_WithEmptyString_ShouldReturnEmpty()
    {
        // Act
        var result = KeyMomentsParser.Parse("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact(DisplayName = "Should return empty list for whitespace string")]
    [Trait("Infra", "KeyMomentsParser")]
    public void Parse_WithWhitespaceString_ShouldReturnEmpty()
    {
        // Act
        var result = KeyMomentsParser.Parse("   \t\n  ");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact(DisplayName = "Should return empty list for invalid JSON")]
    [Trait("Infra", "KeyMomentsParser")]
    public void Parse_WithInvalidJson_ShouldReturnEmpty()
    {
        // Act
        var result = KeyMomentsParser.Parse("not a json at all");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact(DisplayName = "Should use 0 for missing second field")]
    [Trait("Infra", "KeyMomentsParser")]
    public void Parse_WithMissingSecond_ShouldDefaultToZero()
    {
        // Arrange
        var json = """[{"summary": "No timestamp"}]""";

        // Act
        var result = KeyMomentsParser.Parse(json);

        // Assert
        result.Should().HaveCount(1);
        result[0].Second.Should().Be(0);
        result[0].Summary.Should().Be("No timestamp");
    }

    [Fact(DisplayName = "Should use empty string for missing summary field")]
    [Trait("Infra", "KeyMomentsParser")]
    public void Parse_WithMissingSummary_ShouldDefaultToEmptyString()
    {
        // Arrange
        var json = """[{"second": 10.5}]""";

        // Act
        var result = KeyMomentsParser.Parse(json);

        // Assert
        result.Should().HaveCount(1);
        result[0].Second.Should().Be(10.5);
        result[0].Summary.Should().Be("");
    }

    [Fact(DisplayName = "Should be case insensitive for JSON property names")]
    [Trait("Infra", "KeyMomentsParser")]
    public void Parse_WithUppercaseProperties_ShouldParseCaseInsensitively()
    {
        // Arrange
        var json = """[{"Second": 7.5, "Summary": "Case insensitive"}]""";

        // Act
        var result = KeyMomentsParser.Parse(json);

        // Assert
        result.Should().HaveCount(1);
        result[0].Second.Should().Be(7.5);
        result[0].Summary.Should().Be("Case insensitive");
    }

    [Fact(DisplayName = "Should handle single moment correctly")]
    [Trait("Infra", "KeyMomentsParser")]
    public void Parse_WithSingleMoment_ShouldReturnOneMoment()
    {
        // Arrange
        var json = """[{"second": 42.0, "summary": "The answer"}]""";

        // Act
        var result = KeyMomentsParser.Parse(json);

        // Assert
        result.Should().HaveCount(1);
        result[0].Second.Should().Be(42.0);
        result[0].Summary.Should().Be("The answer");
    }

    [Fact(DisplayName = "Should handle large number of moments")]
    [Trait("Infra", "KeyMomentsParser")]
    public void Parse_WithManyMoments_ShouldReturnAll()
    {
        // Arrange
        var items = new System.Text.StringBuilder("[");
        for (int i = 0; i < 50; i++)
        {
            if (i > 0) items.Append(',');
            items.Append($"{{\"second\": {i}.0, \"summary\": \"Moment {i}\"}}");
        }
        items.Append(']');

        // Act
        var result = KeyMomentsParser.Parse(items.ToString());

        // Assert
        result.Should().HaveCount(50);
        result[49].Second.Should().Be(49.0);
    }

    [Fact(DisplayName = "Should handle decimal seconds precisely")]
    [Trait("Infra", "KeyMomentsParser")]
    public void Parse_WithDecimalSeconds_ShouldPreservePrecision()
    {
        // Arrange
        var json = """[{"second": 12.345, "summary": "Precise"}]""";

        // Act
        var result = KeyMomentsParser.Parse(json);

        // Assert
        result[0].Second.Should().BeApproximately(12.345, 0.001);
    }
}
