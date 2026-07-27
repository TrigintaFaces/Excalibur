// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Options.Middleware;

/// <summary>Validates <see cref="LoggingMiddlewareOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class LoggingMiddlewareOptionsValidator : IValidateOptions<LoggingMiddlewareOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, LoggingMiddlewareOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (!Enum.IsDefined(options.SuccessLevel))
		{
			failures.Add($"{nameof(LoggingMiddlewareOptions.SuccessLevel)} must be a defined log level.");
		}

		if (!Enum.IsDefined(options.FailureLevel))
		{
			failures.Add($"{nameof(LoggingMiddlewareOptions.FailureLevel)} must be a defined log level.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
