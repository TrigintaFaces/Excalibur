// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Erasure;

/// <summary>Validates <see cref="ErasureSchedulerOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class ErasureSchedulerOptionsValidator : IValidateOptions<ErasureSchedulerOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, ErasureSchedulerOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.PollingInterval <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(ErasureSchedulerOptions.PollingInterval)} must be greater than zero.");
		}

		if (options.BatchSize < 1)
		{
			failures.Add($"{nameof(ErasureSchedulerOptions.BatchSize)} must be greater than zero.");
		}

		if (options.MaxRetryAttempts < 0)
		{
			failures.Add($"{nameof(ErasureSchedulerOptions.MaxRetryAttempts)} must not be negative.");
		}

		if (options.RetryDelayBase <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(ErasureSchedulerOptions.RetryDelayBase)} must be greater than zero.");
		}

		if (options.CertificateCleanupInterval <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(ErasureSchedulerOptions.CertificateCleanupInterval)} must be greater than zero.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
