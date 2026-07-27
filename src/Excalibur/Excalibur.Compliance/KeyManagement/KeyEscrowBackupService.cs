// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Security.Cryptography;

using Excalibur.Compliance.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Compliance.KeyManagement;

/// <summary>
/// Configuration options for key escrow backup operations.
/// </summary>
public sealed class KeyEscrowBackupOptions
{
	/// <summary>
	/// Gets or sets the escrow provider name.
	/// Default: "InMemory".
	/// </summary>
	public string EscrowProvider { get; set; } = "InMemory";

	/// <summary>
	/// Gets or sets the minimum number of shares required for key recovery
	/// (Shamir's Secret Sharing threshold).
	/// Default: 3.
	/// </summary>
	public int SplitThreshold { get; set; } = 3;

	/// <summary>
	/// Gets or sets the total number of shares to generate.
	/// Default: 5.
	/// </summary>
	public int TotalShares { get; set; } = 5;
}

/// <summary>
/// In-memory implementation of <see cref="IKeyEscrowService"/> providing key escrow
/// and backup capabilities with Shamir's Secret Sharing inspired threshold recovery.
/// </summary>
/// <remarks>
/// <para>
/// This implementation stores escrowed keys in memory and is suitable for development
/// and testing. Production deployments should use a durable store-backed implementation.
/// </para>
/// </remarks>
public sealed partial class KeyEscrowBackupService : IKeyEscrowService
{
	// The logical master-key selector used when encrypting escrowed key material. Recovery reconstructs the
	// same context so DecryptAsync resolves the identical key.
	private const string EscrowMasterKeyId = "escrow-master-key";

	private readonly ConcurrentDictionary<string, EscrowEntry> _escrowStore = new(StringComparer.OrdinalIgnoreCase);
	private readonly IEncryptionProvider _encryptionProvider;
	private readonly ILogger<KeyEscrowBackupService> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="KeyEscrowBackupService"/> class.
	/// </summary>
	/// <param name="encryptionProvider">The encryption provider for encrypting escrowed keys.</param>
	/// <param name="logger">The logger.</param>
	public KeyEscrowBackupService(
		IEncryptionProvider encryptionProvider,
		ILogger<KeyEscrowBackupService> logger)
	{
		_encryptionProvider = encryptionProvider ?? throw new ArgumentNullException(nameof(encryptionProvider));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public async Task<EscrowReceipt> BackupKeyAsync(
		string keyId,
		ReadOnlyMemory<byte> keyMaterial,
		EscrowOptions? options,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

		// Transient plaintext copy of the key material required by the encryption
		// provider. Zeroed in the finally so the secret never outlives this call
		// (defense-in-depth), regardless of success or failure.
		byte[]? transientKeyMaterial = null;

		try
		{
			var context = new EncryptionContext { KeyId = EscrowMasterKeyId };
			transientKeyMaterial = keyMaterial.ToArray();
			var encrypted = await _encryptionProvider.EncryptAsync(
				transientKeyMaterial, context, cancellationToken).ConfigureAwait(false);

			var escrowId = Guid.NewGuid().ToString("N");
			var now = DateTimeOffset.UtcNow;

			var entry = new EscrowEntry
			{
				KeyId = keyId,
				EscrowId = escrowId,
				// Store the FULL encrypted envelope (Iv/AuthTag/Algorithm/master-key metadata), not just the
				// ciphertext — recovery must DECRYPT it and return the real key, never raw ciphertext.
				EncryptedKeyMaterial = encrypted,
				EscrowedAt = now,
				ExpiresAt = options?.ExpiresIn is not null ? now.Add(options.ExpiresIn.Value) : null,
				State = EscrowState.Active,
				// Per-batch quorum envelope wraps, populated (and the master-only copy sealed) on the first
				// GenerateRecoveryTokensAsync. Empty until then.
				BatchWraps = []
			};

			_escrowStore[keyId] = entry;

			var receipt = new EscrowReceipt
			{
				KeyId = keyId,
				EscrowId = escrowId,
				EscrowedAt = now,
				ExpiresAt = entry.ExpiresAt,
				KeyHash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(keyMaterial.Span)),
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				MasterKeyVersion = 1
			};

			LogKeyEscrowBackupCompleted(keyId, escrowId);

			return receipt;
		}
		catch (Exception ex)
		{
			LogKeyEscrowOperationFailed(keyId, "backup", ex);
			throw new KeyEscrowException($"Failed to backup key '{keyId}'.", ex);
		}
		finally
		{
			if (transientKeyMaterial is not null)
			{
				CryptographicOperations.ZeroMemory(transientKeyMaterial);
			}
		}
	}

