// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.AuditLogging;

/// <summary>
/// Supplies the secret keys used to compute and verify the keyed-MAC integrity tag on audit records.
/// </summary>
/// <remarks>
/// <para>
/// The signing key MUST be held separately from the audit store (e.g. in a secret manager / KMS or
/// supplied via configuration) — it is never derivable from, or stored alongside, the audit records it
/// protects. Each record embeds the identifier of the key that produced its tag so that keys can be
/// rotated while older records remain verifiable.
/// </para>
/// <para>
/// Key identifiers MUST be drawn from a colon-free character set (for example alphanumeric, <c>-</c>,
/// or a GUID) because the integrity tag is stored as a colon-delimited, self-describing token.
/// </para>
/// </remarks>
public interface IAuditSigningKeyProvider
{
	/// <summary>
	/// Gets the current signing key (and its identifier) used to produce integrity tags for newly
	/// written audit records.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The current key identifier and the secret key bytes.</returns>
	/// <remarks>
	/// Implementations SHALL fail (throw) when no signing key is available rather than returning an
	/// empty or fabricated key — a record cannot be integrity-protected without a real key.
	/// </remarks>
	ValueTask<(string KeyId, byte[] Key)> GetCurrentSigningKeyAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Gets the secret key for the supplied <paramref name="keyId"/> so a stored integrity tag can be
	/// verified, returning <see langword="null"/> when the key is unknown or unavailable.
	/// </summary>
	/// <param name="keyId">The identifier embedded in the record's integrity tag.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The secret key bytes, or <see langword="null"/> when the key cannot be obtained.</returns>
	/// <remarks>
	/// A <see langword="null"/> result causes verification to fail closed (the record is treated as
	/// unverifiable, never as valid).
	/// </remarks>
	ValueTask<byte[]?> GetSigningKeyAsync(string keyId, CancellationToken cancellationToken);
}
