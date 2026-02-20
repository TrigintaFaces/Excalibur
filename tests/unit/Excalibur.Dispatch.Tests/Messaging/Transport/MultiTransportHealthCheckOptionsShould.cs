using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MultiTransportHealthCheckOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var options = new MultiTransportHealthCheckOptions();

		options.RequireAtLeastOneTransport.ShouldBeFalse();
		options.RequireDefaultTransportHealthy.ShouldBeTrue();
		options.TransportCheckTimeout.ShouldBe(TimeSpan.FromSeconds(5));
		options.ParallelChecks.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var options = new MultiTransportHealthCheckOptions
		{
			RequireAtLeastOneTransport = true,
			RequireDefaultTransportHealthy = false,
			TransportCheckTimeout = TimeSpan.FromSeconds(30),
			ParallelChecks = false,
		};

		options.RequireAtLeastOneTransport.ShouldBeTrue();
		options.RequireDefaultTransportHealthy.ShouldBeFalse();
		options.TransportCheckTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.ParallelChecks.ShouldBeFalse();
	}
}
