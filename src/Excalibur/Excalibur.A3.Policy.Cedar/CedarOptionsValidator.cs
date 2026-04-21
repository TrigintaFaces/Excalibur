// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.A3.Policy.Cedar;

/// <summary>
/// Validates <see cref="CedarOptions"/> cross-property constraints that cannot be expressed
/// via <see cref="System.ComponentModel.DataAnnotations"/> attributes alone.
/// </summary>
internal sealed class CedarOptionsValidator : IValidateOptions<CedarOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, CedarOptions options)
	{
		if (options.Mode == CedarMode.AwsVerifiedPermissions &&
			string.IsNullOrEmpty(options.PolicyStoreId))
		{
			return ValidateOptionsResult.Fail(
				"PolicyStoreId is required when Mode is AwsVerifiedPermissions.");
		}

		return ValidateOptionsResult.Success;
	}
}
