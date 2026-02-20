// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.InMemory.Snapshots;

namespace Excalibur.Data.Tests.InMemory;

/// <summary>
/// Unit tests for <see cref="InMemorySnapshotOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class InMemorySnapshotOptionsShould : UnitTestBase
{
	#region Default Values Tests

	[Fact]
	public void HaveDefaultMaxSnapshots()
	{
		// Arrange & Act
		var options = new InMemorySnapshotOptions();

		// Assert
		options.MaxSnapshots.ShouldBe(10000);
	}

	[Fact]
	public void HaveDefaultMaxSnapshotsPerAggregate()
	{
		// Arrange & Act
		var options = new InMemorySnapshotOptions();

		// Assert
		options.MaxSnapshotsPerAggregate.ShouldBe(1);
	}

	#endregion Default Values Tests

	#region Property Setting Tests

	[Fact]
	public void AllowMaxSnapshotsToBeSet()
	{
		// Arrange & Act
		var options = new InMemorySnapshotOptions { MaxSnapshots = 50000 };

		// Assert
		options.MaxSnapshots.ShouldBe(50000);
	}

	[Fact]
	public void AllowZeroMaxSnapshotsForUnlimited()
	{
		// Arrange & Act
		var options = new InMemorySnapshotOptions { MaxSnapshots = 0 };

		// Assert
		options.MaxSnapshots.ShouldBe(0);
	}

	[Fact]
	public void AllowMaxSnapshotsPerAggregateToBeSet()
	{
		// Arrange & Act
		var options = new InMemorySnapshotOptions { MaxSnapshotsPerAggregate = 5 };

		// Assert
		options.MaxSnapshotsPerAggregate.ShouldBe(5);
	}

	[Fact]
	public void AllowZeroMaxSnapshotsPerAggregateForUnlimited()
	{
		// Arrange & Act
		var options = new InMemorySnapshotOptions { MaxSnapshotsPerAggregate = 0 };

		// Assert
		options.MaxSnapshotsPerAggregate.ShouldBe(0);
	}

	#endregion Property Setting Tests

	#region Configuration Scenario Tests

	[Fact]
	public void CreateHighCapacityConfiguration()
	{
		// Arrange & Act
		var options = new InMemorySnapshotOptions
		{
			MaxSnapshots = 100000,
			MaxSnapshotsPerAggregate = 10,
		};

		// Assert
		options.MaxSnapshots.ShouldBe(100000);
		options.MaxSnapshotsPerAggregate.ShouldBe(10);
	}

	[Fact]
	public void CreateMinimalConfiguration()
	{
		// Arrange & Act
		var options = new InMemorySnapshotOptions
		{
			MaxSnapshots = 100,
			MaxSnapshotsPerAggregate = 1,
		};

		// Assert
		options.MaxSnapshots.ShouldBe(100);
		options.MaxSnapshotsPerAggregate.ShouldBe(1);
	}

	[Fact]
	public void CreateUnlimitedConfiguration()
	{
		// Arrange & Act
		var options = new InMemorySnapshotOptions
		{
			MaxSnapshots = 0,
			MaxSnapshotsPerAggregate = 0,
		};

		// Assert
		options.MaxSnapshots.ShouldBe(0);
		options.MaxSnapshotsPerAggregate.ShouldBe(0);
	}

	[Fact]
	public void CreateHistoryPreservingConfiguration()
	{
		// Arrange & Act - Keep many snapshots per aggregate for history
		var options = new InMemorySnapshotOptions
		{
			MaxSnapshots = 50000,
			MaxSnapshotsPerAggregate = 100,
		};

		// Assert
		options.MaxSnapshots.ShouldBe(50000);
		options.MaxSnapshotsPerAggregate.ShouldBe(100);
	}

	[Fact]
	public void CreateMemoryConstrainedConfiguration()
	{
		// Arrange & Act - Limited memory scenario
		var options = new InMemorySnapshotOptions
		{
			MaxSnapshots = 1000,
			MaxSnapshotsPerAggregate = 1,
		};

		// Assert
		options.MaxSnapshots.ShouldBe(1000);
		options.MaxSnapshotsPerAggregate.ShouldBe(1);
	}

	#endregion Configuration Scenario Tests
}
