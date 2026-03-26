// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Snapshots;

/// <summary>
/// Snapshot strategy that uses incremental (delta-based) snapshots.
/// Triggers a snapshot save on every commit (deltas are cheap), and
/// triggers full compaction when delta count reaches the threshold (R27.64).
/// </summary>
/// <remarks>
/// <para>
/// Unlike interval or time-based strategies that skip most commits,
/// this strategy saves a delta on every commit. The cost is low because
/// deltas only contain changes, not the full state.
/// </para>
/// <para>
/// Integrates with the existing <see cref="ISnapshotStrategy"/> framework (R27.66).
/// </para>
/// </remarks>
public sealed class IncrementalSnapshotStrategy : ISnapshotStrategy
{
	private readonly int _compactionThreshold;

	/// <summary>
	/// Initializes a new instance with the specified compaction threshold.
	/// </summary>
	/// <param name="compactionThreshold">
	/// Number of deltas before a full snapshot compaction is triggered.
	/// Default is 10.
	/// </param>
	public IncrementalSnapshotStrategy(int compactionThreshold = 10)
	{
		if (compactionThreshold < 1)
		{
			throw new ArgumentOutOfRangeException(
				nameof(compactionThreshold),
				compactionThreshold,
				"Compaction threshold must be at least 1.");
		}

		_compactionThreshold = compactionThreshold;
	}

	/// <summary>
	/// Gets the compaction threshold (number of deltas before full snapshot).
	/// </summary>
	public int CompactionThreshold => _compactionThreshold;

	/// <summary>
	/// Always returns true -- incremental snapshots save a delta on every commit.
	/// The actual delta vs. full decision is made by the snapshot manager.
	/// </summary>
	[RequiresUnreferencedCode("Snapshot strategy evaluation may require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("Snapshot strategy evaluation may require dynamic code generation.")]
	public bool ShouldCreateSnapshot(IAggregateRoot aggregate)
	{
		// Always save a delta -- the incremental store handles compaction
		return true;
	}
}
