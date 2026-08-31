// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Retention;

/// <summary>Validates <see cref="OutboxRetentionOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class OutboxRetentionOptionsValidator : IValidateOptions<OutboxRetentionOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, OutboxRetentionOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.RetentionDays < 0)
		{
			failures.Add($"{nameof(OutboxRetentionOptions.RetentionDays)} must be zero or greater.");
		}

		if (options.BatchSize < 1)
		{
			failures.Add($"{nameof(OutboxRetentionOptions.BatchSize)} must be greater than zero.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
