// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class SchemaMigrationDryRunResultShould
{
	[Fact]
	public void SetRequiredProperties()
	{
		var sut = new SchemaMigrationDryRunResult
		{
			Success = true,
			DocumentsTested = 100,
			DocumentsSuccessful = 98,
			DocumentsFailed = 2,
		};

		sut.Success.ShouldBeTrue();
		sut.DocumentsTested.ShouldBe(100);
		sut.DocumentsSuccessful.ShouldBe(98);
		sut.DocumentsFailed.ShouldBe(2);
	}

	[Fact]
	public void HaveNullDefaultsForOptionalProperties()
	{
		var sut = new SchemaMigrationDryRunResult
		{
			Success = true,
			DocumentsTested = 50,
			DocumentsSuccessful = 50,
			DocumentsFailed = 0,
		};

		sut.SampleFailures.ShouldBeNull();
		sut.PerformanceMetrics.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingOptionalProperties()
	{
		var failures = new List<DocumentMigrationFailure>
		{
			new() { DocumentId = "doc-1", Reason = "Type mismatch" },
		};
		var metrics = new DryRunPerformanceMetrics
		{
			AverageProcessingTimeMs = 1.5,
			EstimatedTotalTime = TimeSpan.FromMinutes(30),
			DocumentsPerSecond = 666.7,
		};

		var sut = new SchemaMigrationDryRunResult
		{
			Success = false,
			DocumentsTested = 100,
			DocumentsSuccessful = 95,
			DocumentsFailed = 5,
			SampleFailures = failures,
			PerformanceMetrics = metrics,
		};

		sut.SampleFailures.ShouldBeSameAs(failures);
		sut.PerformanceMetrics.ShouldBeSameAs(metrics);
	}
}
