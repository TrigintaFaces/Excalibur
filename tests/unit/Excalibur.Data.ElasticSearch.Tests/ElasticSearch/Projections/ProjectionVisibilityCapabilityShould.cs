// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.ElasticSearch.Projections;
using Excalibur.EventSourcing;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.ElasticSearch.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Projections")]
public sealed class ProjectionVisibilityCapabilityShould
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task AdvertiseSearchVisibilityOnlyWhenConfigured(bool enabled)
	{
		var options = new ElasticSearchProjectionStoreOptions
		{
			NodeUri = "http://localhost:9200",
			Index = { WaitForSearchVisibilityOnWrite = enabled },
		};
		var monitor = A.Fake<IOptionsMonitor<ElasticSearchProjectionStoreOptions>>();
		A.CallTo(() => monitor.Get(A<string>._)).Returns(options);
		await using var store = new ElasticSearchProjectionStore<Projection>(new ElasticsearchClient(), monitor,
			NullLogger<ElasticSearchProjectionStore<Projection>>.Instance);
		var services = (IServiceProvider)store;
		(services.GetService(typeof(IProjectionVisibilityBarrier)) is IProjectionVisibilityBarrier).ShouldBe(enabled);
		services.GetService(typeof(IPositionedProjectionStore<Projection>)).ShouldBeSameAs(store);
		services.GetService(typeof(string)).ShouldBeNull();
	}

	private sealed class Projection { public string Id { get; set; } = string.Empty; }
}
