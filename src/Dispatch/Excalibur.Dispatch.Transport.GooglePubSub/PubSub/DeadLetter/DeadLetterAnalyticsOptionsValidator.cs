// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>Validates <see cref="DeadLetterAnalyticsOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class DeadLetterAnalyticsOptionsValidator : IValidateOptions<DeadLetterAnalyticsOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, DeadLetterAnalyticsOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.CollectionInterval <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(DeadLetterAnalyticsOptions.CollectionInterval)} must be greater than zero.");
		}

		if (options.ReportingInterval <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(DeadLetterAnalyticsOptions.ReportingInterval)} must be greater than zero.");
		}

		if (options.BatchSize < 1)
		{
			failures.Add($"{nameof(DeadLetterAnalyticsOptions.BatchSize)} must be greater than zero.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
