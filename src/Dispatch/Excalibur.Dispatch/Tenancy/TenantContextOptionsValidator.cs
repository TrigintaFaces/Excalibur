// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch;

/// <summary>
/// Validates <see cref="TenantContextOptions"/> at startup (fail-fast).
/// </summary>
internal sealed class TenantContextOptionsValidator : IValidateOptions<TenantContextOptions>
{
	public ValidateOptionsResult Validate(string? name, TenantContextOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.DefaultTenantId is not null && string.IsNullOrWhiteSpace(options.DefaultTenantId))
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(TenantContextOptions.DefaultTenantId)}, when set, must not be empty or whitespace.");
		}

		// A required tenant with no default means every operation must supply its own tenant; that is a valid
		// (strict) configuration, so it is allowed. The absence of a default is only a problem at resolve time,
		// which the resolver/scope enforces, not here.
		return ValidateOptionsResult.Success;
	}
}
