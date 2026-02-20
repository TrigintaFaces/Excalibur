// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ProjectionRebuildResultShould
{
	[Fact]
	public void SetRequiredProperties()
	{
		var startedAt = DateTimeOffset.UtcNow;
		var sut = new ProjectionRebuildResult
		{
			OperationId = "op-123",
			Started = true,
			StartedAt = startedAt,
		};

		sut.OperationId.ShouldBe("op-123");
		sut.Started.ShouldBeTrue();
		sut.StartedAt.ShouldBe(startedAt);
	}

	[Fact]
	public void HaveNullDefaultsForOptionalProperties()
	{
		var sut = new ProjectionRebuildResult
		{
			OperationId = "op-1",
			Started = false,
			StartedAt = DateTimeOffset.UtcNow,
		};

		sut.Message.ShouldBeNull();
		sut.EstimatedCompletionTime.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingOptionalProperties()
	{
		var est = DateTimeOffset.UtcNow.AddHours(1);
		var sut = new ProjectionRebuildResult
		{
			OperationId = "op-456",
			Started = true,
			StartedAt = DateTimeOffset.UtcNow,
			Message = "Rebuild initiated successfully",
			EstimatedCompletionTime = est,
		};

		sut.Message.ShouldBe("Rebuild initiated successfully");
		sut.EstimatedCompletionTime.ShouldBe(est);
	}
}
