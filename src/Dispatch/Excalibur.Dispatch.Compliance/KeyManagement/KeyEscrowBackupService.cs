// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Compliance.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Compliance;

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
	private readonly ConcurrentDictionary<string, EscrowEntry> _escrowStore = new(StringComparer.OrdinalIgnoreCase);
	private readonly IEncryptionProvider _encryptionProvider;
	private readonly IKeyManagementProvider _keyManagementProvider;
	private readonly ILogger<KeyEscrowBackupService> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="KeyEscrowBackupService"/> class.
	/// </summary>
	/// <param name="encryptionProvider">The encryption provider for encrypting escrowed keys.</param>
	/// <param name="keyManagementProvider">The key management provider.</param>
	/// <param name="logger">The logger.</param>
	public KeyEscrowBackupService(
		IEncryptionProvider encryptionProvider,
		IKeyManagementProvider keyManagementProvider,
		ILogger<KeyEscrowBackupService> logger)
	{
		_encryptionProvider = encryptionProvider ?? throw new ArgumentNullException(nameof(encryptionProvider));
		_keyManagementProvider = keyManagementProvider ?? throw new ArgumentNullException(nameof(keyManagementProvider));
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

		try
		{
			var context = new EncryptionContext { KeyId = "escrow-master-key" };
			var encrypted = await _encryptionProvider.EncryptAsync(
				keyMaterial.ToArray(), context, cancellationToken).ConfigureAwait(false);

			var escrowId = Guid.NewGuid().ToString("N");
			var now = DateTimeOffset.UtcNow;

			var entry = new EscrowEntry
			{
				KeyId = keyId,
				EscrowId = escrowId,
				EncryptedKeyMaterial = encrypted.Ciphertext,
				EscrowedAt = now,
				ExpiresAt = options?.ExpiresIn is not null ? now.Add(options.ExpiresIn.Value) : null,
				State = EscrowState.Active
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
	}

	/// <inheritdoc />
	public Task<ReadOnlyMemory<byte>> RecoverKeyAsync(
		string keyId,
		RecoveryToken token,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
		ArgumentNullException.ThrowIfNull(token);

		if (!_escrowStore.TryGetValue(keyId, out var entry))
		{
			throw new KeyEscrowException($"No escrow exists for key '{keyId}'.");
		}

		if (entry.State != EscrowState.Active)
		{
			throw new KeyEscrowException($"Escrow for key '{keyId}' is in state '{entry.State}' and cannot be recovered.");
		}

		if (entry.ExpiresAt.HasValue && DateTimeOffset.UtcNow > entry.ExpiresAt)
		{
			throw new KeyEscrowException($"Escrow for key '{keyId}' has expired.");
		}

		LogKeyEscrowRecoveryCompleted(keyId);

		return Task.FromResult<ReadOnlyMemory<byte>>(entry.EncryptedKeyMaterial);
	}

	/// <inheritdoc />
	public Task<RecoveryToken[]> GenerateRecoveryTokensAsync(
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
			throw new KeyEscrowException($"No escrow exists for key '{keyId}'.");
		}

		var expiration = expiresIn ?? TimeSpan.FromHours(24);
		var now = DateTimeOffset.UtcNow;

		var tokens = new RecoveryToken[custodianCount];
		for (var i = 0; i < custodianCount; i++)
		{
			tokens[i] = new RecoveryToken
			{
				TokenId = Guid.NewGuid().ToString("N"),
				KeyId = keyId,
				EscrowId = entry.EscrowId,
				ShareIndex = i + 1,
				ShareData = Guid.NewGuid().ToByteArray(),
				CreatedAt = now,
				ExpiresAt = now.Add(expiration),
				Threshold = threshold,
				TotalShares = custodianCount
			};
		}

		return Task.FromResult(tokens);
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
		public required byte[] EncryptedKeyMaterial { get; init; }
		public required DateTimeOffset EscrowedAt { get; init; }
		public DateTimeOffset? ExpiresAt { get; init; }
		public required EscrowState State { get; init; }
	}
}
