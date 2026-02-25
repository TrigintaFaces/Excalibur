// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Projections;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class ProjectionRebuildStatusShould
{
	[Fact]
	public void ExposeAllProperties()
	{
		var ts = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
		var sut = new ProjectionRebuildStatus("OrderProjection", ProjectionRebuildState.Rebuilding, 45, ts);

		sut.ProjectionName.ShouldBe("OrderProjection");
		sut.State.ShouldBe(ProjectionRebuildState.Rebuilding);
		sut.Progress.ShouldBe(45);
		sut.LastRebuiltAt.ShouldBe(ts);
	}

	[Fact]
	public void AllowNullLastRebuiltAt()
	{
		var sut = new ProjectionRebuildStatus("MyProjection", ProjectionRebuildState.Idle, 0, null);
		sut.LastRebuiltAt.ShouldBeNull();
	}

	[Fact]
	public void SupportRecordEquality()
	{
		var ts = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var a = new ProjectionRebuildStatus("P1", ProjectionRebuildState.Completed, 100, ts);
		var b = new ProjectionRebuildStatus("P1", ProjectionRebuildState.Completed, 100, ts);
		a.ShouldBe(b);
	}

	[Fact]
	public void SupportRecordInequality()
	{
		var ts = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var a = new ProjectionRebuildStatus("P1", ProjectionRebuildState.Completed, 100, ts);
		var b = new ProjectionRebuildStatus("P2", ProjectionRebuildState.Failed, 50, ts);
		a.ShouldNotBe(b);
	}

	[Theory]
	[InlineData(ProjectionRebuildState.Idle)]
	[InlineData(ProjectionRebuildState.Rebuilding)]
	[InlineData(ProjectionRebuildState.Completed)]
	[InlineData(ProjectionRebuildState.Failed)]
	public void AcceptAllRebuildStates(ProjectionRebuildState state)
	{
		var sut = new ProjectionRebuildStatus("Test", state, 0, null);
		sut.State.ShouldBe(state);
	}

	[Fact]
	public void SupportProgressAtZero()
	{
		var sut = new ProjectionRebuildStatus("Test", ProjectionRebuildState.Idle, 0, null);
		sut.Progress.ShouldBe(0);
	}

	[Fact]
	public void SupportProgressAtHundred()
	{
		var sut = new ProjectionRebuildStatus("Test", ProjectionRebuildState.Completed, 100, null);
		sut.Progress.ShouldBe(100);
	}
}
