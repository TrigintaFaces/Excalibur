// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Middleware.Timeout;

/// <summary>
/// Validates <see cref="HandlerTimeoutOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class HandlerTimeoutOptionsValidator : IValidateOptions<HandlerTimeoutOptions>
{
	private static readonly TimeSpan MinTimeout = TimeSpan.FromMilliseconds(1);
	private static readonly TimeSpan MaxTimeout = TimeSpan.FromHours(1);

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, HandlerTimeoutOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.DefaultTimeout < MinTimeout || options.DefaultTimeout > MaxTimeout)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(HandlerTimeoutOptions.DefaultTimeout)} must be between {MinTimeout} and {MaxTimeout} (was {options.DefaultTimeout}).");
		}

		return ValidateOptionsResult.Success;
	}
}
