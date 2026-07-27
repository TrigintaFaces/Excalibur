// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance;

/// <summary>Validates <see cref="ConsentOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class ConsentOptionsValidator : IValidateOptions<ConsentOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, ConsentOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return options.DefaultExpirationDays < 1
			? ValidateOptionsResult.Fail($"{nameof(ConsentOptions.DefaultExpirationDays)} must be greater than zero.")
			: ValidateOptionsResult.Success;
	}
}
