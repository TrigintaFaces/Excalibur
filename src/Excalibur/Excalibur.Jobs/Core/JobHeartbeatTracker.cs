// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

namespace Excalibur.Jobs.Core;

/// <summary>
/// Tracks heartbeat timestamps for background jobs. Register as a singleton in DI.
/// </summary>
public sealed class JobHeartbeatTracker
{
	private readonly ConcurrentDictionary<string, DateTimeOffset> _heartbeats = new(StringComparer.Ordinal);

	/// <summary>
	/// Records a heartbeat for the specified job.
	/// </summary>
	/// <param name="jobName"> The job name. </param>
	public void RecordHeartbeat(string jobName) => _heartbeats[jobName] = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets the last heartbeat time for the specified job.
	/// </summary>
	/// <param name="jobName"> The job name. </param>
	/// <returns> The last heartbeat time, or <see langword="null"/> if no heartbeat has been recorded. </returns>
	public DateTimeOffset? GetLastHeartbeat(string jobName) =>
		_heartbeats.TryGetValue(jobName, out var ts) ? ts : null;
}
