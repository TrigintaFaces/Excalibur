// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Snapshots;

/// <summary>
/// Snapshot strategy based on time intervals.
/// </summary>
/// <remarks>
/// <para>
/// Creates snapshots when a specified time interval has elapsed since the last snapshot.
/// Useful for aggregates that are frequently accessed but may not have many events.
/// </para>
/// </remarks>
public sealed class TimeBasedSnapshotStrategy : ISnapshotStrategy
{
	private readonly TimeSpan _interval;
	private readonly ConcurrentDictionary<string, DateTimeOffset> _lastSnapshotTimes;

	/// <summary>
	/// Initializes a new instance of the <see cref="TimeBasedSnapshotStrategy"/> class.
	/// </summary>
	/// <param name="interval">Time interval between snapshots.</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when interval is zero or negative.</exception>
	public TimeBasedSnapshotStrategy(TimeSpan interval)
	{
		if (interval <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(
					nameof(interval),
					Resources.TimeBasedSnapshotStrategy_IntervalMustBeGreaterThanZero);
		}

		_interval = interval;
		_lastSnapshotTimes = new ConcurrentDictionary<string, DateTimeOffset>(StringComparer.Ordinal);
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Snapshot strategy evaluation may require types that cannot be statically analyzed. Consider using source generation.")]
	[RequiresDynamicCode("Snapshot strategy evaluation may require dynamic code generation which is not compatible with AOT compilation.")]
	public bool ShouldCreateSnapshot(IAggregateRoot aggregate)
	{
		ArgumentNullException.ThrowIfNull(aggregate);

		var now = DateTimeOffset.UtcNow;

		// Check if we have a last snapshot time for this aggregate
		if (_lastSnapshotTimes.TryGetValue(aggregate.Id, out var lastSnapshotTime))
		{
			// Check if enough time has passed
			if (now - lastSnapshotTime >= _interval)
			{
				// Update the last snapshot time
				_lastSnapshotTimes[aggregate.Id] = now;
				return true;
			}

			return false;
		}

		// First time seeing this aggregate, create snapshot
		_lastSnapshotTimes[aggregate.Id] = now;
		return true;
	}

	/// <summary>
	/// Clears the tracked snapshot times for all aggregates.
	/// </summary>
	public void ClearTrackedTimes() => _lastSnapshotTimes.Clear();

	/// <summary>
	/// Gets the last snapshot time for a specific aggregate.
	/// </summary>
	/// <param name="aggregateId">The aggregate ID.</param>
	/// <returns>The last snapshot time, or null if not tracked.</returns>
	public DateTimeOffset? GetLastSnapshotTime(string aggregateId) =>
		_lastSnapshotTimes.TryGetValue(aggregateId, out var time) ? time : null;
}
