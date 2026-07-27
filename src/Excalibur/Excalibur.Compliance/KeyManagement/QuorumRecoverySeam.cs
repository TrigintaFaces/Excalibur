// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Excalibur.Compliance.Encryption;

namespace Excalibur.Compliance.KeyManagement;

/// <summary>
/// Provider-agnostic Shamir M-of-N quorum seam shared by every <see cref="IKeyEscrowService"/>
/// implementation. It owns the single security invariant of key-escrow recovery so the guarantee
/// lives in exactly one place: a genuine quorum secret is split on generation and, on recovery, the
/// reconstructed secret is verified against a <b>server-supplied</b> commitment before any key
/// material is released.
/// </summary>
/// <remarks>
/// <para>
/// The provider supplies persistence only — the stored commitment(s), the encrypted key material, and
/// the decrypt call. This seam owns the crypto/quorum decision and <b>fails closed</b>: a single token
/// (no combined quorum), a below-threshold or tampered share set, or a reconstructed secret whose
/// commitment does not match a server-held value all throw and release <b>zero</b> key material.
/// </para>
/// <para>
/// The commitment MUST be the value the store persisted at generation, never a value carried inside the
/// caller-supplied token: a self-consistent set of fabricated shares embeds its own matching commitment
/// and would otherwise pass. Verifying against the server-held commitment closes that bypass.
/// </para>
/// </remarks>
internal static class QuorumRecoverySeam
{
	// SHA-256 digest length — the size of every quorum-secret commitment.
	private const int CommitmentLength = 32;

	// The high-entropy quorum secret split across custodians. 256 bits keeps the Shamir commitment's
	// offline guess-and-check computationally infeasible (see ShamirSecretSharing security model).
	private const int QuorumSecretBytes = 32;

	// A combined token carries ShareIndex 0 (see RecoveryToken.Combine); an individual custodian share
	// carries its 1-based index. Recovery requires the combined form so the threshold is met at source.
	private const int CombinedTokenShareIndex = 0;

	// Size (bytes) of the presented-share-count header that prefixes the self-describing combined pack.
	private const int CombinedShareCountHeaderBytes = 4;

	// KEK is AES-256 (32 bytes). GCM uses a 96-bit (12-byte) nonce and a 128-bit (16-byte) tag — the
	// recommended, interoperable AES-GCM parameters.
	private const int KekLength = 32;
	private const int GcmNonceLength = 12;
	private const int GcmTagLength = 16;

	private static readonly CompositeFormat InsufficientSharesFormat = CompositeFormat.Parse(
		"At least {0} valid share(s) are required to recover the escrowed key, " +
		"but the presented quorum ({1}) is insufficient, tampered, or does not match the escrow's commitment.");

	/// <summary>
	/// Generates a genuine Shamir quorum: a fresh CSPRNG secret split into <paramref name="custodianCount"/>
	/// shares recoverable only with <paramref name="threshold"/> of them, plus the SHA-256 commitment the
	/// caller MUST persist server-side for recovery to verify against.
	/// </summary>
	/// <param name="custodianCount">Total number of shares to produce (one per custodian).</param>
	/// <param name="threshold">Minimum number of shares required to reconstruct the secret.</param>
	/// <returns>The genuine per-custodian shares and the server-side commitment to persist.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when the parameters violate the Shamir constraints (threshold or count below 2,
	/// threshold above count, count above 255).
	/// </exception>
	public static QuorumGenerationResult GenerateQuorumShares(int custodianCount, int threshold)
	{
		var full = GenerateQuorumSharesWithSecret(custodianCount, threshold);

		// Level-1 callers do not bind the secret into key material, so it never outlives generation here.
		CryptographicOperations.ZeroMemory(full.Secret);
		return new QuorumGenerationResult(full.Shares, full.SecretCommitment);
	}

	/// <summary>
	/// Like <see cref="GenerateQuorumShares"/>, but also returns the freshly-generated quorum <b>secret</b> so a
	/// Level-2 (envelope) caller can derive <c>KEK = HKDF(secret)</c> and wrap the escrowed key under it before
	/// persisting. <b>Ownership of the secret transfers to the caller, which MUST zero it</b> immediately after
	/// wrapping. Use <see cref="GenerateQuorumShares"/> if you do not bind the secret into key material.
	/// </summary>
	/// <param name="custodianCount">Total number of shares to produce (one per custodian).</param>
	/// <param name="threshold">Minimum number of shares required to reconstruct the secret.</param>
	/// <returns>The genuine per-custodian shares, the server-side commitment to persist, and the secret (caller-zeroed).</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when the parameters violate the Shamir constraints.</exception>
	public static QuorumEnvelopeGenerationResult GenerateQuorumSharesWithSecret(int custodianCount, int threshold)
	{
		// CSPRNG secret — never Guid/System.Random. This is the value the shares reconstruct to and the
		// value the commitment commits to.
		var secret = RandomNumberGenerator.GetBytes(QuorumSecretBytes);

		// Server-side commitment: SHA-256(secret). Persisted by the provider (store), NOT embedded in any
		// token — so fabricated self-consistent shares cannot supply their own matching commitment.
		var commitment = SHA256.HashData(secret);

		// Real Shamir split — the fabricated-independent-share path is gone. On a parameter violation this
		// throws; the secret is a local that the GC reclaims (no partial state persisted).
		var shares = ShamirSecretSharing.Split(secret, custodianCount, threshold);

		return new QuorumEnvelopeGenerationResult(shares, commitment, secret);
	}

