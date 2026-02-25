// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Snapshots;

using FakeItEasy;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Snapshots;

/// <summary>
/// Unit tests for <see cref="IntervalSnapshotStrategy"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class IntervalSnapshotStrategyShould
{
	[Fact]
	public void ShouldCreateSnapshot_ReturnTrue_AtInterval()
	{
		// Arrange
		var strategy = new IntervalSnapshotStrategy(interval: 10);
		var aggregate = A.Fake<IAggregateRoot>();
		_ = A.CallTo(() => aggregate.Version).Returns(10);

		// Act
		var result = strategy.ShouldCreateSnapshot(aggregate);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ShouldCreateSnapshot_ReturnTrue_AtMultiplesOfInterval()
	{
		// Arrange
		var strategy = new IntervalSnapshotStrategy(interval: 10);
		var aggregate = A.Fake<IAggregateRoot>();
		_ = A.CallTo(() => aggregate.Version).Returns(100);

		// Act
		var result = strategy.ShouldCreateSnapshot(aggregate);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ShouldCreateSnapshot_ReturnFalse_BetweenIntervals()
	{
		// Arrange
		var strategy = new IntervalSnapshotStrategy(interval: 10);
		var aggregate = A.Fake<IAggregateRoot>();
		_ = A.CallTo(() => aggregate.Version).Returns(15);

		// Act
		var result = strategy.ShouldCreateSnapshot(aggregate);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ShouldCreateSnapshot_ReturnFalse_WhenVersionIsZero()
	{
		// Arrange
		var strategy = new IntervalSnapshotStrategy(interval: 10);
		var aggregate = A.Fake<IAggregateRoot>();
		_ = A.CallTo(() => aggregate.Version).Returns(0);

		// Act
		var result = strategy.ShouldCreateSnapshot(aggregate);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ShouldCreateSnapshot_UseDefaultInterval_WhenNotSpecified()
	{
		// Arrange
		var strategy = new IntervalSnapshotStrategy();
		var aggregate = A.Fake<IAggregateRoot>();
		_ = A.CallTo(() => aggregate.Version).Returns(100); // Default interval is 100

		// Act
		var result = strategy.ShouldCreateSnapshot(aggregate);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ShouldCreateSnapshot_ReturnFalse_JustBeforeInterval()
	{
		// Arrange
		var strategy = new IntervalSnapshotStrategy(interval: 10);
		var aggregate = A.Fake<IAggregateRoot>();
		_ = A.CallTo(() => aggregate.Version).Returns(9);

		// Act
		var result = strategy.ShouldCreateSnapshot(aggregate);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ShouldCreateSnapshot_ReturnFalse_JustAfterInterval()
	{
		// Arrange
		var strategy = new IntervalSnapshotStrategy(interval: 10);
		var aggregate = A.Fake<IAggregateRoot>();
		_ = A.CallTo(() => aggregate.Version).Returns(11);

		// Act
		var result = strategy.ShouldCreateSnapshot(aggregate);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ShouldCreateSnapshot_ThrowArgumentNullException_WhenAggregateIsNull()
	{
		// Arrange
		var strategy = new IntervalSnapshotStrategy(interval: 10);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => strategy.ShouldCreateSnapshot(null!));
	}
}
