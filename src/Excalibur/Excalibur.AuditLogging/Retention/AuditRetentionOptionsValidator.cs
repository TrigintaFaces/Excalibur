// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.AuditLogging.Retention;

/// <summary>
/// Validates <see cref="AuditRetentionOptions"/> at startup. Reflection-free (AOT-safe).
/// </summary>
internal sealed class AuditRetentionOptionsValidator : IValidateOptions<AuditRetentionOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, AuditRetentionOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.RetentionPeriod <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(AuditRetentionOptions.RetentionPeriod)} must be greater than zero.");
		}

		if (options.CleanupInterval <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(AuditRetentionOptions.CleanupInterval)} must be greater than zero.");
		}

		if (options.BatchSize < 1)
		{
			failures.Add($"{nameof(AuditRetentionOptions.BatchSize)} must be greater than zero.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
