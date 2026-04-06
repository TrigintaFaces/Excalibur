// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Sanitization;

/// <summary>
/// AOT-safe validator for <see cref="TelemetrySanitizerOptions"/>.
/// Replaces DataAnnotations validation with explicit checks for [Required] collections.
/// </summary>
internal sealed class TelemetrySanitizerOptionsDataAnnotationsValidator : IValidateOptions<TelemetrySanitizerOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, TelemetrySanitizerOptions options)
	{
		var failures = new List<string>();

		if (options.SensitiveTagNames is null)
		{
			failures.Add($"{nameof(options.SensitiveTagNames)} is required.");
		}

		if (options.SuppressedTagNames is null)
		{
			failures.Add($"{nameof(options.SuppressedTagNames)} is required.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
