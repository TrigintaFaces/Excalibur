// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.LeaderElection.Consul;

/// <summary>
/// Validates <see cref="ConsulLeaderElectionOptions"/> cross-property constraints.
/// </summary>
/// <remarks>
/// Ensures:
/// <list type="bullet">
/// <item><see cref="ConsulLeaderElectionOptions.LockDelay"/> is less than <see cref="ConsulLeaderElectionOptions.SessionTTL"/>.</item>
/// </list>
/// </remarks>
public sealed class ConsulLeaderElectionOptionsValidator : IValidateOptions<ConsulLeaderElectionOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, ConsulLeaderElectionOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.LockDelay >= options.SessionTTL)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(ConsulLeaderElectionOptions.LockDelay)} ({options.LockDelay}) must be less than " +
				$"{nameof(ConsulLeaderElectionOptions.SessionTTL)} ({options.SessionTTL}).");
		}

		return ValidateOptionsResult.Success;
	}
}
