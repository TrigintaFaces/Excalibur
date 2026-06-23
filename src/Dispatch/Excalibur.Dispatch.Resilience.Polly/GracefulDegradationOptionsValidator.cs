// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Validates <see cref="GracefulDegradationOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class GracefulDegradationOptionsValidator : IValidateOptions<GracefulDegradationOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, GracefulDegradationOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.HealthCheckInterval <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(GracefulDegradationOptions.HealthCheckInterval)} must be greater than TimeSpan.Zero (was {options.HealthCheckInterval}).");
		}

		if (options.ErrorRateWindow <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(GracefulDegradationOptions.ErrorRateWindow)} must be greater than TimeSpan.Zero (was {options.ErrorRateWindow}).");
		}

		// NOTE: ErrorRateWindow < HealthCheckInterval is a tuning tradeoff (a recent-error window
		// shorter than the check cadence leaves an inter-check blind spot), not an invalid state — the
		// window is always well-defined as "errors over the last ErrorRateWindow". It is therefore a
		// documented recommendation on the option, not a hard validation failure, so legitimate
		// "infrequent checks + short recent-error window" configurations are not rejected at startup.

		if (options.ErrorRateWindowBuckets < 1)
		{
			failures.Add($"{nameof(GracefulDegradationOptions.ErrorRateWindowBuckets)} must be at least 1 (was {options.ErrorRateWindowBuckets}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
