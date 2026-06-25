// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.LeaderElection;

/// <summary>
/// Validates <see cref="LeaderElectionOptions"/> cross-property constraints.
/// </summary>
/// <remarks>
/// Ensures:
/// <list type="bullet">
/// <item><see cref="LeaderElectionOptions.RenewInterval"/> is less than <see cref="LeaderElectionOptions.LeaseDuration"/>.</item>
/// <item><see cref="LeaderElectionOptions.GracePeriod"/> is less than <see cref="LeaderElectionOptions.LeaseDuration"/>.</item>
/// <item>
/// The self-demotion deadline (<see cref="LeaderElectionOptions.RenewInterval"/> +
/// <see cref="LeaderElectionOptions.GracePeriod"/> + a clock-skew margin) is strictly less than
/// <see cref="LeaderElectionOptions.LeaseDuration"/>. A renewal loop waits one
/// <see cref="LeaderElectionOptions.RenewInterval"/> and then self-demotes only after the grace
/// period has elapsed, so the effective self-demotion happens roughly
/// <c>RenewInterval + GracePeriod</c> after the last successful renewal. If the lease key expires
/// at <see cref="LeaderElectionOptions.LeaseDuration"/> <em>before</em> the holder self-demotes,
/// another node can acquire the lease while this node still believes it is the leader — a guaranteed
/// split-brain overlap window. Enforcing the cross-property sum closes that window.
/// </item>
/// </list>
/// </remarks>
public sealed class LeaderElectionOptionsValidator : IValidateOptions<LeaderElectionOptions>
{
	/// <summary>
	/// The clock-skew safety margin added to the self-demotion deadline so the holder relinquishes
	/// strictly before lease expiry even under modest clock drift between nodes.
	/// </summary>
	private static readonly TimeSpan ClockSkewMargin = TimeSpan.FromSeconds(1);

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, LeaderElectionOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.RenewInterval >= options.LeaseDuration)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(LeaderElectionOptions.RenewInterval)} ({options.RenewInterval}) must be less than " +
				$"{nameof(LeaderElectionOptions.LeaseDuration)} ({options.LeaseDuration}).");
		}

		if (options.GracePeriod >= options.LeaseDuration)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(LeaderElectionOptions.GracePeriod)} ({options.GracePeriod}) must be less than " +
				$"{nameof(LeaderElectionOptions.LeaseDuration)} ({options.LeaseDuration}).");
		}

		// Cross-property rule: the self-demotion deadline must fall strictly before lease expiry,
		// otherwise a validation-passing config still guarantees a split-brain overlap window.
		var selfDemotionDeadline = options.RenewInterval + options.GracePeriod + ClockSkewMargin;
		if (selfDemotionDeadline >= options.LeaseDuration)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(LeaderElectionOptions.RenewInterval)} ({options.RenewInterval}) + " +
				$"{nameof(LeaderElectionOptions.GracePeriod)} ({options.GracePeriod}) + clock-skew margin " +
				$"({ClockSkewMargin}) must be less than {nameof(LeaderElectionOptions.LeaseDuration)} " +
				$"({options.LeaseDuration}); otherwise the self-demotion deadline falls at or after lease " +
				$"expiry, guaranteeing a split-brain overlap window where another node can acquire the lease " +
				$"before this node relinquishes leadership.");
		}

		return ValidateOptionsResult.Success;
	}
}
