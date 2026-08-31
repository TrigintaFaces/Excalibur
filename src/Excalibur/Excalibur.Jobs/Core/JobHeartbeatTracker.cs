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
	private readonly TimeProvider _timeProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="JobHeartbeatTracker" /> class.
	/// </summary>
	/// <param name="timeProvider">
	/// The clock heartbeats are stamped from. Defaults to <see cref="TimeProvider.System" />. The health
	/// check that reads these stamps decides healthy, degraded or unhealthy by comparing them against its
	/// own clock, so both sides have to be drivable for that decision to be testable without waiting.
	/// </param>
	public JobHeartbeatTracker(TimeProvider? timeProvider = null) => _timeProvider = timeProvider ?? TimeProvider.System;

	/// <summary>
	/// Records a heartbeat for the specified job.
	/// </summary>
	/// <param name="jobName"> The job name. </param>
	public void RecordHeartbeat(string jobName) => _heartbeats[jobName] = _timeProvider.GetUtcNow();

	/// <summary>
	/// Gets the last heartbeat time for the specified job.
	/// </summary>
	/// <param name="jobName"> The job name. </param>
	/// <returns> The last heartbeat time, or <see langword="null"/> if no heartbeat has been recorded. </returns>
	public DateTimeOffset? GetLastHeartbeat(string jobName) =>
		_heartbeats.TryGetValue(jobName, out var ts) ? ts : null;
}
