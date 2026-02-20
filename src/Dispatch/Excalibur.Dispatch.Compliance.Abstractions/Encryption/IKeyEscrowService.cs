// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Provides key escrow operations for secure key backup and recovery.
/// </summary>
/// <remarks>
/// <para>
/// This service manages user/tenant key escrow (NOT master keys).
/// Key escrow enables disaster recovery while maintaining security through:
/// </para>
/// <list type="bullet">
///   <item>Encrypted key storage (keys encrypted with master key)</item>
///   <item>Shamir's Secret Sharing for split-knowledge recovery</item>
///   <item>Time-limited recovery tokens</item>
///   <item>Full audit logging for all operations</item>
/// </list>
/// <para>
/// This service is designed for user-level and tenant-level encryption keys.
/// For master key backup, see <see cref="IMasterKeyBackupService"/>.
/// </para>
/// </remarks>
public interface IKeyEscrowService
{
	/// <summary>
	/// Creates an encrypted backup of a key in escrow storage.
	/// </summary>
	/// <param name="keyId">The unique identifier of the key to backup.</param>
	/// <param name="keyMaterial">The raw key material to backup (will be encrypted).</param>
	/// <param name="options">Configuration options for the escrow operation.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A receipt confirming the escrow operation.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="keyId"/> or <paramref name="keyMaterial"/> is null.</exception>
	/// <exception cref="ArgumentException">Thrown when <paramref name="keyId"/> is empty or whitespace.</exception>
	/// <exception cref="KeyEscrowException">Thrown when the escrow operation fails.</exception>
	Task<EscrowReceipt> BackupKeyAsync(
		string keyId,
		ReadOnlyMemory<byte> keyMaterial,
		EscrowOptions? options,
		CancellationToken cancellationToken);

	/// <summary>
	/// Recovers a key from escrow storage using a valid recovery token.
	/// </summary>
	/// <param name="keyId">The unique identifier of the key to recover.</param>
	/// <param name="token">A valid recovery token (from Shamir's Secret Sharing reconstruction).</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The recovered key material.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="keyId"/> or <paramref name="token"/> is null.</exception>
	/// <exception cref="KeyEscrowException">Thrown when recovery fails (invalid token, expired, or key not found).</exception>
	/// <exception cref="UnauthorizedAccessException">Thrown when the token is invalid or expired.</exception>
	Task<ReadOnlyMemory<byte>> RecoverKeyAsync(
		string keyId,
		RecoveryToken token,
		CancellationToken cancellationToken);

	/// <summary>
	/// Generates recovery tokens using Shamir's Secret Sharing scheme.
	/// </summary>
	/// <param name="keyId">The unique identifier of the escrowed key.</param>
	/// <param name="custodianCount">Total number of custodians (shares to generate). Default is 5.</param>
	/// <param name="threshold">Minimum shares required for recovery. Default is 3.</param>
	/// <param name="expiresIn">Time until tokens expire. Default is 24 hours.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>Array of recovery tokens to distribute to custodians.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="keyId"/> is null.</exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="threshold"/> is greater than <paramref name="custodianCount"/>
	/// or when either value is less than 2.
	/// </exception>
	/// <exception cref="KeyEscrowException">Thrown when token generation fails.</exception>
	Task<RecoveryToken[]> GenerateRecoveryTokensAsync(
		string keyId,
		int custodianCount,
		int threshold,
		TimeSpan? expiresIn,
		CancellationToken cancellationToken);

	/// <summary>
	/// Revokes all escrow data and recovery tokens for a key.
	/// </summary>
	/// <param name="keyId">The unique identifier of the escrowed key.</param>
	/// <param name="reason">The reason for revocation (for audit purposes).</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>True if the escrow was revoked; false if no escrow existed for the key.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="keyId"/> is null.</exception>
	Task<bool> RevokeEscrowAsync(
		string keyId,
		string? reason,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the escrow status for a specific key.
	/// </summary>
	/// <param name="keyId">The unique identifier of the key.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The escrow status, or null if no escrow exists for the key.</returns>
	Task<EscrowStatus?> GetEscrowStatusAsync(
		string keyId,
		CancellationToken cancellationToken);
}
