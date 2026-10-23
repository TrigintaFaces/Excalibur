// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.ElasticSearch.Projections;
using Excalibur.EventSourcing;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.ElasticSearch.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Projections")]
public sealed class CheckpointVisibilityOptionsShould
{
	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task AdvertiseTheBarrierForEitherExplicitVisibilityGuarantee(bool perWrite, bool perCheckpoint)
	{
		var options = new ElasticSearchProjectionStoreOptions
		{
			NodeUri = "http://localhost:9200",
			Index =
			{
				WaitForSearchVisibilityOnWrite = perWrite,
				EnsureSearchVisibilityOnProjectionCheckpoint = perCheckpoint,
			},
		};
		var monitor = A.Fake<IOptionsMonitor<ElasticSearchProjectionStoreOptions>>();
		A.CallTo(() => monitor.Get(A<string>._)).Returns(options);
		await using var store = new ElasticSearchProjectionStore<Projection>(new ElasticsearchClient(), monitor,
			NullLogger<ElasticSearchProjectionStore<Projection>>.Instance);
		(((IServiceProvider)store).GetService(typeof(IProjectionVisibilityBarrier)) is IProjectionVisibilityBarrier)
			.ShouldBe(perWrite || perCheckpoint);
	}

	[Fact]
	public void KeepBothVisibilityCostsOptIn()
	{
		var options = new ElasticSearchProjectionIndexOptions();
		options.WaitForSearchVisibilityOnWrite.ShouldBeFalse();
		options.EnsureSearchVisibilityOnProjectionCheckpoint.ShouldBeFalse();
	}

	[Fact]
	public void AllowCheckpointVisibilityWithAutomaticRefreshDisabled()
	{
		var options = new ElasticSearchProjectionStoreOptions
		{
			NodeUri = "http://localhost:9200",
			Index = { EnsureSearchVisibilityOnProjectionCheckpoint = true, RefreshInterval = "-1" },
		};
		Should.NotThrow(options.Validate);
	}

	private sealed class Projection { public string Id { get; set; } = string.Empty; }
}
