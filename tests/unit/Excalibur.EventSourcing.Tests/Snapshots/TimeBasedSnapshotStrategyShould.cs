// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Snapshots;

using FakeItEasy;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Snapshots;

/// <summary>
/// Unit tests for <see cref="TimeBasedSnapshotStrategy"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TimeBasedSnapshotStrategyShould
{
	[Fact]
	public void Constructor_ThrowArgumentOutOfRangeException_WhenIntervalIsZero()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			new TimeBasedSnapshotStrategy(TimeSpan.Zero));
	}

	[Fact]
	public void Constructor_ThrowArgumentOutOfRangeException_WhenIntervalIsNegative()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			new TimeBasedSnapshotStrategy(TimeSpan.FromMinutes(-5)));
	}

	[Fact]
	public void ShouldCreateSnapshot_ReturnTrue_OnFirstCall()
	{
		// Arrange
		var strategy = new TimeBasedSnapshotStrategy(TimeSpan.FromMinutes(5));
		var aggregate = A.Fake<IAggregateRoot>();
		_ = A.CallTo(() => aggregate.Id).Returns("test-aggregate-1");

		// Act
		var result = strategy.ShouldCreateSnapshot(aggregate);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ShouldCreateSnapshot_ReturnFalse_WhenCalledImmediatelyAfterFirst()
	{
		// Arrange
		var strategy = new TimeBasedSnapshotStrategy(TimeSpan.FromMinutes(5));
		var aggregate = A.Fake<IAggregateRoot>();
		_ = A.CallTo(() => aggregate.Id).Returns("test-aggregate-2");

		// First call - registers the aggregate
		_ = strategy.ShouldCreateSnapshot(aggregate);

		// Act - second call immediately after
		var result = strategy.ShouldCreateSnapshot(aggregate);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ShouldCreateSnapshot_ReturnTrue_ForDifferentAggregates()
	{
		// Arrange
		var strategy = new TimeBasedSnapshotStrategy(TimeSpan.FromMinutes(5));

		var aggregate1 = A.Fake<IAggregateRoot>();
		_ = A.CallTo(() => aggregate1.Id).Returns("aggregate-1");

		var aggregate2 = A.Fake<IAggregateRoot>();
		_ = A.CallTo(() => aggregate2.Id).Returns("aggregate-2");

		// Act
		var result1 = strategy.ShouldCreateSnapshot(aggregate1);
		var result2 = strategy.ShouldCreateSnapshot(aggregate2);

		// Assert
		result1.ShouldBeTrue();
		result2.ShouldBeTrue();
	}

	[Fact]
	public void ClearTrackedTimes_ShouldResetAllTrackedAggregates()
	{
		// Arrange
		var strategy = new TimeBasedSnapshotStrategy(TimeSpan.FromMinutes(5));
		var aggregate = A.Fake<IAggregateRoot>();
		_ = A.CallTo(() => aggregate.Id).Returns("test-aggregate-3");

		// First call registers the aggregate
		_ = strategy.ShouldCreateSnapshot(aggregate);

		// Act
		strategy.ClearTrackedTimes();
		var result = strategy.ShouldCreateSnapshot(aggregate);

		// Assert - should return true because tracking was cleared
		result.ShouldBeTrue();
	}

	[Fact]
	public void GetLastSnapshotTime_ReturnNull_WhenAggregateNotTracked()
	{
		// Arrange
		var strategy = new TimeBasedSnapshotStrategy(TimeSpan.FromMinutes(5));

		// Act
		var result = strategy.GetLastSnapshotTime("untracked-aggregate");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetLastSnapshotTime_ReturnTime_WhenAggregateIsTracked()
	{
		// Arrange
		var strategy = new TimeBasedSnapshotStrategy(TimeSpan.FromMinutes(5));
		var aggregate = A.Fake<IAggregateRoot>();
		_ = A.CallTo(() => aggregate.Id).Returns("tracked-aggregate");
		var beforeRegistration = DateTime.UtcNow;

		// Register the aggregate
		_ = strategy.ShouldCreateSnapshot(aggregate);
		var afterRegistration = DateTime.UtcNow;

		// Act
		var result = strategy.GetLastSnapshotTime("tracked-aggregate");

		// Assert
		_ = result.ShouldNotBeNull();
		result.Value.ShouldBeInRange(beforeRegistration, afterRegistration);
	}

	[Fact]
	public void ShouldCreateSnapshot_ThrowArgumentNullException_WhenAggregateIsNull()
	{
		// Arrange
		var strategy = new TimeBasedSnapshotStrategy(TimeSpan.FromMinutes(5));

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => strategy.ShouldCreateSnapshot(null!));
	}
}