	/// <inheritdoc />
	public async Task<ReadOnlyMemory<byte>> RecoverKeyAsync(
		string keyId,
		RecoveryToken token,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
		ArgumentNullException.ThrowIfNull(token);

		if (!_escrowStore.TryGetValue(keyId, out var entry))
		{
			throw new KeyEscrowException($"No escrow exists for key '{keyId}'.")
			{ KeyId = keyId, ErrorCode = KeyEscrowErrorCode.KeyNotFound };
		}

		if (entry.State != EscrowState.Active)
		{
			throw new KeyEscrowException($"Escrow for key '{keyId}' is in state '{entry.State}' and cannot be recovered.")
			{ KeyId = keyId, EscrowId = entry.EscrowId, ErrorCode = KeyEscrowErrorCode.EscrowExpired };
		}

		if (entry.ExpiresAt.HasValue && DateTimeOffset.UtcNow > entry.ExpiresAt)
		{
			throw new KeyEscrowException($"Escrow for key '{keyId}' has expired.")
			{ KeyId = keyId, EscrowId = entry.EscrowId, ErrorCode = KeyEscrowErrorCode.EscrowExpired };
		}

		if (token.EscrowId != entry.EscrowId)
		{
			throw new KeyEscrowException($"Recovery token does not belong to the active escrow for key '{keyId}'.")
			{ KeyId = keyId, EscrowId = entry.EscrowId, ErrorCode = KeyEscrowErrorCode.InvalidToken };
		}

		// Fail-closed M-of-N quorum: reconstruct + verify against the SERVER-side commitment(s), and learn WHICH
		// batch the quorum matched (each batch splits a distinct secret). Any failure throws with ZERO material.
		var storedCommitments = entry.BatchWraps.Select(static w => w.SecretCommitment).ToList();
		var quorum = QuorumRecoverySeam.RecoverAndVerifyQuorumSecretForBatch(token, keyId, storedCommitments);

		try
		{
			// Select the batch wrap the reconstructed quorum matched. The DEK is bound under KEK = HKDF(S_batch);
			// the master key alone strips only the outer layer and can NEVER derive the KEK (closes the bypass).
			var wrap = entry.BatchWraps.FirstOrDefault(
					w => CryptographicOperations.FixedTimeEquals(w.SecretCommitment, quorum.MatchedCommitment))
				?? throw new KeyEscrowException($"No key wrap exists for the recovered batch of key '{keyId}'.")
				{ KeyId = keyId, EscrowId = entry.EscrowId, ErrorCode = KeyEscrowErrorCode.KeyNotFound };

			// Outer layer: master-decrypt the wrapped inner ciphertext.
			var context = new EncryptionContext { KeyId = EscrowMasterKeyId };
			var innerCiphertext = await _encryptionProvider
				.DecryptAsync(wrap.Outer, context, cancellationToken)
				.ConfigureAwait(false);

			// Inner layer: derive the batch KEK from the reconstructed quorum secret and AES-GCM-unwrap the DEK.
			// A wrong quorum or tampered wrap throws (tag mismatch) = fail closed, zero key material.
			var kek = QuorumRecoverySeam.DeriveKek(quorum.Secret, wrap.KekSalt, BuildKekInfo(keyId, wrap.BatchId));
			try
			{
				var decrypted = QuorumRecoverySeam.UnwrapDek(innerCiphertext, kek, wrap.InnerIv, wrap.InnerAuthTag);
				LogKeyEscrowRecoveryCompleted(keyId);
				return decrypted;
			}
			finally
			{
				CryptographicOperations.ZeroMemory(kek);
			}
		}
		finally
		{
			// Zero the quorum secret only AFTER KEK derivation (moved from the former immediate Level-1 zero).
			CryptographicOperations.ZeroMemory(quorum.Secret);
		}
	}

