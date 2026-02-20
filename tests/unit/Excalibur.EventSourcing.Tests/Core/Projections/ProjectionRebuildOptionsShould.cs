// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Projections;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class ProjectionRebuildOptionsShould
{
	[Fact]
	public void DefaultBatchSizeTo500()
	{
		var sut = new ProjectionRebuildOptions();
		sut.BatchSize.ShouldBe(500);
	}

	[Fact]
	public void DefaultBatchDelayTo10Ms()
	{
		var sut = new ProjectionRebuildOptions();
		sut.BatchDelay.ShouldBe(TimeSpan.FromMilliseconds(10));
	}

	[Fact]
	public void DefaultRebuildOnStartupToFalse()
	{
		var sut = new ProjectionRebuildOptions();
		sut.RebuildOnStartup.ShouldBeFalse();
	}

	[Fact]
	public void DefaultMaxParallelismTo1()
	{
		var sut = new ProjectionRebuildOptions();
		sut.MaxParallelism.ShouldBe(1);
	}

	[Fact]
	public void AllowSettingBatchSize()
	{
		var sut = new ProjectionRebuildOptions { BatchSize = 1000 };
		sut.BatchSize.ShouldBe(1000);
	}

	[Fact]
	public void AllowSettingBatchDelay()
	{
		var sut = new ProjectionRebuildOptions { BatchDelay = TimeSpan.FromSeconds(1) };
		sut.BatchDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void AllowSettingRebuildOnStartup()
	{
		var sut = new ProjectionRebuildOptions { RebuildOnStartup = true };
		sut.RebuildOnStartup.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingMaxParallelism()
	{
		var sut = new ProjectionRebuildOptions { MaxParallelism = 8 };
		sut.MaxParallelism.ShouldBe(8);
	}
}
