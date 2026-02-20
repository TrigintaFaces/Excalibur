// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ProjectionRebuildStatusShould
{
	[Fact]
	public void SetRequiredProperties()
	{
		var startedAt = DateTimeOffset.UtcNow;
		var sut = new ProjectionRebuildStatus
		{
			OperationId = "op-1",
			State = RebuildState.InProgress,
			ProjectionType = "OrderProjection",
			StartedAt = startedAt,
		};

		sut.OperationId.ShouldBe("op-1");
		sut.State.ShouldBe(RebuildState.InProgress);
		sut.ProjectionType.ShouldBe("OrderProjection");
		sut.StartedAt.ShouldBe(startedAt);
	}

	[Fact]
	public void HaveDefaultsForOptionalProperties()
	{
		var sut = new ProjectionRebuildStatus
		{
			OperationId = "op-1",
			State = RebuildState.Queued,
			ProjectionType = "Test",
			StartedAt = DateTimeOffset.UtcNow,
		};

		sut.TotalDocuments.ShouldBe(0);
		sut.ProcessedDocuments.ShouldBe(0);
		sut.FailedDocuments.ShouldBe(0);
		sut.PercentComplete.ShouldBe(0);
		sut.CompletedAt.ShouldBeNull();
		sut.DocumentsPerSecond.ShouldBe(0);
		sut.EstimatedTimeRemaining.ShouldBeNull();
		sut.LastError.ShouldBeNull();
		sut.Checkpoint.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var startedAt = DateTimeOffset.UtcNow;
		var completedAt = startedAt.AddMinutes(10);

		var sut = new ProjectionRebuildStatus
		{
			OperationId = "op-2",
			State = RebuildState.Completed,
			ProjectionType = "OrderProjection",
			StartedAt = startedAt,
			TotalDocuments = 100000,
			ProcessedDocuments = 100000,
			FailedDocuments = 5,
			PercentComplete = 100.0,
			CompletedAt = completedAt,
			DocumentsPerSecond = 166.7,
			EstimatedTimeRemaining = TimeSpan.Zero,
			LastError = "Mapping conflict on field 'status'",
			Checkpoint = "seq-100000",
		};

		sut.TotalDocuments.ShouldBe(100000);
		sut.ProcessedDocuments.ShouldBe(100000);
		sut.FailedDocuments.ShouldBe(5);
		sut.PercentComplete.ShouldBe(100.0);
		sut.CompletedAt.ShouldBe(completedAt);
		sut.DocumentsPerSecond.ShouldBe(166.7);
		sut.EstimatedTimeRemaining.ShouldBe(TimeSpan.Zero);
		sut.LastError.ShouldBe("Mapping conflict on field 'status'");
		sut.Checkpoint.ShouldBe("seq-100000");
	}
}
