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
/// Extended unit tests for <see cref="CompositeSnapshotStrategy"/> covering
/// edge cases, added strategy behavior, and multi-strategy interactions.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CompositeSnapshotStrategyExtendedShould
{
	#region AddStrategy Affects ShouldCreateSnapshot

	[Fact]
	public void ShouldCreateSnapshot_ShouldIncludeAddedStrategy_InAnyMode()
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
			falseStrategy);

		// First check: only false strategy
		composite.ShouldCreateSnapshot(aggregate).ShouldBeFalse();

		// Act - add true strategy
		composite.AddStrategy(trueStrategy);

		// Assert
		composite.ShouldCreateSnapshot(aggregate).ShouldBeTrue();
		composite.StrategyCount.ShouldBe(2);
	}

	[Fact]
	public void ShouldCreateSnapshot_ShouldIncludeAddedStrategy_InAllMode()
	{
		// Arrange
		var aggregate = A.Fake<IAggregateRoot>();
		_ = A.CallTo(() => aggregate.Id).Returns("test-id");
		_ = A.CallTo(() => aggregate.Version).Returns(10);

		var trueStrategy1 = A.Fake<ISnapshotStrategy>();
		_ = A.CallTo(() => trueStrategy1.ShouldCreateSnapshot(A<IAggregateRoot>._)).Returns(true);

		var falseStrategy = A.Fake<ISnapshotStrategy>();
		_ = A.CallTo(() => falseStrategy.ShouldCreateSnapshot(A<IAggregateRoot>._)).Returns(false);

		var composite = new CompositeSnapshotStrategy(
			CompositeSnapshotStrategy.CompositeMode.All,
			trueStrategy1);

		// First check: only true strategy - All mode returns true
		composite.ShouldCreateSnapshot(aggregate).ShouldBeTrue();

		// Act - add false strategy
		composite.AddStrategy(falseStrategy);

		// Assert - All mode now returns false because one strategy returns false
		composite.ShouldCreateSnapshot(aggregate).ShouldBeFalse();
		composite.StrategyCount.ShouldBe(2);
	}

	#endregion

	#region RemoveStrategy Affects ShouldCreateSnapshot

	[Fact]
	public void ShouldCreateSnapshot_ShouldExcludeRemovedStrategy()
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

		// All mode with true+false should be false
		composite.ShouldCreateSnapshot(aggregate).ShouldBeFalse();

		// Act - remove false strategy
		composite.RemoveStrategy(falseStrategy);

		// Assert - Now only true strategy remains
		composite.ShouldCreateSnapshot(aggregate).ShouldBeTrue();
		composite.StrategyCount.ShouldBe(1);
	}

	#endregion

	#region Multiple Strategies

	[Fact]
	public void ShouldCreateSnapshot_AnyMode_WithThreeStrategies_FirstTrue()
	{
		// Arrange
		var aggregate = A.Fake<IAggregateRoot>();
		_ = A.CallTo(() => aggregate.Id).Returns("test-id");

		var trueStrategy = A.Fake<ISnapshotStrategy>();
		_ = A.CallTo(() => trueStrategy.ShouldCreateSnapshot(A<IAggregateRoot>._)).Returns(true);

		var falseStrategy1 = A.Fake<ISnapshotStrategy>();
		_ = A.CallTo(() => falseStrategy1.ShouldCreateSnapshot(A<IAggregateRoot>._)).Returns(false);

		var falseStrategy2 = A.Fake<ISnapshotStrategy>();
		_ = A.CallTo(() => falseStrategy2.ShouldCreateSnapshot(A<IAggregateRoot>._)).Returns(false);

		var composite = new CompositeSnapshotStrategy(
			CompositeSnapshotStrategy.CompositeMode.Any,
			trueStrategy, falseStrategy1, falseStrategy2);

		// Act
		var result = composite.ShouldCreateSnapshot(aggregate);

		// Assert
		result.ShouldBeTrue();
		composite.StrategyCount.ShouldBe(3);
	}

	[Fact]
	public void ShouldCreateSnapshot_AllMode_WithThreeStrategies_AllTrue()
	{
		// Arrange
		var aggregate = A.Fake<IAggregateRoot>();
		_ = A.CallTo(() => aggregate.Id).Returns("test-id");

		var true1 = A.Fake<ISnapshotStrategy>();
		_ = A.CallTo(() => true1.ShouldCreateSnapshot(A<IAggregateRoot>._)).Returns(true);

		var true2 = A.Fake<ISnapshotStrategy>();
		_ = A.CallTo(() => true2.ShouldCreateSnapshot(A<IAggregateRoot>._)).Returns(true);

		var true3 = A.Fake<ISnapshotStrategy>();
		_ = A.CallTo(() => true3.ShouldCreateSnapshot(A<IAggregateRoot>._)).Returns(true);

		var composite = new CompositeSnapshotStrategy(
			CompositeSnapshotStrategy.CompositeMode.All,
			true1, true2, true3);

		// Act
		var result = composite.ShouldCreateSnapshot(aggregate);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ShouldCreateSnapshot_AllMode_WithThreeStrategies_OneFalse()
	{
		// Arrange
		var aggregate = A.Fake<IAggregateRoot>();
		_ = A.CallTo(() => aggregate.Id).Returns("test-id");

		var true1 = A.Fake<ISnapshotStrategy>();
		_ = A.CallTo(() => true1.ShouldCreateSnapshot(A<IAggregateRoot>._)).Returns(true);

		var false1 = A.Fake<ISnapshotStrategy>();
		_ = A.CallTo(() => false1.ShouldCreateSnapshot(A<IAggregateRoot>._)).Returns(false);

		var true2 = A.Fake<ISnapshotStrategy>();
		_ = A.CallTo(() => true2.ShouldCreateSnapshot(A<IAggregateRoot>._)).Returns(true);

		var composite = new CompositeSnapshotStrategy(
			CompositeSnapshotStrategy.CompositeMode.All,
			true1, false1, true2);

		// Act
		var result = composite.ShouldCreateSnapshot(aggregate);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region Single Strategy Behavior

	[Fact]
	public void ShouldCreateSnapshot_AnyMode_SingleFalseStrategy()
	{
		// Arrange
		var aggregate = A.Fake<IAggregateRoot>();
		_ = A.CallTo(() => aggregate.Id).Returns("test-id");

		var falseStrategy = A.Fake<ISnapshotStrategy>();
		_ = A.CallTo(() => falseStrategy.ShouldCreateSnapshot(A<IAggregateRoot>._)).Returns(false);

		var composite = new CompositeSnapshotStrategy(
			CompositeSnapshotStrategy.CompositeMode.Any,
			falseStrategy);

		// Act
		var result = composite.ShouldCreateSnapshot(aggregate);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ShouldCreateSnapshot_AllMode_SingleTrueStrategy()
	{
		// Arrange
		var aggregate = A.Fake<IAggregateRoot>();
		_ = A.CallTo(() => aggregate.Id).Returns("test-id");

		var trueStrategy = A.Fake<ISnapshotStrategy>();
		_ = A.CallTo(() => trueStrategy.ShouldCreateSnapshot(A<IAggregateRoot>._)).Returns(true);

		var composite = new CompositeSnapshotStrategy(
			CompositeSnapshotStrategy.CompositeMode.All,
			trueStrategy);

		// Act
		var result = composite.ShouldCreateSnapshot(aggregate);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ShouldCreateSnapshot_AllMode_SingleFalseStrategy()
	{
		// Arrange
		var aggregate = A.Fake<IAggregateRoot>();
		_ = A.CallTo(() => aggregate.Id).Returns("test-id");

		var falseStrategy = A.Fake<ISnapshotStrategy>();
		_ = A.CallTo(() => falseStrategy.ShouldCreateSnapshot(A<IAggregateRoot>._)).Returns(false);

		var composite = new CompositeSnapshotStrategy(
			CompositeSnapshotStrategy.CompositeMode.All,
			falseStrategy);

		// Act
		var result = composite.ShouldCreateSnapshot(aggregate);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region Unknown CompositeMode Tests

	[Fact]
	public void ShouldCreateSnapshot_ShouldThrowInvalidOperationException_ForUnknownCompositeMode()
	{
		// Arrange
		var aggregate = A.Fake<IAggregateRoot>();
		_ = A.CallTo(() => aggregate.Id).Returns("test-id");

		var strategy = A.Fake<ISnapshotStrategy>();
		_ = A.CallTo(() => strategy.ShouldCreateSnapshot(A<IAggregateRoot>._)).Returns(true);

		// Create with valid mode first
		var composite = new CompositeSnapshotStrategy(
			CompositeSnapshotStrategy.CompositeMode.Any,
			strategy);

		// Use reflection to set an invalid CompositeMode value
		var modeField = typeof(CompositeSnapshotStrategy).GetField("_mode",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		modeField?.SetValue(composite, (CompositeSnapshotStrategy.CompositeMode)999);

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => composite.ShouldCreateSnapshot(aggregate));
		exception.Message.ShouldContain("999"); // Unknown mode value in message
	}

	#endregion

	#region StrategyCount Property

	[Fact]
	public void StrategyCount_ShouldReflectInitialStrategies()
	{
		// Arrange
		var s1 = A.Fake<ISnapshotStrategy>();
		var s2 = A.Fake<ISnapshotStrategy>();
		var s3 = A.Fake<ISnapshotStrategy>();

		// Act
		var composite = new CompositeSnapshotStrategy(
			CompositeSnapshotStrategy.CompositeMode.Any,
			s1, s2, s3);

		// Assert
		composite.StrategyCount.ShouldBe(3);
	}

	[Fact]
	public void StrategyCount_ShouldUpdateAfterAddAndRemove()
	{
		// Arrange
		var s1 = A.Fake<ISnapshotStrategy>();
		var s2 = A.Fake<ISnapshotStrategy>();
		var composite = new CompositeSnapshotStrategy(
			CompositeSnapshotStrategy.CompositeMode.Any, s1);

		composite.StrategyCount.ShouldBe(1);

		// Act - Add
		composite.AddStrategy(s2);
		composite.StrategyCount.ShouldBe(2);

		// Act - Remove
		composite.RemoveStrategy(s1);
		composite.StrategyCount.ShouldBe(1);
	}

	#endregion
}
