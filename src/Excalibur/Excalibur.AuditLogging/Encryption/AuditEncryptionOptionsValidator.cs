// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.AuditLogging.Encryption;

/// <summary>
/// Validates <see cref="AuditEncryptionOptions"/> at startup. Reflection-free (AOT-safe).
/// </summary>
internal sealed class AuditEncryptionOptionsValidator : IValidateOptions<AuditEncryptionOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, AuditEncryptionOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return string.IsNullOrWhiteSpace(options.EncryptionPurpose)
			? ValidateOptionsResult.Fail(
				$"{nameof(AuditEncryptionOptions.EncryptionPurpose)} must be a non-empty key-derivation purpose label.")
			: ValidateOptionsResult.Success;
	}
}
