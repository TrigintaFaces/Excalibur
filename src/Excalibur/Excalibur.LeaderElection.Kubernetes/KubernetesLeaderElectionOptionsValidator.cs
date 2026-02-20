// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.LeaderElection.Kubernetes;

/// <summary>
/// Validates <see cref="KubernetesLeaderElectionOptions"/> cross-property constraints.
/// </summary>
/// <remarks>
/// Ensures:
/// <list type="bullet">
/// <item><see cref="KubernetesLeaderElectionOptions.RenewIntervalMilliseconds"/> is less than
/// <see cref="KubernetesLeaderElectionOptions.LeaseDurationSeconds"/> (converted to ms).</item>
/// <item><see cref="KubernetesLeaderElectionOptions.GracePeriodSeconds"/> is less than
/// <see cref="KubernetesLeaderElectionOptions.LeaseDurationSeconds"/>.</item>
/// </list>
/// </remarks>
public sealed class KubernetesLeaderElectionOptionsValidator : IValidateOptions<KubernetesLeaderElectionOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, KubernetesLeaderElectionOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var leaseDurationMs = options.LeaseDurationSeconds * 1000;

		if (options.RenewIntervalMilliseconds >= leaseDurationMs)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(KubernetesLeaderElectionOptions.RenewIntervalMilliseconds)} ({options.RenewIntervalMilliseconds}ms) must be less than " +
				$"{nameof(KubernetesLeaderElectionOptions.LeaseDurationSeconds)} ({options.LeaseDurationSeconds}s = {leaseDurationMs}ms).");
		}

		if (options.GracePeriodSeconds >= options.LeaseDurationSeconds)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(KubernetesLeaderElectionOptions.GracePeriodSeconds)} ({options.GracePeriodSeconds}s) must be less than " +
				$"{nameof(KubernetesLeaderElectionOptions.LeaseDurationSeconds)} ({options.LeaseDurationSeconds}s).");
		}

		return ValidateOptionsResult.Success;
	}
}
