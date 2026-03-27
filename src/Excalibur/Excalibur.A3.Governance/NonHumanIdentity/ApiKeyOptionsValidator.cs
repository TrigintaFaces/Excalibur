// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Governance.NonHumanIdentity;

using Microsoft.Extensions.Options;

namespace Excalibur.A3.Governance;

/// <summary>
/// Cross-property validator for <see cref="ApiKeyOptions"/>.
/// </summary>
internal sealed class ApiKeyOptionsValidator : IValidateOptions<ApiKeyOptions>
{
	public ValidateOptionsResult Validate(string? name, ApiKeyOptions options)
	{
		if (options.KeyLengthBytes < 16)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(ApiKeyOptions.KeyLengthBytes)} must be at least 16 bytes for security.");
		}

		if (options.DefaultExpirationDays < 1)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(ApiKeyOptions.DefaultExpirationDays)} must be at least 1 day.");
		}

		return ValidateOptionsResult.Success;
	}
}
