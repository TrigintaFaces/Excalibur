using Excalibur.Dispatch.Routing.LoadBalancing;

namespace Excalibur.Dispatch.Tests.Routing;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RouteHealthStatusShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        var status = new RouteHealthStatus();

        status.RouteId.ShouldBe(string.Empty);
        status.IsHealthy.ShouldBeFalse();
        status.LastCheck.ShouldBeGreaterThan(DateTimeOffset.MinValue);
        status.ConsecutiveFailures.ShouldBe(0);
        status.AverageLatency.ShouldBe(TimeSpan.Zero);
        status.SuccessRate.ShouldBe(0.0);
        status.Metadata.ShouldNotBeNull();
        status.Metadata.Count.ShouldBe(0);
    }

    [Fact]
    public void AllowSettingAllProperties()
    {
        var now = DateTimeOffset.UtcNow;
        var status = new RouteHealthStatus
        {
            RouteId = "route-1",
            IsHealthy = true,
            LastCheck = now,
            ConsecutiveFailures = 2,
            AverageLatency = TimeSpan.FromMilliseconds(50),
            SuccessRate = 0.95,
        };

        status.RouteId.ShouldBe("route-1");
        status.IsHealthy.ShouldBeTrue();
        status.LastCheck.ShouldBe(now);
        status.ConsecutiveFailures.ShouldBe(2);
        status.AverageLatency.ShouldBe(TimeSpan.FromMilliseconds(50));
        status.SuccessRate.ShouldBe(0.95);
    }

    [Fact]
    public void AllowPopulatingMetadata()
    {
        var status = new RouteHealthStatus
        {
            Metadata =
            {
                ["region"] = "us-east-1",
                ["version"] = "v2",
            },
        };

        status.Metadata.Count.ShouldBe(2);
        status.Metadata["region"].ShouldBe("us-east-1");
        status.Metadata["version"].ShouldBe("v2");
    }
}
