// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ProjectionRebuildSummaryShould
{
	[Fact]
	public void SetRequiredProperties()
	{
		var startedAt = DateTimeOffset.UtcNow;
		var sut = new ProjectionRebuildSummary
		{
			OperationId = "op-1",
			ProjectionType = "OrderProjection",
			State = RebuildState.InProgress,
			StartedAt = startedAt,
		};

		sut.OperationId.ShouldBe("op-1");
		sut.ProjectionType.ShouldBe("OrderProjection");
		sut.State.ShouldBe(RebuildState.InProgress);
		sut.StartedAt.ShouldBe(startedAt);
	}

	[Fact]
	public void HaveDefaultsForOptionalProperties()
	{
		var sut = new ProjectionRebuildSummary
		{
			OperationId = "op-1",
			ProjectionType = "Test",
			State = RebuildState.Queued,
			StartedAt = DateTimeOffset.UtcNow,
		};

		sut.CompletedAt.ShouldBeNull();
		sut.ProcessedDocuments.ShouldBe(0);
		sut.FailedDocuments.ShouldBe(0);
		sut.Duration.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var start = DateTimeOffset.UtcNow;
		var completed = start.AddMinutes(30);
		var duration = TimeSpan.FromMinutes(30);

		var sut = new ProjectionRebuildSummary
		{
			OperationId = "op-2",
			ProjectionType = "OrderProjection",
			State = RebuildState.Completed,
			StartedAt = start,
			CompletedAt = completed,
			ProcessedDocuments = 50000,
			FailedDocuments = 3,
			Duration = duration,
		};

		sut.CompletedAt.ShouldBe(completed);
		sut.ProcessedDocuments.ShouldBe(50000);
		sut.FailedDocuments.ShouldBe(3);
		sut.Duration.ShouldBe(duration);
	}
}
