// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.AuditLogging;

/// <summary>
/// Validates <see cref="AuditIntegrityOptions"/> at startup (<c>ValidateOnStart</c>), failing fast on a
/// malformed key identifier rather than emitting unverifiable integrity tags at runtime.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="AuditIntegrityOptions.KeyId"/> is embedded verbatim in each record's colon-delimited
/// integrity tag so older records remain verifiable across key rotation; a missing or colon-bearing
/// <c>KeyId</c> would corrupt that tag format. It is therefore validated here.
/// </para>
/// <para>
/// <see cref="AuditIntegrityOptions.SigningKey"/> is intentionally <b>not</b> required: a null key is a
/// valid state (the default provider fails closed when integrity is actually used), so requiring it at
/// startup would break deployments that do not enable integrity. When a key <i>is</i> supplied, however,
/// it must be at least <see cref="MinimumSigningKeyLengthBytes"/> bytes: a key weaker than HMAC-SHA256's
/// key strength is a weak-key security defect that is worse than no key (which fails closed), so it is
/// rejected at startup rather than silently producing low-strength integrity tags.
/// </para>
/// </remarks>
internal sealed class AuditIntegrityOptionsValidator : IValidateOptions<AuditIntegrityOptions>
{
	/// <summary>
	/// The minimum acceptable length, in bytes, of a non-null <see cref="AuditIntegrityOptions.SigningKey"/>.
	/// 32 bytes (256 bits) matches the output and recommended key strength of HMAC-SHA256.
	/// </summary>
	internal const int MinimumSigningKeyLengthBytes = 32;

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, AuditIntegrityOptions options)
	{
		System.ArgumentNullException.ThrowIfNull(options);

		if (string.IsNullOrWhiteSpace(options.KeyId))
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(AuditIntegrityOptions)}.{nameof(AuditIntegrityOptions.KeyId)} must be a non-empty, " +
				"non-whitespace identifier (it is embedded in each record's integrity tag).");
		}

		if (options.KeyId.Contains(':', System.StringComparison.Ordinal))
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(AuditIntegrityOptions)}.{nameof(AuditIntegrityOptions.KeyId)} must not contain ':' — " +
				"the character is reserved as the integrity-tag field separator.");
		}

		if (options.SigningKey is { Length: var keyLength } && keyLength < MinimumSigningKeyLengthBytes)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(AuditIntegrityOptions)}.{nameof(AuditIntegrityOptions.SigningKey)}, when supplied, must be " +
				$"at least {MinimumSigningKeyLengthBytes} bytes (256-bit, HMAC-SHA256 strength); a shorter key is a " +
				$"weak-key defect. It was {keyLength} byte(s). Leave it null to fail closed instead of using a weak key.");
		}

		return ValidateOptionsResult.Success;
	}
}
