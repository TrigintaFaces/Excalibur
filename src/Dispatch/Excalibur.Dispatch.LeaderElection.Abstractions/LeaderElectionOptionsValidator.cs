// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.LeaderElection;

/// <summary>
/// Validates <see cref="LeaderElectionOptions"/> cross-property constraints.
/// </summary>
/// <remarks>
/// Ensures:
/// <list type="bullet">
/// <item>All four timing values are positive.</item>
/// <item><see cref="LeaderElectionOptions.RenewInterval"/> is less than <see cref="LeaderElectionOptions.LeaseDuration"/>.</item>
/// <item><see cref="LeaderElectionOptions.GracePeriod"/> is less than <see cref="LeaderElectionOptions.LeaseDuration"/>.</item>
/// <item>
/// The configured timing budget (<see cref="LeaderElectionOptions.RenewInterval"/> +
/// <see cref="LeaderElectionOptions.GracePeriod"/> + a clock-skew margin) is strictly less than
/// <see cref="LeaderElectionOptions.LeaseDuration"/>. This validates configuration headroom only;
/// it does not prove a runtime deadline, bound a provider call, or establish substrate ownership.
/// Providers must enforce their own renewal/verification deadlines and actual backend lease rules.
/// </item>
/// </list>
/// </remarks>
public sealed class LeaderElectionOptionsValidator : IValidateOptions<LeaderElectionOptions>
{
	/// <summary>
	/// Headroom reserved by the configuration check. This constant is not a measured upper bound on
	/// clock skew, scheduling pauses or network round-trip time, and does not bound provider calls.
	/// </summary>
	private static readonly TimeSpan ClockSkewMargin = TimeSpan.FromSeconds(1);

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, LeaderElectionOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.LeaseDuration <= TimeSpan.Zero)
		{
			return ValidateOptionsResult.Fail($"{nameof(LeaderElectionOptions.LeaseDuration)} must be positive.");
		}
		if (options.RenewInterval <= TimeSpan.Zero)
		{
			return ValidateOptionsResult.Fail($"{nameof(LeaderElectionOptions.RenewInterval)} must be positive.");
		}
		if (options.RetryInterval <= TimeSpan.Zero)
		{
			return ValidateOptionsResult.Fail($"{nameof(LeaderElectionOptions.RetryInterval)} must be positive.");
		}
		if (options.GracePeriod <= TimeSpan.Zero)
		{
			return ValidateOptionsResult.Fail($"{nameof(LeaderElectionOptions.GracePeriod)} must be positive.");
		}

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

		// Configuration headroom is necessary but does not establish provider deadline enforcement.
		// Subtraction preserves the strict sum relation without overflowing TimeSpan for extreme input.
		var remainingLease = options.LeaseDuration - options.RenewInterval;
		if (options.GracePeriod >= remainingLease || ClockSkewMargin >= remainingLease - options.GracePeriod)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(LeaderElectionOptions.RenewInterval)} ({options.RenewInterval}) + " +
				$"{nameof(LeaderElectionOptions.GracePeriod)} ({options.GracePeriod}) + clock-skew margin " +
				$"({ClockSkewMargin}) must be less than {nameof(LeaderElectionOptions.LeaseDuration)} " +
				$"({options.LeaseDuration}); otherwise the configured renewal timing leaves no headroom " +
				$"before lease expiry. Runtime deadline enforcement and ownership are provider-specific.");
		}

		return ValidateOptionsResult.Success;
	}
}
