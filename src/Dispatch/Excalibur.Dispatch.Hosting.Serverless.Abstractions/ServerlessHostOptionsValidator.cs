// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Hosting.Serverless;

/// <summary>
/// Validates <see cref="ServerlessHostOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class ServerlessHostOptionsValidator : IValidateOptions<ServerlessHostOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, ServerlessHostOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.MemoryLimitMB.HasValue && options.MemoryLimitMB.Value < 1)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(ServerlessHostOptions.MemoryLimitMB)} must be >= 1 when specified (was {options.MemoryLimitMB}).");
		}

		return ValidateOptionsResult.Success;
	}
}
