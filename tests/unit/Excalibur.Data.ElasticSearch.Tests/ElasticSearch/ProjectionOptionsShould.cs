// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch;
using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ProjectionOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new ProjectionOptions();

		sut.IndexPrefix.ShouldBe("projections");
		sut.ErrorHandling.ShouldNotBeNull();
		sut.RetryPolicy.ShouldNotBeNull();
		sut.ConsistencyTracking.ShouldNotBeNull();
		sut.SchemaEvolution.ShouldNotBeNull();
		sut.RebuildManager.ShouldNotBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var errorHandling = new ProjectionErrorHandlingOptions();
		var retryPolicy = new ProjectionRetryOptions();
		var consistency = new ConsistencyTrackingOptions();
		var schema = new SchemaEvolutionOptions();
		var rebuild = new RebuildManagerOptions();

		var sut = new ProjectionOptions
		{
			IndexPrefix = "custom-projections",
			ErrorHandling = errorHandling,
			RetryPolicy = retryPolicy,
			ConsistencyTracking = consistency,
			SchemaEvolution = schema,
			RebuildManager = rebuild,
		};

		sut.IndexPrefix.ShouldBe("custom-projections");
		sut.ErrorHandling.ShouldBeSameAs(errorHandling);
		sut.RetryPolicy.ShouldBeSameAs(retryPolicy);
		sut.ConsistencyTracking.ShouldBeSameAs(consistency);
		sut.SchemaEvolution.ShouldBeSameAs(schema);
		sut.RebuildManager.ShouldBeSameAs(rebuild);
	}
}
