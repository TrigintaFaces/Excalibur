// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.NonHumanIdentity;

/// <summary>
/// Manages API key lifecycle: creation, revocation, rotation, and validation.
/// </summary>
/// <remarks>
/// <para>
/// Keys are stored as SHA-256 hashes -- plaintext is never persisted.
/// The plaintext key is returned exactly once at creation/rotation via
/// <see cref="ApiKeyCreationResult"/>.
/// </para>
/// <para>
/// Mandatory expiry is enforced. Rotation is atomic: new key created + old key
/// revoked in a single operation.
/// </para>
/// </remarks>
public interface IApiKeyManager
{
	/// <summary>
	/// Creates a new API key for a principal.
	/// </summary>
	/// <param name="request">The key creation request.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The creation result containing the plaintext key (returned once).</returns>
	Task<ApiKeyCreationResult> CreateKeyAsync(ApiKeyRequest request, CancellationToken cancellationToken);

	/// <summary>
	/// Revokes an existing API key.
	/// </summary>
	/// <param name="keyId">The key identifier to revoke.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task RevokeKeyAsync(string keyId, CancellationToken cancellationToken);

	/// <summary>
	/// Validates an API key and returns its metadata if valid.
	/// </summary>
	/// <param name="apiKey">The plaintext API key to validate.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The validation result.</returns>
	Task<ApiKeyValidationResult> ValidateKeyAsync(string apiKey, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves all active (non-revoked) keys for a principal.
	/// </summary>
	/// <param name="principalId">The principal identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Metadata for all active keys belonging to the principal.</returns>
	Task<IReadOnlyList<ApiKeyMetadata>> GetKeysByPrincipalAsync(string principalId, CancellationToken cancellationToken);

	/// <summary>
	/// Atomically rotates an API key: revokes the existing key and creates a new one
	/// with the same principal, scopes, and description.
	/// </summary>
	/// <param name="keyId">The identifier of the key to rotate.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The creation result containing the new plaintext key (returned once).</returns>
	/// <exception cref="InvalidOperationException">
	/// The key does not exist or has already been revoked.
	/// </exception>
	Task<ApiKeyCreationResult> RotateKeyAsync(string keyId, CancellationToken cancellationToken);

	/// <summary>
	/// Gets a sub-interface or service from this manager.
	/// </summary>
	/// <param name="serviceType">The type of service to retrieve.</param>
	/// <returns>The service instance, or <see langword="null"/> if not supported.</returns>
	object? GetService(Type serviceType);
}
