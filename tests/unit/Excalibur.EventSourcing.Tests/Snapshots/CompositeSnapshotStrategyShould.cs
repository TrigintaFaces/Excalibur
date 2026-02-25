// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Snapshots;

using FakeItEasy;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Snapshots;

/// <summary>
/// Unit tests for <see cref="CompositeSnapshotStrategy"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CompositeSnapshotStrategyShould
{
	#region Constructor Tests

	[Fact]
	public void ThrowArgumentException_WhenNoStrategiesProvided()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new CompositeSnapshotStrategy(CompositeSnapshotStrategy.CompositeMode.Any));
	}

	[Fact]
	public void ThrowArgumentException_WhenNullStrategiesArray()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new CompositeSnapshotStrategy(CompositeSnapshotStrategy.CompositeMode.Any, null!));
	}

	[Fact]
	public void ThrowArgumentException_WhenEmptyStrategiesArray()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new CompositeSnapshotStrategy(CompositeSnapshotStrategy.CompositeMode.Any, Array.Empty<ISnapshotStrategy>()));
	}

	[Fact]
	public void CreateSuccessfully_WhenAtLeastOneStrategyProvided()
	{
		// Arrange
		var strategy = A.Fake<ISnapshotStrategy>();

		// Act
		var composite = new CompositeSnapshotStrategy(CompositeSnapshotStrategy.CompositeMode.Any, strategy);

		// Assert
		composite.StrategyCount.ShouldBe(1);
	}

	#endregion Constructor Tests

	#region ShouldCreateSnapshot - Any Mode Tests

	[Fact]
	public void ShouldCreateSnapshot_AnyMode_ReturnTrue_WhenAnyStrategyReturnsTrue()
	{
		// Arrange
		var aggregate = A.Fake<IAggregateRoot>();
		_ = A.CallTo(() => aggregate.Id).Returns("test-id");
		_ = A.CallTo(() => aggregate.Version).Returns(10);

		var falseStrategy = A.Fake<ISnapshotStrategy>();
		_ = A.CallTo(() => falseStrategy.ShouldCreateSnapshot(A<IAggregateRoot>._)).Returns(false);

		var trueStrategy = A.Fake<ISnapshotStrategy>();
		_ = A.CallTo(() => trueStrategy.ShouldCreateSnapshot(A<IAggregateRoot>._)).Returns(true);

		var composite = new CompositeSnapshotStrategy(
			CompositeSnapshotStrategy.CompositeMode.Any,
			falseStrategy,
			trueStrategy);

		// Act
		var result = composite.ShouldCreateSnapshot(aggregate);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ShouldCreateSnapshot_AnyMode_ReturnFalse_WhenAllStrategiesReturnFalse()
	{
		// Arrange
		var aggregate = A.Fake<IAggregateRoot>();
		_ = A.CallTo(() => aggregate.Id).Returns("test-id");
		_ = A.CallTo(() => aggregate.Version).Returns(10);

		var falseStrategy1 = A.Fake<ISnapshotStrategy>();
		_ = A.CallTo(() => falseStrategy1.ShouldCreateSnapshot(A<IAggregateRoot>._)).Returns(false);

		var falseStrategy2 = A.Fake<ISnapshotStrategy>();
		_ = A.CallTo(() => falseStrategy2.ShouldCreateSnapshot(A<IAggregateRoot>._)).Returns(false);

		var composite = new CompositeSnapshotStrategy(
			CompositeSnapshotStrategy.CompositeMode.Any,
			falseStrategy1,
			falseStrategy2);

		// Act
		var result = composite.ShouldCreateSnapshot(aggregate);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ShouldCreateSnapshot_AnyMode_ReturnTrue_WhenFirstStrategyReturnsTrue()
	{
		// Arrange
		var aggregate = A.Fake<IAggregateRoot>();
		_ = A.CallTo(() => aggregate.Id).Returns("test-id");
		_ = A.CallTo(() => aggregate.Version).Returns(10);

		var trueStrategy = A.Fake<ISnapshotStrategy>();
		_ = A.CallTo(() => trueStrategy.ShouldCreateSnapshot(A<IAggregateRoot>._)).Returns(true);

		var composite = new CompositeSnapshotStrategy(
			CompositeSnapshotStrategy.CompositeMode.Any,
			trueStrategy);

		// Act
		var result = composite.ShouldCreateSnapshot(aggregate);

		// Assert
		result.ShouldBeTrue();
	}

	#endregion ShouldCreateSnapshot - Any Mode Tests

	#region ShouldCreateSnapshot - All Mode Tests

	[Fact]
	public void ShouldCreateSnapshot_AllMode_ReturnTrue_WhenAllStrategiesReturnTrue()
	{
		// Arrange
		var aggregate = A.Fake<IAggregateRoot>();
		_ = A.CallTo(() => aggregate.Id).Returns("test-id");
		_ = A.CallTo(() => aggregate.Version).Returns(10);

		var trueStrategy1 = A.Fake<ISnapshotStrategy>();
		_ = A.CallTo(() => trueStrategy1.ShouldCreateSnapshot(A<IAggregateRoot>._)).Returns(true);

		var trueStrategy2 = A.Fake<ISnapshotStrategy>();
		_ = A.CallTo(() => trueStrategy2.ShouldCreateSnapshot(A<IAggregateRoot>._)).Returns(true);

		var composite = new CompositeSnapshotStrategy(
			CompositeSnapshotStrategy.CompositeMode.All,
			trueStrategy1,
			trueStrategy2);

		// Act
		var result = composite.ShouldCreateSnapshot(aggregate);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ShouldCreateSnapshot_AllMode_ReturnFalse_WhenAnyStrategyReturnsFalse()
	{
		// Arrange
		var aggregate = A.Fake<IAggregateRoot>();
		_ = A.CallTo(() => aggregate.Id).Returns("test-id");
		_ = A.CallTo(() => aggregate.Version).Returns(10);

		var trueStrategy = A.Fake<ISnapshotStrategy>();
		_ = A.CallTo(() => trueStrategy.ShouldCreateSnapshot(A<IAggregateRoot>._)).Returns(true);

		var falseStrategy = A.Fake<ISnapshotStrategy>();
		_ = A.CallTo(() => falseStrategy.ShouldCreateSnapshot(A<IAggregateRoot>._)).Returns(false);

		var composite = new CompositeSnapshotStrategy(
			CompositeSnapshotStrategy.CompositeMode.All,
			trueStrategy,
			falseStrategy);

		// Act
		var result = composite.ShouldCreateSnapshot(aggregate);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion ShouldCreateSnapshot - All Mode Tests

	#region AddStrategy Tests

	[Fact]
	public void AddStrategy_ShouldIncreaseStrategyCount()
	{
		// Arrange
		var strategy1 = A.Fake<ISnapshotStrategy>();
		var strategy2 = A.Fake<ISnapshotStrategy>();

		var composite = new CompositeSnapshotStrategy(
			CompositeSnapshotStrategy.CompositeMode.Any,
			strategy1);

		// Act
		composite.AddStrategy(strategy2);

		// Assert
		composite.StrategyCount.ShouldBe(2);
	}

	[Fact]
	public void AddStrategy_ShouldThrowArgumentNullException_WhenStrategyIsNull()
	{
		// Arrange
		var strategy = A.Fake<ISnapshotStrategy>();
		var composite = new CompositeSnapshotStrategy(
			CompositeSnapshotStrategy.CompositeMode.Any,
			strategy);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => composite.AddStrategy(null!));
	}

	#endregion AddStrategy Tests

	#region RemoveStrategy Tests

	[Fact]
	public void RemoveStrategy_ShouldDecreaseStrategyCount()
	{
		// Arrange
		var strategy1 = A.Fake<ISnapshotStrategy>();
		var strategy2 = A.Fake<ISnapshotStrategy>();

		var composite = new CompositeSnapshotStrategy(
			CompositeSnapshotStrategy.CompositeMode.Any,
			strategy1,
			strategy2);

		// Act
		var removed = composite.RemoveStrategy(strategy1);

		// Assert
		removed.ShouldBeTrue();
		composite.StrategyCount.ShouldBe(1);
	}

	[Fact]
	public void RemoveStrategy_ShouldReturnFalse_WhenStrategyNotFound()
	{
		// Arrange
		var strategy1 = A.Fake<ISnapshotStrategy>();
		var strategy2 = A.Fake<ISnapshotStrategy>();

		var composite = new CompositeSnapshotStrategy(
			CompositeSnapshotStrategy.CompositeMode.Any,
			strategy1);

		// Act
		var removed = composite.RemoveStrategy(strategy2);

		// Assert
		removed.ShouldBeFalse();
		composite.StrategyCount.ShouldBe(1);
	}

	#endregion RemoveStrategy Tests

	#region Null Validation Tests

	[Fact]
	public void ShouldCreateSnapshot_ShouldThrowArgumentNullException_WhenAggregateIsNull()
	{
		// Arrange
		var strategy = A.Fake<ISnapshotStrategy>();
		var composite = new CompositeSnapshotStrategy(
			CompositeSnapshotStrategy.CompositeMode.Any,
			strategy);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => composite.ShouldCreateSnapshot(null!));
	}

	#endregion Null Validation Tests
}
