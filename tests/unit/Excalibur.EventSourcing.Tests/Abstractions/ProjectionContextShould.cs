// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Tests.Abstractions;

/// <summary>
/// Tests for <see cref="ProjectionContext"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProjectionContextShould
{
	// --- AggregateId ---

	[Fact]
	public void StoreAggregateIdFromConstructor()
	{
		// Arrange & Act
		var context = new ProjectionContext(isReplay: false, globalPosition: null, aggregateId: "customer-42");

		// Assert
		context.AggregateId.ShouldBe("customer-42");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void RejectAnAbsentAggregateId(string? absent)
	{
		// The identity is required, not defaulted. A projection whose identity silently became "" is
		// indistinguishable from one that was never given an identity, so the type refuses to represent
		// that state rather than leaving every handler to guard it.
		_ = Should.Throw<ArgumentException>(() =>
			new ProjectionContext(isReplay: false, globalPosition: null, aggregateId: absent!));
	}

	[Fact]
	public void PreserveAggregateIdAlongsideReplayAndPosition()
	{
		// Arrange & Act
		var context = new ProjectionContext(isReplay: true, globalPosition: 7L, aggregateId: "order-1");

		// Assert
		context.IsReplay.ShouldBeTrue();
		context.GlobalPosition.ShouldBe(7L);
		context.AggregateId.ShouldBe("order-1");
	}

	// --- Constructor ---

	[Fact]
	public void StoreIsReplayFromConstructor()
	{
		// Arrange & Act
		var context = new ProjectionContext(isReplay: true, globalPosition: 42L, aggregateId: "a-1");

		// Assert
		context.IsReplay.ShouldBeTrue();
	}

	[Fact]
	public void StoreGlobalPositionFromConstructor()
	{
		// Arrange & Act
		var context = new ProjectionContext(isReplay: false, globalPosition: 100L, aggregateId: "a-1");

		// Assert
		context.GlobalPosition.ShouldBe(100L);
	}

	[Fact]
	public void AllowNullGlobalPosition()
	{
		// Arrange & Act
		var context = new ProjectionContext(isReplay: false, globalPosition: null, aggregateId: "a-1");

		// Assert
		context.GlobalPosition.ShouldBeNull();
	}

	// --- Live construction ---

	[Fact]
	public void ReportNotReplayingForALiveContext()
	{
		// Arrange & Act
		var context = new ProjectionContext(isReplay: false, globalPosition: null, aggregateId: "a-1");

		// Assert
		context.IsReplay.ShouldBeFalse();
		context.GlobalPosition.ShouldBeNull();
		context.AggregateId.ShouldBe("a-1");
	}

	// --- Replay factory ---

	[Fact]
	public void CreateReplayContextWithIsReplayTrue()
	{
		// Act
		var replay = ProjectionContext.Replay(500L, "a-1");

		// Assert
		replay.IsReplay.ShouldBeTrue();
	}

	[Fact]
	public void CreateReplayContextWithSpecifiedGlobalPosition()
	{
		// Act
		var replay = ProjectionContext.Replay(12345L, "a-1");

		// Assert
		replay.GlobalPosition.ShouldBe(12345L);
	}

	[Fact]
	public void CreateDistinctInstancesForEachReplayCall()
	{
		// Act
		var first = ProjectionContext.Replay(1L, "a-1");
		var second = ProjectionContext.Replay(2L, "a-1");

		// Assert
		ReferenceEquals(first, second).ShouldBeFalse();
		first.GlobalPosition.ShouldBe(1L);
		second.GlobalPosition.ShouldBe(2L);
	}

	[Fact]
	public void AcceptZeroGlobalPositionInReplay()
	{
		// Act
		var replay = ProjectionContext.Replay(0L, "a-1");

		// Assert
		replay.IsReplay.ShouldBeTrue();
		replay.GlobalPosition.ShouldBe(0L);
	}

	[Fact]
	public void RejectNegativeGlobalPositionInReplay()
	{
		// Regression test for bd-a1zvnv: Replay must guard against negative positions
		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() => ProjectionContext.Replay(-1L, "a-1"));
	}

	[Theory]
	[InlineData(-1L)]
	[InlineData(-100L)]
	[InlineData(long.MinValue)]
	public void RejectAllNegativeGlobalPositionsInReplay(long negativePosition)
	{
		// Regression test for bd-a1zvnv: boundary coverage for negative positions
		// Act & Assert
		var ex = Should.Throw<ArgumentOutOfRangeException>(() => ProjectionContext.Replay(negativePosition, "a-1"));
		ex.ParamName.ShouldBe("globalPosition");
	}
}
