// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance;

/// <summary>Validates <see cref="DataPortabilityOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class DataPortabilityOptionsValidator : IValidateOptions<DataPortabilityOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, DataPortabilityOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.ExportDirectory))
		{
			failures.Add($"{nameof(DataPortabilityOptions.ExportDirectory)} must be a non-empty path.");
		}

		if (options.MaxExportSize < 1)
		{
			failures.Add($"{nameof(DataPortabilityOptions.MaxExportSize)} must be greater than zero.");
		}

		if (options.RetentionPeriod <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(DataPortabilityOptions.RetentionPeriod)} must be greater than zero.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
