// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Snapshots;

using FakeItEasy;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Snapshots;

/// <summary>
/// Unit tests for <see cref="NoSnapshotStrategy"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class NoSnapshotStrategyShould
{
	[Fact]
	public void ReturnFalse_ForAnyAggregate()
	{
		// Arrange
		var strategy = NoSnapshotStrategy.Instance;
		var aggregate = A.Fake<IAggregateRoot>();
		A.CallTo(() => aggregate.Id).Returns("test-agg-1");
		A.CallTo(() => aggregate.Version).Returns(1000);

		// Act
		var result = strategy.ShouldCreateSnapshot(aggregate);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ReturnFalse_ForHighVersionAggregate()
	{
		// Arrange
		var strategy = NoSnapshotStrategy.Instance;
		var aggregate = A.Fake<IAggregateRoot>();
		A.CallTo(() => aggregate.Id).Returns("test-agg-2");
		A.CallTo(() => aggregate.Version).Returns(999999);

		// Act
		var result = strategy.ShouldCreateSnapshot(aggregate);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void BeSingleton()
	{
		// Arrange & Act
		var instance1 = NoSnapshotStrategy.Instance;
		var instance2 = NoSnapshotStrategy.Instance;

		// Assert
		instance1.ShouldBeSameAs(instance2);
	}

	[Fact]
	public void ReturnFalse_ForMultipleCalls()
	{
		// Arrange
		var strategy = NoSnapshotStrategy.Instance;
		var aggregate = A.Fake<IAggregateRoot>();
		A.CallTo(() => aggregate.Id).Returns("test-agg-3");

		// Act & Assert - should always return false
		strategy.ShouldCreateSnapshot(aggregate).ShouldBeFalse();
		strategy.ShouldCreateSnapshot(aggregate).ShouldBeFalse();
		strategy.ShouldCreateSnapshot(aggregate).ShouldBeFalse();
	}
}
