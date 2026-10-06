using ServerManager.Web.Helpers;

namespace ServerManager.Web.Tests;

public class MetricDisplayTests
{
    [Fact]
    public void Used_of_total_formats_both_sizes()
    {
        Assert.Equal("3,2 GB / 8 GB", MetricDisplay.UsedOfTotal((long)(3.2 * 1024 * 1024 * 1024), 8L * 1024 * 1024 * 1024));
    }

    [Fact]
    public void Used_of_total_is_dash_when_total_is_unknown()
    {
        Assert.Equal("—", MetricDisplay.UsedOfTotal(0, 0));
    }

    [Fact]
    public void Cpu_cores_converts_percent_to_core_equivalent()
    {
        Assert.Equal("1,6 / 4 çekirdek", MetricDisplay.CpuCores(40, 4));
    }

    [Theory]
    [InlineData(null, 4)]
    [InlineData(40d, null)]
    [InlineData(40d, 0)]
    public void Cpu_cores_is_dash_without_percent_or_threads(double? percent, int? threads)
    {
        Assert.Equal("—", MetricDisplay.CpuCores(percent, threads));
    }
}
