// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class DryRunPerformanceMetricsShould
{
	[Fact]
	public void SetRequiredProperties()
	{
		var estimatedTime = TimeSpan.FromMinutes(45);
		var sut = new DryRunPerformanceMetrics
		{
			AverageProcessingTimeMs = 1.5,
			EstimatedTotalTime = estimatedTime,
			DocumentsPerSecond = 666.7,
		};

		sut.AverageProcessingTimeMs.ShouldBe(1.5);
		sut.EstimatedTotalTime.ShouldBe(estimatedTime);
		sut.DocumentsPerSecond.ShouldBe(666.7);
	}
}
