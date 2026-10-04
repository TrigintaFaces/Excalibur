// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.ElasticSearch.MaterializedViews;
using Excalibur.EventSourcing;
using Excalibur.Integration.Tests.Infrastructure;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.MaterializedViews;

/// <summary>
/// Runs the view-position checkpoint contract against REAL Elasticsearch.
/// </summary>
/// <remarks>
/// The arms are in <see cref="ViewPositionCheckpointConformance"/>, shared with the OpenSearch suite, so
/// neither provider can quietly run a different set — which is exactly how the two defects they bind got
/// through.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "MaterializedViews")]
public sealed class ElasticSearchViewPositionShould(ElasticSearchViewPositionContainerFixture fixture)
	: ViewPositionCheckpointConformance, IClassFixture<ElasticSearchViewPositionContainerFixture>
{
	/// <inheritdoc/>
	protected override async Task<IMaterializedViewStore> NewStoreAsync()
	{
		fixture.DockerAvailable.ShouldBeTrue(
			"this suite measures the ENGINE's behaviour, so it is never skipped: a skip here is how both "
			+ "defects it binds reached consumers");

		// Cleared through the fixture rather than through the store, so a defect in ResetPositionAsync
		// cannot quietly become this suite's setup.
		await fixture.DropPositionsIndexAsync().ConfigureAwait(false);

		var options = Options.Create(new ElasticSearchMaterializedViewStoreOptions
		{
			ViewsIndexName = fixture.ViewsIndexName,
			PositionsIndexName = fixture.PositionsIndexName,
			RefreshPolicy = "true",
		});

		return new ElasticSearchMaterializedViewStore(
			fixture.Client,
			options,
			NullLogger<ElasticSearchMaterializedViewStore>.Instance,
			UntenantedTestTenantContext.Instance);
	}
}
