// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Options;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Validates <see cref="BaggagePropagationOptions"/> at startup so a cap that could never admit an entry
/// fails fast rather than silently discarding baggage on every message.
/// </summary>
/// <remarks>
/// A non-positive cap is the dangerous misconfiguration here, and it is dangerous in the quiet direction: it
/// drops every entry while the configuration reads as though propagation were enabled. That failure is
/// indistinguishable at runtime from an allowlist nobody filled in, so it is refused at startup instead.
/// </remarks>
internal sealed class BaggagePropagationOptionsValidator : IValidateOptions<BaggagePropagationOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, BaggagePropagationOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail($"{nameof(BaggagePropagationOptions)} must not be null.");
		}

		var failures = new List<string>();

		if (options.MaxEntries <= 0)
		{
			failures.Add(
				$"{nameof(BaggagePropagationOptions.MaxEntries)} must be greater than zero; {options.MaxEntries} "
				+ "would drop every entry while the allowlist still reads as though keys were permitted. To "
				+ "copy nothing, leave AllowedKeys empty — that is already the default.");
		}

		if (options.MaxValueLength <= 0)
		{
			failures.Add(
				$"{nameof(BaggagePropagationOptions.MaxValueLength)} must be greater than zero; "
				+ $"{options.MaxValueLength} would drop every value, including the ones you allowed by name.");
		}

		if (options.MaxTotalLength <= 0)
		{
			failures.Add(
				$"{nameof(BaggagePropagationOptions.MaxTotalLength)} must be greater than zero; "
				+ $"{options.MaxTotalLength} would drop every entry regardless of the other caps.");
		}

		if (options.AllowedKeys.Any(string.IsNullOrWhiteSpace))
		{
			failures.Add(
				$"{nameof(BaggagePropagationOptions.AllowedKeys)} must not contain a blank key. A blank entry "
				+ "matches no baggage key and is almost always a configuration binding that lost its value.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
