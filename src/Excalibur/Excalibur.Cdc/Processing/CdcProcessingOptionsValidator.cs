// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Cdc.Processing;

/// <summary>
/// Validates <see cref="CdcProcessingOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
/// <remarks>
/// Performs cross-property constraint checks that cannot be expressed via
/// <see cref="System.ComponentModel.DataAnnotations"/> attributes alone.
/// Sprint 561 S561.44.
/// </remarks>
public sealed class CdcProcessingOptionsValidator : IValidateOptions<CdcProcessingOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, CdcProcessingOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		// PollingInterval must be positive
		if (options.PollingInterval <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(CdcProcessingOptions.PollingInterval)} must be positive (was {options.PollingInterval}).");
		}

		// DrainTimeoutSeconds has [Range(1, int.MaxValue)] via DataAnnotations,
		// but cross-property: DrainTimeout should be greater than PollingInterval
		if (options.DrainTimeout <= options.PollingInterval)
		{
			failures.Add(
				$"{nameof(CdcProcessingOptions.DrainTimeoutSeconds)} ({options.DrainTimeoutSeconds}s) must yield a timeout greater than " +
				$"{nameof(CdcProcessingOptions.PollingInterval)} ({options.PollingInterval}).");
		}

		// UnhealthyThreshold must be positive (also enforced by [Range])
		if (options.UnhealthyThreshold < 1)
		{
			failures.Add($"{nameof(CdcProcessingOptions.UnhealthyThreshold)} must be >= 1 (was {options.UnhealthyThreshold}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
