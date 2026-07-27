// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Erasure;

/// <summary>Validates <see cref="LegalHoldExpirationOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class LegalHoldExpirationOptionsValidator : IValidateOptions<LegalHoldExpirationOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, LegalHoldExpirationOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return options.PollingInterval <= TimeSpan.Zero
			? ValidateOptionsResult.Fail($"{nameof(LegalHoldExpirationOptions.PollingInterval)} must be greater than zero.")
			: ValidateOptionsResult.Success;
	}
}
