// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Excalibur.Dispatch.LeaderElection;

namespace Excalibur.LeaderElection.InMemory;

/// <summary>
/// Shared state for in-memory leader election instances, enabling coordination
/// between multiple <see cref="InMemoryLeaderElection"/> instances in the same process.
/// </summary>
/// <remarks>
/// <para>
/// In production, use <see cref="Default"/> so all instances share the same state
/// (which is the purpose of in-memory leader election). In tests, create a new instance
/// per test to ensure isolation.
/// </para>
/// </remarks>
public sealed class InMemoryLeaderElectionSharedState
{
	/// <summary>
	/// Gets the default shared state instance for production use.
	/// </summary>
	public static InMemoryLeaderElectionSharedState Default { get; } = new();

	/// <summary>
	/// Gets the dictionary of current leaders per resource name.
	/// </summary>
	internal ConcurrentDictionary<string, string?> Leaders { get; } = new(StringComparer.Ordinal);

	/// <summary>
	/// Gets the dictionary of candidate health data per resource name.
	/// </summary>
	internal ConcurrentDictionary<string, ConcurrentDictionary<string, CandidateHealth>> Candidates { get; } = new(StringComparer.Ordinal);
}
