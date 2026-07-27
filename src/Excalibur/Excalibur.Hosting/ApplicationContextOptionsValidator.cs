// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain;

using Microsoft.Extensions.Options;

namespace Excalibur.Hosting;

/// <summary>Validates <see cref="ApplicationContextOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class ApplicationContextOptionsValidator : IValidateOptions<ApplicationContextOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, ApplicationContextOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.ApplicationName))
		{
			failures.Add($"{nameof(ApplicationContextOptions.ApplicationName)} must be a non-empty application name.");
		}

		if (string.IsNullOrWhiteSpace(options.ApplicationSystemName))
		{
			failures.Add($"{nameof(ApplicationContextOptions.ApplicationSystemName)} must be a non-empty system name.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