	/// <inheritdoc />
	public async Task<RecoveryToken[]> GenerateRecoveryTokensAsync(
		string keyId,
		int custodianCount,
		int threshold,
		TimeSpan? expiresIn,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

		if (threshold < 2)
		{
			throw new ArgumentOutOfRangeException(nameof(threshold), "Threshold must be at least 2.");
		}

		if (custodianCount < threshold)
		{
			throw new ArgumentOutOfRangeException(nameof(custodianCount), "Custodian count must be greater than or equal to threshold.");
		}

		if (!_escrowStore.TryGetValue(keyId, out var entry))
		{
			throw new KeyEscrowException($"No escrow exists for key '{keyId}'.")
			{ KeyId = keyId, ErrorCode = KeyEscrowErrorCode.KeyNotFound };
		}

		// Fail closed if already sealed (a batch exists): adding a custodian batch requires an existing quorum's
		// consent (rotation), not supported on this path (PdM Option-A — no lone-holder-adds-a-bypass-batch).
		if (entry.EncryptedKeyMaterial is null || entry.BatchWraps.Count > 0)
		{
			throw AlreadySealed(keyId, entry.EscrowId);
		}

		var expiration = expiresIn ?? TimeSpan.FromHours(24);
		var now = DateTimeOffset.UtcNow;
		var batchId = Guid.NewGuid().ToString("N");

		// Fresh quorum secret S for THIS batch; ownership transfers here (zeroed after wrapping).
		var quorum = QuorumRecoverySeam.GenerateQuorumSharesWithSecret(custodianCount, threshold);
		var batchWrap = await WrapKeyForBatchAsync(keyId, batchId, entry.EncryptedKeyMaterial, quorum, cancellationToken)
			.ConfigureAwait(false);

		// SEAL atomically: drop the master-only copy and record the batch wrap. If a concurrent caller sealed
		// first (BatchWraps non-empty / EncryptedKeyMaterial null), refuse and fail closed — this batch's tokens
		// were never recorded, so they must not be handed out.
		var didSeal = false;
		_ = _escrowStore.AddOrUpdate(
			keyId,
			_ => throw new KeyEscrowException($"No escrow exists for key '{keyId}'.")
			{ KeyId = keyId, ErrorCode = KeyEscrowErrorCode.KeyNotFound },
			(_, current) =>
			{
				if (current.EncryptedKeyMaterial is null || current.BatchWraps.Count > 0)
				{
					return current;
				}

				didSeal = true;
				return current with { EncryptedKeyMaterial = null, BatchWraps = [batchWrap] };
			});

		if (!didSeal)
		{
			throw AlreadySealed(keyId, entry.EscrowId);
		}

		var tokens = new RecoveryToken[custodianCount];
		for (var i = 0; i < custodianCount; i++)
		{
			tokens[i] = new RecoveryToken
			{
				TokenId = Guid.NewGuid().ToString("N"),
				KeyId = keyId,
				EscrowId = entry.EscrowId,
				ShareIndex = i + 1,
				// Genuine Shamir share (index encoded in the share header). Recoverable only when a threshold
				// of these are combined via RecoveryToken.Combine and verified against the stored commitment.
				ShareData = quorum.Shares[i],
				CreatedAt = now,
				ExpiresAt = now.Add(expiration),
				Threshold = threshold,
				TotalShares = custodianCount
			};
		}

		return tokens;
	}

	// The HKDF context/label binding the batch KEK to this key and batch — identical at wrap and unwrap.
	private static string BuildKekInfo(string keyId, string batchId) =>
		$"excalibur:key-escrow:kek:v2|{keyId}|batch:{batchId}";

	private static KeyEscrowException AlreadySealed(string keyId, string escrowId) =>
		new($"Recovery tokens have already been generated for key '{keyId}'. Adding another custodian batch " +
			"requires an existing quorum's consent (rotation), which is not supported on this path.")
		{ KeyId = keyId, EscrowId = escrowId, ErrorCode = KeyEscrowErrorCode.EscrowExpired };