	/// <summary>
	/// Reconstructs and verifies the quorum secret, failing closed unless a genuine combined quorum of at
	/// least the threshold reconstructs to a secret whose SHA-256 commitment matches one the store persisted.
	/// </summary>
	/// <param name="token">The combined recovery token (produced by <see cref="RecoveryToken.Combine"/>).</param>
	/// <param name="keyId">The escrowed key identifier, for error attribution.</param>
	/// <param name="storedCommitments">
	/// The server-held SHA-256 commitment(s) for the escrow's token batch(es). A reconstructed secret must
	/// match one of these; an empty or non-matching set fails closed.
	/// </param>
	/// <returns>
	/// The verified quorum secret. Ownership transfers to the caller, which MUST zero it when done. Level-1
	/// recovery uses it only to gate release and zeroes it immediately; it is returned (not buried) so a
	/// future KEK-split (Level-2) can bind it into key unwrap without changing this seam.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="token"/> is null.</exception>
	/// <exception cref="KeyEscrowException">
	/// Thrown (with <see cref="KeyEscrowErrorCode.InsufficientShares"/>) when the token is not a combined
	/// quorum, the shares are below threshold or tampered, or the reconstructed secret matches no server
	/// commitment. No key material is derived or returned on any failure path.
	/// </exception>
	public static byte[] RecoverAndVerifyQuorumSecret(
		RecoveryToken token,
		string keyId,
		IReadOnlyCollection<byte[]> storedCommitments) =>
		RecoverAndVerifyQuorumSecretForBatch(token, keyId, storedCommitments).Secret;

	/// <summary>
	/// Reconstructs and verifies the quorum secret like <see cref="RecoverAndVerifyQuorumSecret"/>, but also
	/// returns the <b>server-side commitment the reconstruction matched</b>. Level-2 (KEK-envelope) recovery
	/// needs this to select the correct per-batch key wrap: with multi-recipient escrow each token batch splits
	/// a distinct secret S_i, so the matched commitment identifies which batch — and thus which
	/// <c>KEK_i = HKDF(S_i)</c> wrap — to unwrap.
	/// </summary>
	/// <param name="token">The combined recovery token (produced by <see cref="RecoveryToken.Combine"/>).</param>
	/// <param name="keyId">The escrowed key identifier, for error attribution.</param>
	/// <param name="storedCommitments">The server-held SHA-256 commitment(s) for the escrow's token batch(es).</param>
	/// <returns>The verified quorum secret (caller MUST zero it) and the exact server commitment it matched.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="token"/> is null.</exception>
	/// <exception cref="KeyEscrowException">
	/// Thrown when the token is not a combined quorum, the shares are below threshold or tampered, or the
	/// reconstructed secret matches no server commitment. No key material is derived or returned on failure.
	/// </exception>
	public static QuorumRecoveryResult RecoverAndVerifyQuorumSecretForBatch(
		RecoveryToken token,
		string keyId,
		IReadOnlyCollection<byte[]> storedCommitments)
	{
		ArgumentNullException.ThrowIfNull(token);
		ArgumentNullException.ThrowIfNull(storedCommitments);

		// Combined-quorum requirement: a single custodian token (ShareIndex != 0) can NEVER recover — the
		// threshold is enforced at the source by RecoveryToken.Combine, so a lone share fails closed here.
		if (token.ShareIndex != CombinedTokenShareIndex)
		{
			throw InsufficientShares(keyId, token.Threshold, 1);
		}

		var shares = ExtractSharesFromCombinedToken(token);

		// Reconstruct() fails closed on sub-threshold (FR-E6) and on its embedded commitment (FR-E7). That
		// embedded commitment is CALLER-supplied, so it is NOT sufficient on its own — we additionally verify
		// against the SERVER-side commitment below.
		byte[] reconstructedSecret;
		try
		{
			reconstructedSecret = ShamirSecretSharing.Reconstruct(shares);
		}
		catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
		{
			throw InsufficientShares(keyId, token.Threshold, shares.Length, ex);
		}

		// Verify the reconstructed secret against a value the SERVER persisted — closes the fabricated-share
		// bypass. Constant-time compare; the reconstructed secret is zeroed on every failure path so it never
		// outlives a rejected recovery.
		var actualCommitment = SHA256.HashData(reconstructedSecret);

		byte[]? matchedCommitment = null;
		foreach (var stored in storedCommitments)
		{
			if (stored is { Length: CommitmentLength } && CryptographicOperations.FixedTimeEquals(stored, actualCommitment))
			{
				matchedCommitment = stored;
				break;
			}
		}

		if (matchedCommitment is null)
		{
			CryptographicOperations.ZeroMemory(reconstructedSecret);
			throw InsufficientShares(keyId, token.Threshold, shares.Length);
		}

		// Verified. Ownership of the secret transfers to the caller (which zeroes it); the matched commitment
		// identifies the batch whose KEK wrap the caller must unwrap (Level-2 envelope).
		return new QuorumRecoveryResult(reconstructedSecret, matchedCommitment);
	}

