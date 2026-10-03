using ServerManager.Application.Monitoring;

namespace ServerManager.Application.Tests.Monitoring;

public class MetricRangesTests
{
    [Theory]
    [InlineData("1h", MetricRange.OneHour)]
    [InlineData("6h", MetricRange.SixHours)]
    [InlineData("24H", MetricRange.OneDay)]
    [InlineData(" 7d ", MetricRange.SevenDays)]
    [InlineData("30d", MetricRange.ThirtyDays)]
    [InlineData(null, MetricRange.OneHour)]
    [InlineData("'; DROP TABLE Servers;--", MetricRange.OneHour)]
    public void Parse_maps_codes_and_defaults_to_one_hour(string? code, MetricRange expected)
    {
        Assert.Equal(expected, MetricRanges.Parse(code));
    }

    [Fact]
    public void Code_round_trips_for_all_ranges()
    {
        foreach (var range in MetricRanges.All)
            Assert.Equal(range, MetricRanges.Parse(MetricRanges.Code(range)));
    }

    [Fact]
    public void Long_ranges_use_hourly_aggregates()
    {
        Assert.False(MetricRanges.UsesHourlyData(MetricRange.OneDay));
        Assert.True(MetricRanges.UsesHourlyData(MetricRange.SevenDays));
        Assert.True(MetricRanges.UsesHourlyData(MetricRange.ThirtyDays));
    }
}
