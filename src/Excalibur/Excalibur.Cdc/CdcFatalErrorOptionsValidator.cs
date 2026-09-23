// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Options;

namespace Excalibur.Cdc;

/// <summary>
/// Validates <see cref="CdcFatalErrorOptions{TEvent}"/> at startup.
/// </summary>
/// <typeparam name="TEvent">The provider-specific change-event type.</typeparam>
internal sealed class CdcFatalErrorOptionsValidator<TEvent> : IValidateOptions<CdcFatalErrorOptions<TEvent>>
	where TEvent : class
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, CdcFatalErrorOptions<TEvent> options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.MaxConsecutiveTransientFailures is < 1)
		{
			failures.Add(
				$"{nameof(CdcFatalErrorOptions<>.MaxConsecutiveTransientFailures)} must be at least 1 when set " +
				$"(was {options.MaxConsecutiveTransientFailures}); leave it unset to retry without a limit.");
		}

		if (options.MaxReconnectDelay <= TimeSpan.Zero)
		{
			failures.Add(
				$"{nameof(CdcFatalErrorOptions<>.MaxReconnectDelay)} must be greater than zero (was {options.MaxReconnectDelay}).");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