	/// <summary>
	/// Derives a 256-bit key-encryption key (KEK) from a reconstructed quorum secret using HKDF-SHA256
	/// (RFC 5869). The quorum secret is high-entropy CSPRNG/Shamir output, so an extract-and-expand KDF is
	/// correct here — never a password KDF (Argon2/PBKDF2). Building ON <see cref="HKDF"/> per the
	/// Microsoft-first standard (no hand-rolled key mixing).
	/// </summary>
	/// <param name="quorumSecret">The verified quorum secret (ikm).</param>
	/// <param name="salt">Per-escrow random salt.</param>
	/// <param name="info">Context/label binding the KEK to this key and batch (e.g. keyId + batchId).</param>
	/// <returns>A 32-byte KEK. The caller MUST zero it when done.</returns>
	public static byte[] DeriveKek(ReadOnlySpan<byte> quorumSecret, ReadOnlySpan<byte> salt, string info)
	{
		ArgumentException.ThrowIfNullOrEmpty(info);
		var kek = new byte[KekLength];
		// Span-output HKDF overload: keeps ikm/salt as spans (no secret copy) and expands into the KEK buffer.
		HKDF.DeriveKey(HashAlgorithmName.SHA256, quorumSecret, kek, salt, Encoding.UTF8.GetBytes(info));
		return kek;
	}

	/// <summary>
	/// Wraps a data-encryption key (DEK) under a KEK using AES-256-GCM. The GCM authentication tag is returned
	/// and MUST be presented (and is verified) on unwrap, so a tampered wrap fails closed.
	/// </summary>
	/// <param name="dek">The data-encryption key (plaintext) to wrap.</param>
	/// <param name="kek">The 32-byte key-encryption key from <see cref="DeriveKek"/>.</param>
	/// <returns>The wrapped DEK ciphertext, the random GCM nonce (IV), and the authentication tag.</returns>
	public static QuorumKeyWrap WrapDek(ReadOnlySpan<byte> dek, ReadOnlySpan<byte> kek)
	{
		var iv = RandomNumberGenerator.GetBytes(GcmNonceLength);
		var ciphertext = new byte[dek.Length];
		var tag = new byte[GcmTagLength];
		using var gcm = new AesGcm(kek, GcmTagLength);
		gcm.Encrypt(iv, dek, ciphertext, tag);
		return new QuorumKeyWrap(ciphertext, iv, tag);
	}

	/// <summary>
	/// Unwraps a DEK previously wrapped by <see cref="WrapDek"/>. AES-256-GCM verifies the authentication tag;
	/// a wrong KEK (wrong quorum secret) or a tampered wrap throws
	/// <see cref="AuthenticationTagMismatchException"/> and yields <b>zero</b> key material — so master-key
	/// access alone (which can strip only the outer layer, never derive the KEK) cannot recover the DEK.
	/// </summary>
	/// <param name="wrappedDek">The wrapped DEK ciphertext.</param>
	/// <param name="kek">The 32-byte KEK re-derived from the reconstructed quorum secret.</param>
	/// <param name="iv">The GCM nonce stored alongside the wrap.</param>
	/// <param name="tag">The GCM authentication tag stored alongside the wrap.</param>
	/// <returns>The unwrapped DEK. The caller MUST zero it when done.</returns>
	public static byte[] UnwrapDek(
		ReadOnlySpan<byte> wrappedDek,
		ReadOnlySpan<byte> kek,
		ReadOnlySpan<byte> iv,
		ReadOnlySpan<byte> tag)
	{
		var dek = new byte[wrappedDek.Length];
		using var gcm = new AesGcm(kek, GcmTagLength);
		gcm.Decrypt(iv, wrappedDek, tag, dek);
		return dek;
	}

