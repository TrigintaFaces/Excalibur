// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Security;

/// <summary>
/// Validates <see cref="MaskingTelemetrySanitizerOptions"/> at startup so a set-but-empty pepper is a
/// fail-fast configuration error rather than a footgun that presents as keyed while providing no keyed
/// protection.
/// </summary>
/// <remarks>
/// A <see langword="null"/> pepper stays valid: it is the documented zero-config, honestly-weak default
/// (unkeyed SHA-256 fingerprint). Only an empty (zero-length) pepper is rejected. This is a startup-only
/// check; it does not affect the fail-open runtime contract (an unset pepper never throws at runtime).
/// </remarks>
internal sealed class MaskingTelemetrySanitizerOptionsValidator : IValidateOptions<MaskingTelemetrySanitizerOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, MaskingTelemetrySanitizerOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return options.Pepper is { Length: 0 }
			? ValidateOptionsResult.Fail(
				$"{nameof(MaskingTelemetrySanitizerOptions)}.{nameof(MaskingTelemetrySanitizerOptions.Pepper)} " +
				"is set but empty. Provide a high-entropy secret from your secret manager / KMS, or leave it " +
				"unset (null) to use the documented unkeyed fingerprint. An empty pepper is rejected because it " +
				"presents as keyed while providing no keyed protection.")
			: ValidateOptionsResult.Success;
	}
}
