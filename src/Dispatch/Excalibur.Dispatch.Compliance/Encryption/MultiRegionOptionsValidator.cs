// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Validates <see cref="MultiRegionOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Performs cross-property constraint checks beyond what <see cref="System.ComponentModel.DataAnnotations"/> can express.
/// </summary>
internal sealed class MultiRegionOptionsValidator : IValidateOptions<MultiRegionOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, MultiRegionOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.RpoTarget <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(MultiRegionOptions.RpoTarget)} must be greater than zero (was {options.RpoTarget}).");
		}

		if (options.RtoTarget <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(MultiRegionOptions.RtoTarget)} must be greater than zero (was {options.RtoTarget}).");
		}

		if (options.OperationTimeout <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(MultiRegionOptions.OperationTimeout)} must be greater than zero (was {options.OperationTimeout}).");
		}

		if (options.Failover.HealthCheckInterval <= TimeSpan.Zero)
		{
			failures.Add(
				$"{nameof(MultiRegionFailoverOptions)}.{nameof(MultiRegionFailoverOptions.HealthCheckInterval)} " +
				$"must be greater than zero (was {options.Failover.HealthCheckInterval}).");
		}

		if (options.Failover.FailoverThreshold < 1)
		{
			failures.Add(
				$"{nameof(MultiRegionFailoverOptions)}.{nameof(MultiRegionFailoverOptions.FailoverThreshold)} " +
				$"must be >= 1 (was {options.Failover.FailoverThreshold}).");
		}

		if (options.Failover.AsyncReplicationInterval <= TimeSpan.Zero)
		{
			failures.Add(
				$"{nameof(MultiRegionFailoverOptions)}.{nameof(MultiRegionFailoverOptions.AsyncReplicationInterval)} " +
				$"must be greater than zero (was {options.Failover.AsyncReplicationInterval}).");
		}

		if (options.Primary is not null && string.IsNullOrWhiteSpace(options.Primary.RegionId))
		{
			failures.Add($"{nameof(MultiRegionOptions.Primary)}.{nameof(RegionConfiguration.RegionId)} must not be empty.");
		}

		if (options.Primary is not null && options.Primary.Endpoint is null)
		{
			failures.Add($"{nameof(MultiRegionOptions.Primary)}.{nameof(RegionConfiguration.Endpoint)} must not be null.");
		}

		if (options.Secondary is not null && string.IsNullOrWhiteSpace(options.Secondary.RegionId))
		{
			failures.Add($"{nameof(MultiRegionOptions.Secondary)}.{nameof(RegionConfiguration.RegionId)} must not be empty.");
		}

		if (options.Secondary is not null && options.Secondary.Endpoint is null)
		{
			failures.Add($"{nameof(MultiRegionOptions.Secondary)}.{nameof(RegionConfiguration.Endpoint)} must not be null.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
