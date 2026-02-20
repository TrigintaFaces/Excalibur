// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Provides contextual information for encryption operations.
/// </summary>
/// <remarks>
/// Encryption context includes tenant isolation, key selection, and associated authenticated data (AAD) for GCM mode.
/// </remarks>
public sealed record EncryptionContext
{
	/// <summary>
	/// Gets the identifier for the key to use. If null, the provider selects the default active key.
	/// </summary>
	public string? KeyId { get; init; }

	/// <summary>
	/// Gets the specific key version to use. If null, uses the latest version. Required for decryption when multiple key versions exist.
	/// </summary>
	public int? KeyVersion { get; init; }

	/// <summary>
	/// Gets the encryption algorithm to use. If null, uses the provider's default algorithm.
	/// </summary>
	public EncryptionAlgorithm? Algorithm { get; init; }

	/// <summary>
	/// Gets the tenant identifier for multi-tenant isolation. Ensures cryptographic separation between tenants.
	/// </summary>
	public string? TenantId { get; init; }

	/// <summary>
	/// Gets the purpose of this encryption operation (e.g., "pii-field", "message-payload"). Used for audit logging and key selection policies.
	/// </summary>
	public string? Purpose { get; init; }

	/// <summary>
	/// Gets additional authenticated data (AAD) to bind with the ciphertext. AAD is authenticated but not encrypted; tampering is detected
	/// on decryption.
	/// </summary>
	public byte[]? AssociatedData { get; init; }

	/// <summary>
	/// Gets the data classification level for the data being encrypted. Used for policy enforcement and audit.
	/// </summary>
	public DataClassification? Classification { get; init; }

	/// <summary>
	/// Gets a value indicating whether FIPS 140-2 compliance is required for this operation.
	/// </summary>
	public bool RequireFipsCompliance { get; init; }

	/// <summary>
	/// Creates a minimal context with default settings.
	/// </summary>
	public static EncryptionContext Default => new();

	/// <summary>
	/// Creates a context for a specific tenant.
	/// </summary>
	/// <param name="tenantId"> The tenant identifier. </param>
	/// <returns> An encryption context configured for the tenant. </returns>
	public static EncryptionContext ForTenant(string tenantId) => new() { TenantId = tenantId };
}
