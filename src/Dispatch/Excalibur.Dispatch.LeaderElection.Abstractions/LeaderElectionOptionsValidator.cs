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
/// </list>
/// </remarks>
public sealed class LeaderElectionOptionsValidator : IValidateOptions<LeaderElectionOptions>
{
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

		return ValidateOptionsResult.Success;
	}
}
