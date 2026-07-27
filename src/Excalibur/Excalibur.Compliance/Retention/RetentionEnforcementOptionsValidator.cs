// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance;

/// <summary>Validates <see cref="RetentionEnforcementOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class RetentionEnforcementOptionsValidator : IValidateOptions<RetentionEnforcementOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, RetentionEnforcementOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.ScanInterval <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(RetentionEnforcementOptions.ScanInterval)} must be greater than zero.");
		}

		if (options.BatchSize < 1)
		{
			failures.Add($"{nameof(RetentionEnforcementOptions.BatchSize)} must be greater than zero.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
