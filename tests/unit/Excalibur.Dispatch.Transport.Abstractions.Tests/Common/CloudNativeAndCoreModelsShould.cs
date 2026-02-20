using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Common;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class CloudNativeAndCoreModelsShould
{
	[Fact]
	public void CloudNativeOptions_HaveExpectedDefaults_AndSupportOverrides()
	{
		var options = new CloudNativeOptions();

		options.Enabled.ShouldBeTrue();
		options.UseCloudLogging.ShouldBeTrue();
		options.UseCloudMetrics.ShouldBeTrue();
		options.UseCloudTracing.ShouldBeTrue();
		options.Environment.ShouldBe("production");
		options.MetricsFlushInterval.ShouldBe(TimeSpan.FromSeconds(60));
		options.Tags.ShouldNotBeNull();

		options.Provider = "aws";
		options.Region = "us-east-1";
		options.Tags["Project"] = "Dispatch";

		options.Provider.ShouldBe("aws");
		options.Region.ShouldBe("us-east-1");
		options.Tags["Project"].ShouldBe("Dispatch");
	}

	[Fact]
	public void AccessRule_StoresPrincipalAndPermissions()
	{
		var rule = new AccessRule
		{
			Principal = "svc-orders",
			Permissions = AccessPermissions.Receive | AccessPermissions.Send
		};

		rule.Principal.ShouldBe("svc-orders");
		rule.Permissions.ShouldBe(AccessPermissions.Receive | AccessPermissions.Send);
	}

	[Fact]
	public void TransportPollingStatistics_StoresAggregates()
	{
		var stats = new TransportPollingStatistics
		{
			TotalPolls = 15,
			TotalMessages = 120,
			TotalErrors = 2,
			TotalDuration = TimeSpan.FromSeconds(30)
		};

		stats.TotalPolls.ShouldBe(15);
		stats.TotalMessages.ShouldBe(120);
		stats.TotalErrors.ShouldBe(2);
		stats.TotalDuration.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void CloudWatchMetricsCollector_Dispose_IsSafeAndIdempotent()
	{
		var collector = new CloudWatchMetricsCollector();

		collector.Dispose();
		collector.Dispose();
	}
}
