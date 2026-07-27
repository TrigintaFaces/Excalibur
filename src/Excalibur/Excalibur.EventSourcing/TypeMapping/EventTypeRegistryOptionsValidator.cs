// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.TypeMapping;

/// <summary>
/// Validates <see cref="EventTypeRegistryOptions"/> at startup so a malformed alias or type map fails fast
/// instead of surfacing as a deep runtime error during event type resolution.
/// </summary>
internal sealed class EventTypeRegistryOptionsValidator : IValidateOptions<EventTypeRegistryOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, EventTypeRegistryOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.Aliases is null)
		{
			return ValidateOptionsResult.Fail($"{nameof(EventTypeRegistryOptions.Aliases)} must not be null.");
		}

		if (options.TypeMappings is null)
		{
			return ValidateOptionsResult.Fail($"{nameof(EventTypeRegistryOptions.TypeMappings)} must not be null.");
		}

		foreach (var key in options.Aliases.Keys)
		{
			if (string.IsNullOrWhiteSpace(key))
			{
				return ValidateOptionsResult.Fail(
					$"{nameof(EventTypeRegistryOptions.Aliases)} must not contain empty or whitespace keys.");
			}
		}

		foreach (var key in options.TypeMappings.Keys)
		{
			if (string.IsNullOrWhiteSpace(key))
			{
				return ValidateOptionsResult.Fail(
					$"{nameof(EventTypeRegistryOptions.TypeMappings)} must not contain empty or whitespace keys.");
			}
		}

		return ValidateOptionsResult.Success;
	}
}