	/// <summary>
	/// Master-decrypts the DEK (the only point it is plaintext), binds it to this batch's quorum KEK
	/// (<c>inner = AES-GCM(DEK, HKDF(S))</c>) then master-encrypts the outer layer, and returns the batch wrap.
	/// The quorum secret and the plaintext DEK are zeroed before returning.
	/// </summary>
	private async Task<BatchWrap> WrapKeyForBatchAsync(
		string keyId,
		string batchId,
		EncryptedData masterEncryptedDek,
		QuorumEnvelopeGenerationResult quorum,
		CancellationToken cancellationToken)
	{
		var masterContext = new EncryptionContext { KeyId = EscrowMasterKeyId };
		var dek = await _encryptionProvider.DecryptAsync(masterEncryptedDek, masterContext, cancellationToken)
			.ConfigureAwait(false);
		try
		{
			var kekSalt = RandomNumberGenerator.GetBytes(32);
			var kek = QuorumRecoverySeam.DeriveKek(quorum.Secret, kekSalt, BuildKekInfo(keyId, batchId));
			QuorumKeyWrap innerWrap;
			try
			{
				innerWrap = QuorumRecoverySeam.WrapDek(dek, kek);
			}
			finally
			{
				CryptographicOperations.ZeroMemory(kek);
			}

			var outer = await _encryptionProvider.EncryptAsync(innerWrap.WrappedDek, masterContext, cancellationToken)
				.ConfigureAwait(false);

			return new BatchWrap(batchId, quorum.SecretCommitment, kekSalt, innerWrap.Iv, innerWrap.Tag, outer);
		}
		finally
		{
			CryptographicOperations.ZeroMemory(dek);
			CryptographicOperations.ZeroMemory(quorum.Secret);
		}
	}

	/// <inheritdoc />
	public Task<bool> RevokeEscrowAsync(
		string keyId,
		string? reason,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

		if (!_escrowStore.TryGetValue(keyId, out var entry))
		{
			return Task.FromResult(false);
		}

		var revoked = entry with { State = EscrowState.Revoked };
		_escrowStore[keyId] = revoked;

		return Task.FromResult(true);
	}

	/// <inheritdoc />
	public Task<EscrowStatus?> GetEscrowStatusAsync(
		string keyId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

		if (!_escrowStore.TryGetValue(keyId, out var entry))
		{
			return Task.FromResult<EscrowStatus?>(null);
		}

		var status = new EscrowStatus
		{
			KeyId = entry.KeyId,
			EscrowId = entry.EscrowId,
			State = entry.State,
			EscrowedAt = entry.EscrowedAt,
			ExpiresAt = entry.ExpiresAt
		};

		return Task.FromResult<EscrowStatus?>(status);
	}

	[LoggerMessage(
		ComplianceEventId.KeyEscrowBackupCompleted,
		LogLevel.Information,
		"Key escrow backup completed for key {KeyId}, escrow {EscrowId}")]
	private partial void LogKeyEscrowBackupCompleted(string keyId, string escrowId);

	[LoggerMessage(
		ComplianceEventId.KeyEscrowRecoveryCompleted,
		LogLevel.Information,
		"Key escrow recovery completed for key {KeyId}")]
	private partial void LogKeyEscrowRecoveryCompleted(string keyId);

	[LoggerMessage(
		ComplianceEventId.KeyEscrowOperationFailed,
		LogLevel.Error,
		"Key escrow {Operation} failed for key {KeyId}")]
	private partial void LogKeyEscrowOperationFailed(string keyId, string operation, Exception exception);

	private sealed record EscrowEntry
	{
		public required string KeyId { get; init; }
		public required string EscrowId { get; init; }

		/// <summary>The master-only encrypted DEK, present only BEFORE any recovery tokens are generated. Once a
		/// token batch is generated the escrow is SEALED (this becomes <see langword="null"/>) and the key is
		/// recoverable only via the per-batch quorum envelope in <see cref="BatchWraps"/> — the master key alone
		/// can no longer unwrap it (closes the lone-holder bypass, e6batc).</summary>
		public EncryptedData? EncryptedKeyMaterial { get; init; }

		public required DateTimeOffset EscrowedAt { get; init; }
		public DateTimeOffset? ExpiresAt { get; init; }
		public required EscrowState State { get; init; }

		/// <summary>The per-batch key wraps (multi-recipient envelope): for each token batch, the DEK bound under
		/// <c>KEK = HKDF(S_batch)</c>. Recovery selects the wrap whose server commitment the reconstructed quorum
		/// matched. Empty until the first batch is generated.</summary>
		public required IReadOnlyList<BatchWrap> BatchWraps { get; init; }
	}

	/// <summary>One token batch's key wrap: the escrowed DEK bound under a KEK derived from that batch's quorum
	/// secret (inner AES-GCM) and then master-encrypted (outer layer).</summary>
	private sealed record BatchWrap(
		string BatchId,
		byte[] SecretCommitment,
		byte[] KekSalt,
		byte[] InnerIv,
		byte[] InnerAuthTag,
		EncryptedData Outer);
}
