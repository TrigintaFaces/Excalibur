// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing;

/// <summary>
/// Configures how long a consistent read waits for a projection to catch up, and how often it looks.
/// </summary>
public sealed class ProjectionSessionOptions
{
	/// <summary>
	/// Gets or sets how long a consistent read waits for the projection to reach the session token.
	/// </summary>
	/// <value>The bound. Defaults to 5 seconds. Must be between 50 milliseconds and 2 minutes.</value>
	/// <remarks>
	/// Reached while a projection is merely behind, this returns
	/// <see cref="ProjectionFreshness.BoundExpired"/> rather than throwing: the read still answers, and
	/// the caller decides whether slightly stale data is acceptable. Setting this high does not make a
	/// stopped projection start; it makes a request hang while one is stopped.
	/// </remarks>
	public TimeSpan Bound { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets how often the read re-checks the projection's progress while waiting.
	/// </summary>
	/// <value>The poll interval. Defaults to 25 milliseconds. Must be between 1 millisecond and the bound.</value>
	/// <remarks>
	/// Polling rather than signalling is deliberate: the progress a read waits on can be made by ANOTHER
	/// PROCESS, which no in-process signal can observe. A signal would be faster and would silently fail
	/// to wake for exactly the multi-instance deployments this guarantee matters most in.
	/// </remarks>
	public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(25);
}
