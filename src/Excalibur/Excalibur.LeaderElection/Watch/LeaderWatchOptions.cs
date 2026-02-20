// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.LeaderElection.Watch;

/// <summary>
/// Configuration options for the leader election watcher.
/// </summary>
/// <remarks>
/// Follows the <c>IOptions&lt;T&gt;</c> pattern from <c>Microsoft.Extensions.Options</c>.
/// Property count: 2 (within the ≤10-property quality gate).
/// </remarks>
public class LeaderWatchOptions
{
	/// <summary>
	/// Gets or sets the interval between leader state polls.
	/// </summary>
	/// <value>The poll interval. Defaults to 5 seconds.</value>
	[Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
	public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets a value indicating whether to include heartbeat events in the watch stream.
	/// </summary>
	/// <remarks>
	/// When <see langword="true"/>, the watcher emits an event on every poll cycle, even if the
	/// leader has not changed. This is useful for liveness monitoring. When <see langword="false"/>,
	/// only actual leader changes are emitted.
	/// </remarks>
	/// <value>
	/// <see langword="true"/> to include heartbeat events; otherwise, <see langword="false"/>.
	/// Defaults to <see langword="false"/>.
	/// </value>
	public bool IncludeHeartbeats { get; set; }
}
