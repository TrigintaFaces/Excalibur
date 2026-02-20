// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.InMemory.Snapshots;

namespace Excalibur.Data.Tests.InMemory.Snapshots;

/// <summary>
/// Unit tests for <see cref="InMemorySnapshotOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.InMemory")]
public sealed class InMemorySnapshotOptionsShould
{
	[Fact]
	public void HaveDefaultMaxSnapshotsOf10000()
	{
		// Arrange & Act
		var options = new InMemorySnapshotOptions();

		// Assert
		options.MaxSnapshots.ShouldBe(10000);
	}

	[Fact]
	public void HaveDefaultMaxSnapshotsPerAggregateOf1()
	{
		// Arrange & Act
		var options = new InMemorySnapshotOptions();

		// Assert
		options.MaxSnapshotsPerAggregate.ShouldBe(1);
	}

	[Fact]
	public void AllowMaxSnapshotsToBeCustomized()
	{
		// Arrange
		var options = new InMemorySnapshotOptions();

		// Act
		options.MaxSnapshots = 500;

		// Assert
		options.MaxSnapshots.ShouldBe(500);
	}

	[Fact]
	public void AllowMaxSnapshotsPerAggregateToBeCustomized()
	{
		// Arrange
		var options = new InMemorySnapshotOptions();

		// Act
		options.MaxSnapshotsPerAggregate = 10;

		// Assert
		options.MaxSnapshotsPerAggregate.ShouldBe(10);
	}

	[Fact]
	public void AllowZeroMaxSnapshots_MeaningUnlimited()
	{
		// Arrange
		var options = new InMemorySnapshotOptions();

		// Act
		options.MaxSnapshots = 0;

		// Assert
		options.MaxSnapshots.ShouldBe(0);
	}

	[Fact]
	public void AllowZeroMaxSnapshotsPerAggregate_MeaningUnlimited()
	{
		// Arrange
		var options = new InMemorySnapshotOptions();

		// Act
		options.MaxSnapshotsPerAggregate = 0;

		// Assert
		options.MaxSnapshotsPerAggregate.ShouldBe(0);
	}
}
