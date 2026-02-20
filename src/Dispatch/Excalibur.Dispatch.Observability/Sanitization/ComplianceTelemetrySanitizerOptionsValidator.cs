// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.RegularExpressions;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Sanitization;

/// <summary>
/// Validates <see cref="ComplianceTelemetrySanitizerOptions"/> at startup.
/// </summary>
/// <remarks>
/// Ensures that custom regex patterns are valid and that the redacted placeholder is not empty.
/// </remarks>
internal sealed class ComplianceTelemetrySanitizerOptionsValidator
	: IValidateOptions<ComplianceTelemetrySanitizerOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, ComplianceTelemetrySanitizerOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.Enabled && string.IsNullOrWhiteSpace(options.RedactedPlaceholder))
		{
			failures.Add("RedactedPlaceholder must not be empty when compliance sanitization is enabled.");
		}

		for (var i = 0; i < options.CustomPatterns.Count; i++)
		{
			var pattern = options.CustomPatterns[i];
			if (string.IsNullOrWhiteSpace(pattern))
			{
				failures.Add($"CustomPatterns[{i}] must not be null or whitespace.");
				continue;
			}

			try
			{
				_ = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
			}
#pragma warning disable CA1031 // Do not catch general exception types — validation must collect all errors
			catch (Exception ex)
#pragma warning restore CA1031
			{
				failures.Add($"CustomPatterns[{i}] is not a valid regex pattern: {ex.Message}");
			}
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
