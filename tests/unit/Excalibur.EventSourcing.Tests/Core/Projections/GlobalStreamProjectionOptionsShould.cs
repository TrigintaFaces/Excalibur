// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Projections;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class GlobalStreamProjectionOptionsShould
{
	[Fact]
	public void DefaultCheckpointIntervalTo100()
	{
		var sut = new GlobalStreamProjectionOptions();
		sut.CheckpointInterval.ShouldBe(100);
	}

	[Fact]
	public void DefaultProjectionNameToGlobalStreamProjection()
	{
		var sut = new GlobalStreamProjectionOptions();
		sut.ProjectionName.ShouldBe("GlobalStreamProjection");
	}

	[Fact]
	public void DefaultBatchSizeTo500()
	{
		var sut = new GlobalStreamProjectionOptions();
		sut.BatchSize.ShouldBe(500);
	}

	[Fact]
	public void DefaultIdlePollingIntervalTo1Second()
	{
		var sut = new GlobalStreamProjectionOptions();
		sut.IdlePollingInterval.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void AllowSettingCheckpointInterval()
	{
		var sut = new GlobalStreamProjectionOptions { CheckpointInterval = 50 };
		sut.CheckpointInterval.ShouldBe(50);
	}

	[Fact]
	public void AllowSettingProjectionName()
	{
		var sut = new GlobalStreamProjectionOptions { ProjectionName = "CustomProjection" };
		sut.ProjectionName.ShouldBe("CustomProjection");
	}

	[Fact]
	public void AllowSettingBatchSize()
	{
		var sut = new GlobalStreamProjectionOptions { BatchSize = 2000 };
		sut.BatchSize.ShouldBe(2000);
	}

	[Fact]
	public void AllowSettingIdlePollingInterval()
	{
		var sut = new GlobalStreamProjectionOptions { IdlePollingInterval = TimeSpan.FromSeconds(5) };
		sut.IdlePollingInterval.ShouldBe(TimeSpan.FromSeconds(5));
	}
}
