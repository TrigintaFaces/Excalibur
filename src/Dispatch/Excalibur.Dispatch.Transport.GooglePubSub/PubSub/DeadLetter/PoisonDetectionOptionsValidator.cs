// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>Validates <see cref="PoisonDetectionOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class PoisonDetectionOptionsValidator : IValidateOptions<PoisonDetectionOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, PoisonDetectionOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.MaxFailuresBeforePoison < 1)
		{
			failures.Add($"{nameof(PoisonDetectionOptions.MaxFailuresBeforePoison)} must be greater than zero.");
		}

		if (options.RapidFailureCount < 1)
		{
			failures.Add($"{nameof(PoisonDetectionOptions.RapidFailureCount)} must be greater than zero.");
		}

		if (options.RapidFailureWindow <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(PoisonDetectionOptions.RapidFailureWindow)} must be greater than zero.");
		}

		if (options.ConsistentExceptionThreshold is < 0.0 or > 1.0)
		{
			failures.Add($"{nameof(PoisonDetectionOptions.ConsistentExceptionThreshold)} must be between 0.0 and 1.0.");
		}

		if (options.TimeoutThreshold is < 0.0 or > 1.0)
		{
			failures.Add($"{nameof(PoisonDetectionOptions.TimeoutThreshold)} must be between 0.0 and 1.0.");
		}

		if (options.LoopDetectionThreshold < 1)
		{
			failures.Add($"{nameof(PoisonDetectionOptions.LoopDetectionThreshold)} must be greater than zero.");
		}

		if (options.HistoryRetentionPeriod <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(PoisonDetectionOptions.HistoryRetentionPeriod)} must be greater than zero.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