	/// <summary>
	/// Parses the combined recovery token's packed shares. Wire format (produced by
	/// <see cref="RecoveryToken.Combine"/>): <c>[presentedShareCount (4 bytes)] + share (L bytes) × count</c>,
	/// uniform records. The record count is read from the pack header — NOT from
	/// <see cref="RecoveryToken.TotalShares"/> — so a partial quorum (M &lt; TotalShares, e.g. 3-of-5)
	/// reconstructs correctly; each Shamir share carries its own index in its header, so there is no
	/// per-share index prefix. A malformed pack yields no shares → recovery fails closed downstream.
	/// </summary>
	private static byte[][] ExtractSharesFromCombinedToken(RecoveryToken combinedToken)
	{
		var shareData = combinedToken.ShareData;
		var shares = new List<byte[]>();

		if (shareData.Length < CombinedShareCountHeaderBytes)
		{
			return [.. shares];
		}

		var count = BitConverter.ToInt32(shareData, 0);
		var body = shareData.Length - CombinedShareCountHeaderBytes;

		// Guard the record geometry: a positive, evenly-dividing count leaving a positive share length.
		if (count <= 0 || body % count != 0)
		{
			return [.. shares];
		}

		var shareLength = body / count;
		if (shareLength <= 0)
		{
			return [.. shares];
		}

		for (var i = 0; i < count; i++)
		{
			var share = new byte[shareLength];
			Array.Copy(shareData, CombinedShareCountHeaderBytes + (i * shareLength), share, 0, shareLength);
			shares.Add(share);
		}

		return [.. shares];
	}

	private static KeyEscrowException InsufficientShares(string keyId, int threshold, int presented, Exception? inner = null)
	{
		var message = string.Format(CultureInfo.InvariantCulture, InsufficientSharesFormat, threshold, presented);
		return inner is null
			? new KeyEscrowException(message) { KeyId = keyId, ErrorCode = KeyEscrowErrorCode.InsufficientShares }
			: new KeyEscrowException(message, inner) { KeyId = keyId, ErrorCode = KeyEscrowErrorCode.InsufficientShares };
	}
}

/// <summary>
/// The output of <see cref="QuorumRecoverySeam.GenerateQuorumShares"/>: the genuine per-custodian Shamir
/// shares and the SHA-256 commitment the provider MUST persist server-side for recovery to verify against.
/// </summary>
/// <param name="Shares">One genuine Shamir share per custodian (1-based index encoded in each share).</param>
/// <param name="SecretCommitment">SHA-256 of the quorum secret; the store persists this, never a token.</param>
internal readonly record struct QuorumGenerationResult(byte[][] Shares, byte[] SecretCommitment);

/// <summary>
/// The output of <see cref="QuorumRecoverySeam.GenerateQuorumSharesWithSecret"/>: the per-custodian shares,
/// the server-side commitment to persist, and the freshly-generated quorum secret. Ownership of
/// <see cref="Secret"/> transfers to the caller, which MUST zero it after deriving the batch KEK and wrapping.
/// </summary>
/// <param name="Shares">One genuine Shamir share per custodian.</param>
/// <param name="SecretCommitment">SHA-256 of the quorum secret; the store persists this.</param>
/// <param name="Secret">The quorum secret; the caller MUST zero it immediately after wrapping.</param>
internal readonly record struct QuorumEnvelopeGenerationResult(byte[][] Shares, byte[] SecretCommitment, byte[] Secret);

/// <summary>
/// The output of <see cref="QuorumRecoverySeam.RecoverAndVerifyQuorumSecretForBatch"/>: the verified quorum
/// secret and the exact server-side commitment it matched (which identifies the token batch, and thus the
/// per-batch KEK wrap, to unwrap for Level-2 envelope recovery).
/// </summary>
/// <param name="Secret">The verified quorum secret; the caller MUST zero it when done.</param>
/// <param name="MatchedCommitment">The server-held commitment the reconstruction matched (identifies the batch).</param>
internal readonly record struct QuorumRecoveryResult(byte[] Secret, byte[] MatchedCommitment);

/// <summary>
/// An AES-256-GCM key wrap produced by <see cref="QuorumRecoverySeam.WrapDek"/>: the wrapped DEK ciphertext,
/// the random GCM nonce, and the authentication tag. All three are required to unwrap; the tag is verified so
/// a tampered or wrong-KEK wrap fails closed.
/// </summary>
/// <param name="WrappedDek">The DEK ciphertext under the batch KEK.</param>
/// <param name="Iv">The 96-bit GCM nonce.</param>
/// <param name="Tag">The 128-bit GCM authentication tag.</param>
internal readonly record struct QuorumKeyWrap(byte[] WrappedDek, byte[] Iv, byte[] Tag);
