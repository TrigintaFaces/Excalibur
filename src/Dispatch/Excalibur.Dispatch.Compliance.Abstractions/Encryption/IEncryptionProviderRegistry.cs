// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Registry for managing multiple encryption providers with primary/legacy support.
/// </summary>
/// <remarks>
/// <para>
/// Enables zero-downtime provider migration by maintaining:
/// <list type="bullet">
/// <item>One primary provider for new encryption operations</item>
/// <item>Zero or more legacy providers for decrypting old data</item>
/// </list>
/// </para>
/// <para>
/// Thread-safe for concurrent access. Implementations must use appropriate synchronization
/// for all mutable operations.
/// </para>
/// </remarks>
public interface IEncryptionProviderRegistry
{
	/// <summary>
	/// Registers an encryption provider with a unique identifier.
	/// </summary>
	/// <param name="providerId">Unique provider identifier (e.g., "azure-kv-prod", "aws-kms-legacy").</param>
	/// <param name="provider">The encryption provider instance.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="providerId"/> or <paramref name="provider"/> is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown if a provider with the same ID is already registered.</exception>
	void Register(string providerId, IEncryptionProvider provider);

	/// <summary>
	/// Gets a provider by its unique identifier.
	/// </summary>
	/// <param name="providerId">The provider identifier.</param>
	/// <returns>The provider instance, or <c>null</c> if not found.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="providerId"/> is null.</exception>
	/// <remarks>
	/// Provider lookup is case-insensitive.
	/// </remarks>
	IEncryptionProvider? GetProvider(string providerId);

	/// <summary>
	/// Gets the primary provider used for new encryption operations.
	/// </summary>
	/// <returns>The primary provider instance.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown if no primary provider is configured. Call <see cref="SetPrimary"/> or configure via DI first.
	/// </exception>
	/// <remarks>
	/// All new encryption operations should use the primary provider. Legacy providers are only
	/// used for decryption of existing data.
	/// </remarks>
	IEncryptionProvider GetPrimary();

	/// <summary>
	/// Gets all legacy providers that can decrypt old data.
	/// </summary>
	/// <returns>
	/// Read-only list of legacy providers, ordered by preference (most recently added first).
	/// Returns an empty list if no legacy providers are registered.
	/// </returns>
	/// <remarks>
	/// Legacy providers are used during provider migration to decrypt data encrypted with
	/// previous providers. New encryption always uses the primary provider.
	/// </remarks>
	IReadOnlyList<IEncryptionProvider> GetLegacyProviders();

	/// <summary>
	/// Finds the appropriate provider to decrypt the given encrypted data.
	/// </summary>
	/// <param name="encryptedData">The encrypted data envelope containing algorithm and key metadata.</param>
	/// <returns>
	/// The provider capable of decrypting this data, or <c>null</c> if no registered provider
	/// supports the algorithm or has access to the required key.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="encryptedData"/> is null.</exception>
	/// <remarks>
	/// <para>
	/// Routes based on algorithm and key metadata. Tries primary provider first (fast path for 99% of cases),
	/// then iterates through legacy providers.
	/// </para>
	/// <para>
	/// This method checks if a provider supports the algorithm. Actual key availability is verified
	/// during decryption.
	/// </para>
	/// </remarks>
	IEncryptionProvider? FindDecryptionProvider(EncryptedData encryptedData);

	/// <summary>
	/// Gets all registered providers (primary + legacy).
	/// </summary>
	/// <returns>Read-only list of all registered providers.</returns>
	IReadOnlyList<IEncryptionProvider> GetAll();

	/// <summary>
	/// Sets the primary provider for new encryption operations.
	/// </summary>
	/// <param name="providerId">The provider ID to promote to primary.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="providerId"/> is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown if the provider ID is not registered.</exception>
	/// <remarks>
	/// <para>
	/// This operation is atomic and thread-safe. The previous primary provider (if any) is
	/// automatically demoted but remains available for decryption.
	/// </para>
	/// <para>
	/// At runtime, use this for dynamic provider switching. For startup configuration,
	/// prefer the fluent API (<c>UseKeyManagement&lt;T&gt;</c>).
	/// </para>
	/// </remarks>
	void SetPrimary(string providerId);
}
