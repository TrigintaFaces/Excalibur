// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.Encryption;

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance;

/// <summary>
/// Validates <see cref="AesGcmEncryptionOptions"/> at startup.
/// </summary>
internal sealed class AesGcmEncryptionOptionsValidator : IValidateOptions<AesGcmEncryptionOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, AesGcmEncryptionOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return options.DefaultPurpose is not null && string.IsNullOrWhiteSpace(options.DefaultPurpose)
			? ValidateOptionsResult.Fail($"{nameof(AesGcmEncryptionOptions.DefaultPurpose)} must not be empty or whitespace when set.")
			: ValidateOptionsResult.Success;
	}
}

/// <summary>
/// Validates <see cref="InMemoryKeyManagementOptions"/> at startup.
/// </summary>
internal sealed class InMemoryKeyManagementOptionsValidator : IValidateOptions<InMemoryKeyManagementOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, InMemoryKeyManagementOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return string.IsNullOrWhiteSpace(options.DefaultKeyId)
			? ValidateOptionsResult.Fail($"{nameof(InMemoryKeyManagementOptions.DefaultKeyId)} must not be empty or whitespace.")
			: ValidateOptionsResult.Success;
	}
}

/// <summary>
/// Validates <see cref="RotatingEncryptionOptions"/> at startup.
/// </summary>
internal sealed class RotatingEncryptionOptionsValidator : IValidateOptions<RotatingEncryptionOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, RotatingEncryptionOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return options.MaxKeyAge <= TimeSpan.Zero
			? ValidateOptionsResult.Fail($"{nameof(RotatingEncryptionOptions.MaxKeyAge)} must be greater than zero.")
			: ValidateOptionsResult.Success;
	}
}
