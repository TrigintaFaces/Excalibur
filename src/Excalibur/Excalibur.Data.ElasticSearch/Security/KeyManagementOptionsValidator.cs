// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>Validates <see cref="KeyManagementOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class KeyManagementOptionsValidator : IValidateOptions<KeyManagementOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, KeyManagementOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return options.KeyRotationInterval <= TimeSpan.Zero
			? ValidateOptionsResult.Fail($"{nameof(KeyManagementOptions.KeyRotationInterval)} must be greater than zero.")
			: ValidateOptionsResult.Success;
	}
}
