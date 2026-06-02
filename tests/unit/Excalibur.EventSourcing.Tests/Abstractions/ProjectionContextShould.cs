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
	// --- Constructor ---

	[Fact]
	public void StoreIsReplayFromConstructor()
	{
		// Arrange & Act
		var context = new ProjectionContext(isReplay: true, globalPosition: 42L);

		// Assert
		context.IsReplay.ShouldBeTrue();
	}

	[Fact]
	public void StoreGlobalPositionFromConstructor()
	{
		// Arrange & Act
		var context = new ProjectionContext(isReplay: false, globalPosition: 100L);

		// Assert
		context.GlobalPosition.ShouldBe(100L);
	}

	[Fact]
	public void AllowNullGlobalPosition()
	{
		// Arrange & Act
		var context = new ProjectionContext(isReplay: false, globalPosition: null);

		// Assert
		context.GlobalPosition.ShouldBeNull();
	}

	// --- Live singleton ---

	[Fact]
	public void ProvideLiveSingletonWithIsReplayFalse()
	{
		// Act
		var live = ProjectionContext.Live;

		// Assert
		live.IsReplay.ShouldBeFalse();
	}

	[Fact]
	public void ProvideLiveSingletonWithNullGlobalPosition()
	{
		// Act
		var live = ProjectionContext.Live;

		// Assert
		live.GlobalPosition.ShouldBeNull();
	}

	[Fact]
	public void ReturnSameLiveSingletonInstance()
	{
		// Act
		var first = ProjectionContext.Live;
		var second = ProjectionContext.Live;

		// Assert
		ReferenceEquals(first, second).ShouldBeTrue();
	}

	// --- Replay factory ---

	[Fact]
	public void CreateReplayContextWithIsReplayTrue()
	{
		// Act
		var replay = ProjectionContext.Replay(500L);

		// Assert
		replay.IsReplay.ShouldBeTrue();
	}

	[Fact]
	public void CreateReplayContextWithSpecifiedGlobalPosition()
	{
		// Act
		var replay = ProjectionContext.Replay(12345L);

		// Assert
		replay.GlobalPosition.ShouldBe(12345L);
	}

	[Fact]
	public void CreateDistinctInstancesForEachReplayCall()
	{
		// Act
		var first = ProjectionContext.Replay(1L);
		var second = ProjectionContext.Replay(2L);

		// Assert
		ReferenceEquals(first, second).ShouldBeFalse();
		first.GlobalPosition.ShouldBe(1L);
		second.GlobalPosition.ShouldBe(2L);
	}

	[Fact]
	public void AcceptZeroGlobalPositionInReplay()
	{
		// Act
		var replay = ProjectionContext.Replay(0L);

		// Assert
		replay.IsReplay.ShouldBeTrue();
		replay.GlobalPosition.ShouldBe(0L);
	}

	[Fact]
	public void RejectNegativeGlobalPositionInReplay()
	{
		// Regression test for bd-a1zvnv: Replay must guard against negative positions
		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() => ProjectionContext.Replay(-1L));
	}

	[Theory]
	[InlineData(-1L)]
	[InlineData(-100L)]
	[InlineData(long.MinValue)]
	public void RejectAllNegativeGlobalPositionsInReplay(long negativePosition)
	{
		// Regression test for bd-a1zvnv: boundary coverage for negative positions
		// Act & Assert
		var ex = Should.Throw<ArgumentOutOfRangeException>(() => ProjectionContext.Replay(negativePosition));
		ex.ParamName.ShouldBe("globalPosition");
	}
}
