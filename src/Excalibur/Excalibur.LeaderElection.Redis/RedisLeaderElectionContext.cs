// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch;
using Excalibur.Dispatch.LeaderElection.Fencing;

namespace Excalibur.LeaderElection.Redis;

/// <summary>
/// Bundles the optional cross-cutting collaborators a <see cref="RedisLeaderElection"/> can opt into,
/// keeping its constructor within the Microsoft-first five-parameter guideline instead of threading each
/// optional service through as a separate argument.
/// </summary>
/// <remarks>
/// Both members default to <see langword="null"/>, so an empty context (the default) preserves the plain
/// grace-only, no-fencing behavior. Populate a member to enable that feature.
/// </remarks>
public sealed class RedisLeaderElectionContext
{
	/// <summary>
	/// Gets the optional failure classifier. When supplied, a renewal-loop fault classified
	/// <see cref="MessageFailureKind.Permanent"/> triggers immediate self-demotion instead of waiting the
	/// full grace period; the classifier can only accelerate (never delay) relinquish.
	/// </summary>
	/// <value>The failure classifier, or <see langword="null"/> for grace-only behavior.</value>
	public IMessageFailureClassifier? FailureClassifier { get; init; }

	/// <summary>
	/// Gets the optional fencing-token provider. When supplied, a monotonic fencing token is issued at each
	/// leadership acquisition so a stale leader's token falls below the resource's high-water mark.
	/// </summary>
	/// <value>The fencing-token provider, or <see langword="null"/> when fencing is not enabled.</value>
	public IFencingTokenProvider? FencingTokenProvider { get; init; }
}
