// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Data.IdentityMap;

/// <summary>
/// Validates <see cref="IdentityMapOptions"/> at startup so a misconfigured identity map fails fast
/// instead of surfacing as a deep runtime error on first use.
/// </summary>
internal sealed class IdentityMapOptionsValidator : IValidateOptions<IdentityMapOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, IdentityMapOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.DefaultExternalSystem is not null && string.IsNullOrWhiteSpace(options.DefaultExternalSystem))
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(IdentityMapOptions.DefaultExternalSystem)}, when set, must not be empty or whitespace.");
		}

		return ValidateOptionsResult.Success;
	}
}
