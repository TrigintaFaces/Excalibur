// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;

using Microsoft.Extensions.Options;

namespace Excalibur.LeaderElection.Kubernetes;

/// <summary>
/// Validates <see cref="KubernetesLeaderElectionOptions"/> cross-property constraints.
/// </summary>
/// <remarks>
/// Ensures, in addition to every base <see cref="LeaderElectionOptions"/> invariant (both pairwise
/// <see cref="LeaderElectionOptions.RenewInterval"/>/<see cref="LeaderElectionOptions.GracePeriod"/> &lt;
/// <see cref="LeaderElectionOptions.LeaseDuration"/> checks <em>and</em> the split-brain self-demotion
/// sum-invariant <c>RenewInterval + GracePeriod + clock-skew &lt; LeaseDuration</c>):
/// <list type="bullet">
/// <item><see cref="LeaderElectionOptions.RetryInterval"/> is strictly positive.</item>
/// <item><see cref="KubernetesLeaderElectionOptions.MaxRetries"/> is non-negative.</item>
/// <item><see cref="KubernetesLeaderElectionOptions.MaxRetryDelay"/> is non-negative.</item>
/// </list>
/// The base invariants are enforced by delegating to <see cref="LeaderElectionOptionsValidator"/> so the
/// safety-critical sum-invariant cannot be silently omitted from the derived validator.
/// </remarks>
internal sealed class KubernetesLeaderElectionOptionsValidator : IValidateOptions<KubernetesLeaderElectionOptions>
{
	// KubernetesLeaderElectionOptions IS-A LeaderElectionOptions, so the base validator enforces every
	// cross-property lease invariant — including the split-brain self-demotion sum-invariant that the
	// pairwise checks alone miss. Composing with it (rather than re-implementing the checks) makes the
	// sum-invariant structurally present in the derived validator.
	private static readonly LeaderElectionOptionsValidator BaseValidator = new();

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, KubernetesLeaderElectionOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var baseResult = BaseValidator.Validate(name, options);
		if (baseResult.Failed)
		{
			return baseResult;
		}

		// Lower-bound checks for the timing/retry knobs (restored after the int-ms -> TimeSpan move that
		// dropped the [Range] attributes): reject negative/zero configuration at ValidateOnStart rather
		// than letting it surface as a runtime Task.Delay ArgumentOutOfRangeException.
		if (options.RetryInterval <= TimeSpan.Zero)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(LeaderElectionOptions.RetryInterval)} ({options.RetryInterval}) must be greater than zero.");
		}

		if (options.MaxRetries < 0)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(KubernetesLeaderElectionOptions.MaxRetries)} ({options.MaxRetries}) must be non-negative.");
		}

		if (options.MaxRetryDelay < TimeSpan.Zero)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(KubernetesLeaderElectionOptions.MaxRetryDelay)} ({options.MaxRetryDelay}) must be non-negative.");
		}

		return ValidateOptionsResult.Success;
	}
}
