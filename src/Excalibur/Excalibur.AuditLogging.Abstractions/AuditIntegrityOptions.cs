// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.AuditLogging;

/// <summary>
/// Configures the shared audit-integrity signing key used by every audit sink's
/// <see cref="IAuditIntegrityStrategy"/> (keyed-MAC). One key-config story across sinks — the
/// audit-integrity key is an application-level security secret, not a per-sink concern.
/// </summary>
/// <remarks>
/// The key MUST be sourced from a secret manager / KMS, never hard-coded. When no key is configured and
/// integrity is in use, the default <see cref="IAuditSigningKeyProvider"/> fails closed (it never
/// fabricates or omits the key). A deployment needing per-sink key isolation or a KMS-backed provider
/// registers its own <see cref="IAuditSigningKeyProvider"/> (the default is registered via
/// <c>TryAddSingleton</c>, so a prior registration wins).
/// </remarks>
public sealed class AuditIntegrityOptions
{
	/// <summary>
	/// Gets the secret signing key bytes used to compute and verify the keyed-MAC integrity tag.
	/// </summary>
	/// <value>
	/// The key bytes, sourced from a secret manager / KMS. <see langword="null"/> by default; when null and
	/// integrity is in use, the default provider fails closed.
	/// </value>
	public byte[]? SigningKey { get; init; }

	/// <summary>
	/// Gets the identifier of the current signing key, embedded in each record's integrity tag so keys can
	/// be rotated while older records remain verifiable.
	/// </summary>
	/// <value>The key identifier (must be colon-free). Defaults to "default".</value>
	public string KeyId { get; init; } = "default";
}
