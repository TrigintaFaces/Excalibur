// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Snapshots;

namespace Excalibur.EventSourcing.Tests.Snapshots;

/// <summary>
/// Unit tests for IncrementalSnapshotStrategy (R27.61-R27.67).
/// Validates strategy behavior, compaction threshold, and integration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class IncrementalSnapshotStrategyShould
{
	/// <summary>
	/// R27.63: ShouldCreateSnapshot always returns true (deltas are cheap).
	/// </summary>
	[Fact]
	public void AlwaysReturnTrueForShouldCreateSnapshot()
	{
		// Arrange
		var strategy = new IncrementalSnapshotStrategy();
		var aggregate = A.Fake<Excalibur.Domain.Model.IAggregateRoot>();

		// Act
#pragma warning disable IL2026 // Members annotated with RequiresUnreferencedCode
#pragma warning disable IL3050 // Members annotated with RequiresDynamicCode
		var result = strategy.ShouldCreateSnapshot(aggregate);
#pragma warning restore IL3050
#pragma warning restore IL2026

		// Assert
		result.ShouldBeTrue();
	}

	/// <summary>
	/// Default compaction threshold is 10.
	/// </summary>
	[Fact]
	public void HaveDefaultCompactionThresholdOfTen()
	{
		var strategy = new IncrementalSnapshotStrategy();
		strategy.CompactionThreshold.ShouldBe(10);
	}

	/// <summary>
	/// Custom compaction threshold is respected.
	/// </summary>
	[Fact]
	public void AcceptCustomCompactionThreshold()
	{
		var strategy = new IncrementalSnapshotStrategy(compactionThreshold: 25);
		strategy.CompactionThreshold.ShouldBe(25);
	}

	/// <summary>
	/// Throws on invalid compaction threshold (< 1).
	/// </summary>
	[Fact]
	public void ThrowOnInvalidCompactionThreshold()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new IncrementalSnapshotStrategy(compactionThreshold: 0));
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new IncrementalSnapshotStrategy(compactionThreshold: -1));
	}

	/// <summary>
	/// Minimum threshold of 1 is valid (every save is a compaction).
	/// </summary>
	[Fact]
	public void AcceptMinimumCompactionThresholdOfOne()
	{
		var strategy = new IncrementalSnapshotStrategy(compactionThreshold: 1);
		strategy.CompactionThreshold.ShouldBe(1);
	}
}

/// <summary>
/// Test state for incremental snapshot tests.
/// </summary>
public sealed class SnapshotTestState
{
	public string Name { get; set; } = string.Empty;
	public decimal Total { get; set; }
	public int ItemCount { get; set; }
}
