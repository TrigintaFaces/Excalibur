using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class HealthCheckResultShould
{
	[Fact]
	public void Healthy_CreateHealthyResult()
	{
		var result = HealthCheckResult.Healthy();

		result.IsHealthy.ShouldBeTrue();
		result.Description.ShouldBe("Healthy");
		result.Exception.ShouldBeNull();
		result.CheckTimestamp.ShouldNotBe(default);
		result.CheckTimestamp.Kind.ShouldBe(DateTimeKind.Utc);
	}

	[Fact]
	public void Healthy_CreateWithCustomDescription()
	{
		var result = HealthCheckResult.Healthy("All good");

		result.IsHealthy.ShouldBeTrue();
		result.Description.ShouldBe("All good");
	}

	[Fact]
	public void Unhealthy_CreateUnhealthyResult()
	{
		var result = HealthCheckResult.Unhealthy("Connection failed");

		result.IsHealthy.ShouldBeFalse();
		result.Description.ShouldBe("Connection failed");
		result.Exception.ShouldBeNull();
		result.CheckTimestamp.ShouldNotBe(default);
		result.CheckTimestamp.Kind.ShouldBe(DateTimeKind.Utc);
	}

	[Fact]
	public void Unhealthy_CreateWithException()
	{
		var ex = new InvalidOperationException("timeout");
		var result = HealthCheckResult.Unhealthy("Connection failed", ex);

		result.IsHealthy.ShouldBeFalse();
		result.Description.ShouldBe("Connection failed");
		result.Exception.ShouldBe(ex);
	}

	[Fact]
	public void AllowSettingProperties()
	{
		var result = new HealthCheckResult
		{
			IsHealthy = true,
			Description = "manual",
			CheckTimestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
		};

		result.IsHealthy.ShouldBeTrue();
		result.Description.ShouldBe("manual");
		result.CheckTimestamp.Year.ShouldBe(2026);
	}

	[Fact]
	public void HaveDefaultDescription()
	{
		var result = new HealthCheckResult();

		result.Description.ShouldBe(string.Empty);
		result.IsHealthy.ShouldBeFalse();
		result.Exception.ShouldBeNull();
	}
}
