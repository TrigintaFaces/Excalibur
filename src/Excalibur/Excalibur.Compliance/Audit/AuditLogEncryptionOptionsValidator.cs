// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance;

/// <summary>Validates <see cref="AuditLogEncryptionOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class AuditLogEncryptionOptionsValidator : IValidateOptions<AuditLogEncryptionOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, AuditLogEncryptionOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (!Enum.IsDefined(options.EncryptionAlgorithm))
		{
			failures.Add($"{nameof(AuditLogEncryptionOptions.EncryptionAlgorithm)} must be a defined algorithm.");
		}

		if (options.EncryptFields is null)
		{
			failures.Add($"{nameof(AuditLogEncryptionOptions.EncryptFields)} must not be null.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
