// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Snapshots;

/// <summary>
/// Evaluates whether an auto-snapshot should be created based on configured thresholds.
/// </summary>
/// <remarks>
/// <para>
/// Pure evaluation logic -- no I/O, no DI resolution on the hot path. Just arithmetic
/// on version numbers and timestamps. Any single matching threshold triggers a snapshot.
/// </para>
/// </remarks>
internal static class AutoSnapshotPolicy
{
	/// <summary>
	/// Determines whether a snapshot should be created for the given options and context.
	/// </summary>
	/// <param name="options">The auto-snapshot configuration.</param>
	/// <param name="context">The snapshot decision context.</param>
	/// <param name="timeProvider">The time provider for clock-based thresholds. Defaults to <see cref="TimeProvider.System"/>.</param>
	/// <returns><see langword="true"/> if any configured threshold is met; otherwise, <see langword="false"/>.</returns>
	internal static bool ShouldSnapshot(AutoSnapshotOptions options, SnapshotDecisionContext context, TimeProvider? timeProvider = null)
	{
		// Never snapshot at version 1 (just created, no point snapshotting)
		if (context.CurrentVersion <= 1)
		{
			return false;
		}

		// Event count threshold: snapshot after N events since last snapshot
		if (options.EventCountThreshold is { } countThreshold
			&& context.EventsSinceSnapshot >= countThreshold)
		{
			return true;
		}

		// Time threshold: snapshot if last snapshot is older than configured duration
		if (options.TimeThreshold is { } timeThreshold)
		{
			var utcNow = (timeProvider ?? TimeProvider.System).GetUtcNow();
			if (context.LastSnapshotTimestamp is { } lastTimestamp
				&& (utcNow - lastTimestamp) >= timeThreshold)
			{
				return true;
			}

			// Never snapshotted: always trigger (aggregate exists but no snapshot)
			if (context.LastSnapshotTimestamp is null)
			{
				return true;
			}
		}

		// Version threshold: snapshot if aggregate version exceeds the threshold
		if (options.VersionThreshold is { } versionThreshold
			&& context.CurrentVersion >= versionThreshold)
		{
			return true;
		}

		// Custom policy: invoke the user-provided function
		if (options.CustomPolicy is { } customPolicy && customPolicy(context))
		{
			return true;
		}

		return false;
	}
}
