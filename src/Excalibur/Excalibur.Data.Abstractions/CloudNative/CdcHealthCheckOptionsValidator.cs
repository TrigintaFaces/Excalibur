// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Data.CloudNative;

/// <summary>Validates <see cref="CdcHealthCheckOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class CdcHealthCheckOptionsValidator : IValidateOptions<CdcHealthCheckOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, CdcHealthCheckOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.DegradedLagThreshold < 1)
		{
			failures.Add($"{nameof(CdcHealthCheckOptions.DegradedLagThreshold)} must be greater than zero.");
		}

		if (options.UnhealthyLagThreshold < options.DegradedLagThreshold)
		{
			failures.Add(
				$"{nameof(CdcHealthCheckOptions.UnhealthyLagThreshold)} must be greater than or equal to " +
				$"{nameof(CdcHealthCheckOptions.DegradedLagThreshold)}.");
		}

		if (options.UnhealthyInactivityTimeout <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(CdcHealthCheckOptions.UnhealthyInactivityTimeout)} must be greater than zero.");
		}

		if (options.DegradedInactivityTimeout <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(CdcHealthCheckOptions.DegradedInactivityTimeout)} must be greater than zero.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
