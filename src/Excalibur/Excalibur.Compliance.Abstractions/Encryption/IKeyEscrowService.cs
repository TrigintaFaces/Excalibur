// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Compliance;

/// <summary>
/// Provides key escrow operations for secure key backup and recovery.
/// </summary>
/// <remarks>
/// <para>
/// This service manages user/tenant key escrow (NOT master keys).
/// Key escrow enables disaster recovery while maintaining security through:
/// </para>
/// <list type="bullet">
///   <item>Encrypted key storage — the escrowed key is encrypted with the master key</item>
///   <item>A Shamir threshold (M-of-N) <b>authorization</b> quorum that gates recovery</item>
///   <item>Time-limited recovery tokens</item>
///   <item>Full audit logging for all operations</item>
/// </list>
/// <para>
/// <b>Recovery guarantee — read carefully.</b> Recovery requires a combined quorum of at least the
/// configured threshold of custodian shares and <b>fails closed</b> below that threshold, or on a
/// tampered or forged share set: no key material is released. This threshold is an <b>authorization</b>
/// control over the recovery workflow. It is <b>not</b> an information-theoretic split of the key itself:
/// the escrowed key is encrypted with the master key, so the master key — not the share quorum — is the
/// cryptographic protection of the stored material. A holder of the master key can therefore decrypt the
/// escrowed key without assembling the quorum. Treat the quorum as fail-closed recovery authorization; do
/// not rely on it as split-knowledge protection of the key.
/// </para>
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
	/// <remarks>
	/// <para>
	/// Recovery is gated by an M-of-N custodian quorum <b>only once recovery tokens have been generated</b>
	/// (see <see cref="GenerateRecoveryTokensAsync"/>). A key that has been backed up but has <b>no recovery
	/// tokens yet</b> is recoverable with the escrow master key alone — there is no custodian quorum to enforce
	/// until custodians are provisioned.
	/// </para>
	/// <para>
	/// Generating recovery tokens binds the key to the quorum (each token batch wraps the key under a
	/// key-encryption key derived from that batch's quorum secret) and removes the master-only copy, after
	/// which the master key alone can no longer recover the key. Provision custodians (generate recovery
	/// tokens) as part of setup to activate quorum-gated recovery.
	/// </para>
	/// </remarks>
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
	/// <param name="token">
	/// A combined recovery token assembled from at least the threshold number of custodian shares (see
	/// <see cref="RecoveryToken.Combine"/>). A single-custodian or below-threshold token fails closed and
	/// recovers nothing.
	/// </param>
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
	/// Generates the per-custodian recovery tokens for the M-of-N recovery-authorization quorum
	/// (genuine Shamir threshold shares of a fresh quorum secret).
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
