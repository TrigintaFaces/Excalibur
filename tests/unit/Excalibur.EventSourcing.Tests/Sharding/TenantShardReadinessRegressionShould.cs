// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.Sharding;
using Excalibur.EventSourcing.Sharding;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.EventSourcing.Tests.Sharding;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "Regression")]
public sealed class TenantShardReadinessRegressionShould
{
	private static readonly string[] ShardIds = ["physical-shard"];
	[Fact]
	public async Task RefuseHealthyWithoutAResolver()
	{
		var map = A.Fake<ITenantShardMap>();
		A.CallTo(() => map.GetRegisteredShardIds()).Returns(ShardIds);
		var result = await new TenantShardHealthCheck(map).CheckHealthAsync(new(), TestContext.Current.CancellationToken);
		result.Status.ShouldBe(HealthStatus.Unhealthy);
	}

	[Fact]
	public async Task RefuseHealthyWhenAResolverCannotProveReadiness()
	{
		var map = A.Fake<ITenantShardMap>();
		A.CallTo(() => map.GetRegisteredShardIds()).Returns(ShardIds);
		var resolver = A.Fake<ITenantStoreResolver<IEventStore>>();
		A.CallTo(() => resolver.Resolve(A<string>._)).Returns(A.Fake<IEventStore>());
		var result = await new TenantShardHealthCheck(map, resolver).CheckHealthAsync(new(), TestContext.Current.CancellationToken);
		result.Status.ShouldBe(HealthStatus.Unhealthy);
	}
}
