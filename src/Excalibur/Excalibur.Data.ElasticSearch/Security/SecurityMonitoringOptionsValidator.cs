// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>Validates <see cref="SecurityMonitoringOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class SecurityMonitoringOptionsValidator : IValidateOptions<SecurityMonitoringOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, SecurityMonitoringOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		// The monitoring loop delays by this value between alert-processing passes: a non-positive interval
		// makes it spin without pause (zero) or throw at the first delay (negative).
		if (options.MonitoringInterval <= TimeSpan.Zero)
		{
			failures.Add(
				$"{nameof(SecurityMonitoringOptions.MonitoringInterval)} must be greater than zero (was {options.MonitoringInterval}).");
		}

		// The threshold is compared as "failed attempts >= threshold": at or below zero the comparison is
		// always true, so every failed sign-in would raise an unauthorized-access threat.
		if (options.FailedLoginThreshold < 1)
		{
			failures.Add(
				$"{nameof(SecurityMonitoringOptions.FailedLoginThreshold)} must be at least 1 (was {options.FailedLoginThreshold}).");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
