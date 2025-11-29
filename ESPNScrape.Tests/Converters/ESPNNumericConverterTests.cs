using System.Text.Json;
using ESPNScrape.Converters;
using Xunit;

namespace ESPNScrape.Tests.Converters;

public class ESPNNumericConverterTests
{
    private class TestModel
    {
        [System.Text.Json.Serialization.JsonConverter(typeof(ESPNNumericConverter))]
        public double Value { get; set; }
    }

    [Theory]
    [InlineData("15.5", 15.5)]
    [InlineData("0", 0)]
    [InlineData("-5.2", -5.2)]
    [InlineData("-", 0)]
    [InlineData("N/A", 0)]
    [InlineData("", 0)]
    [InlineData("66.7%", 66.7)]
    public void Read_ShouldConvertVariousFormats_ToDouble(string jsonValue, double expected)
    {
        // Arrange
        var json = $"{{\"Value\": \"{jsonValue}\"}}";

        // Act
        var result = JsonSerializer.Deserialize<TestModel>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public void Read_ShouldHandleNumericTypes()
    {
        // Arrange
        var json = "{\"Value\": 42.5}";

        // Act
        var result = JsonSerializer.Deserialize<TestModel>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(42.5, result.Value);
    }
}
