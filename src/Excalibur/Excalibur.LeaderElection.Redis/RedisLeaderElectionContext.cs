// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.Dispatch;
using Excalibur.Dispatch.LeaderElection.Fencing;

namespace Excalibur.LeaderElection.Redis;

/// <summary>
/// Bundles the optional cross-cutting collaborators a <see cref="RedisLeaderElection"/> can opt into.
/// </summary>
/// <remarks>
/// Both members default to <see langword="null"/>, so an empty context (the default) preserves the plain
/// deadline-governed, no-fencing behavior. Populate a member to enable that feature. A successful
/// ownership check that disproves ownership always causes immediate retirement.
/// </remarks>
public sealed class RedisLeaderElectionContext
{
	/// <summary>
	/// Gets the optional failure classifier. When supplied, a renewal-loop fault classified
	/// <see cref="MessageFailureKind.Permanent"/> triggers immediate self-demotion instead of waiting for the
	/// local renewal deadline; the classifier can only accelerate (never delay) relinquish.
	/// </summary>
	/// <value>The failure classifier, or <see langword="null"/> for no classifier-based acceleration.</value>
	public IMessageFailureClassifier? FailureClassifier { get; init; }

	/// <summary>
	/// Gets the optional fencing-token provider. When supplied, a monotonic fencing token is issued at each
	/// leadership acquisition so a stale leader's token falls below the resource's high-water mark.
	/// </summary>
	/// <value>The fencing-token provider, or <see langword="null"/> when fencing is not enabled.</value>
	public IFencingTokenIssuer? FencingTokenProvider { get; init; }
}
