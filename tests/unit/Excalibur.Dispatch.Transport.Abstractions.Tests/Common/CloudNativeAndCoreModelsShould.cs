using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Common;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class CloudNativeAndCoreModelsShould
{
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

}
