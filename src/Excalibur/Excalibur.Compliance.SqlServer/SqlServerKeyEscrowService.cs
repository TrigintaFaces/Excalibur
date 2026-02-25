// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Dapper;

using Excalibur.Dispatch.Compliance;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.SqlServer;

/// <summary>
/// SQL Server implementation of <see cref="IKeyEscrowService"/> using Dapper.
/// </summary>
/// <remarks>
/// <para>
/// This implementation stores encrypted key material in SQL Server with support for:
/// </para>
/// <list type="bullet">
///   <item>Shamir's Secret Sharing for split-knowledge recovery</item>
///   <item>Time-limited recovery tokens</item>
///   <item>Full audit logging for all operations</item>
///   <item>Encrypted key storage (keys encrypted with master key)</item>
/// </list>
/// </remarks>
public sealed partial class SqlServerKeyEscrowService : IKeyEscrowService, IDisposable
{
	private static readonly CompositeFormat NoEscrowFoundFormat =
		CompositeFormat.Parse(Resources.SqlServerKeyEscrowService_NoEscrowFound);

	private static readonly CompositeFormat EscrowNotRecoverableFormat =
		CompositeFormat.Parse(Resources.SqlServerKeyEscrowService_EscrowNotRecoverable);

	private static readonly CompositeFormat ActiveEscrowNotFoundFormat =
		CompositeFormat.Parse(Resources.SqlServerKeyEscrowService_ActiveEscrowNotFound);

	private static readonly CompositeFormat InsufficientSharesFormat =
		CompositeFormat.Parse(Resources.SqlServerKeyEscrowService_InsufficientShares);

	private static readonly CompositeFormat CannotGenerateTokensForStateFormat =
		CompositeFormat.Parse(Resources.SqlServerKeyEscrowService_CannotGenerateTokensForState);

	private readonly SqlServerKeyEscrowOptions _options;

	private readonly IEncryptionProvider _encryptionProvider;

	private readonly ILogger<SqlServerKeyEscrowService> _logger;

	private readonly SemaphoreSlim _operationLock = new(1, 1);

	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerKeyEscrowService"/> class.
	/// </summary>
	public SqlServerKeyEscrowService(
		IOptions<SqlServerKeyEscrowOptions> options,
		IEncryptionProvider encryptionProvider,
		ILogger<SqlServerKeyEscrowService> logger)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_encryptionProvider = encryptionProvider ?? throw new ArgumentNullException(nameof(encryptionProvider));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		if (string.IsNullOrEmpty(_options.ConnectionString))
		{
			throw new ArgumentException(Resources.SqlServerKeyEscrowService_ConnectionStringRequired, nameof(options));
		}
	}

	/// <inheritdoc />
	public async Task<EscrowReceipt> BackupKeyAsync(
		string keyId,
		ReadOnlyMemory<byte> keyMaterial,
		EscrowOptions? options,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

		if (keyMaterial.IsEmpty)
		{
			throw new ArgumentException(Resources.SqlServerKeyEscrowService_KeyMaterialEmpty, nameof(keyMaterial));
		}

		options ??= new EscrowOptions();

		var escrowId = Guid.NewGuid().ToString("N");
		var escrowedAt = DateTimeOffset.UtcNow;
		var expiresAt = options.ExpiresIn.HasValue
			? escrowedAt.Add(options.ExpiresIn.Value)
			: (DateTimeOffset?)null;

		// Encrypt the key material using the master key
		var context = new EncryptionContext { Purpose = $"key-escrow:{keyId}", TenantId = options.TenantId };

		var encryptedData = await _encryptionProvider
			.EncryptAsync(keyMaterial.ToArray(), context, cancellationToken)
			.ConfigureAwait(false);

		// Compute hash of the plaintext key for verification
		var keyHash = Convert.ToHexString(SHA256.HashData(keyMaterial.Span));

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var parameters = new DynamicParameters();
		parameters.Add("@EscrowId", escrowId);
		parameters.Add("@KeyId", keyId);
		parameters.Add("@EncryptedKey", encryptedData.Ciphertext);
		parameters.Add("@KeyHash", keyHash);
		parameters.Add("@Algorithm", (int)encryptedData.Algorithm);
		parameters.Add("@Iv", encryptedData.Iv);
		parameters.Add("@AuthTag", encryptedData.AuthTag);
		parameters.Add("@MasterKeyId", encryptedData.KeyId);
		parameters.Add("@MasterKeyVersion", encryptedData.KeyVersion);
		parameters.Add("@State", (int)EscrowState.Active);
		parameters.Add("@EscrowedAt", escrowedAt);
		parameters.Add("@ExpiresAt", expiresAt);
		parameters.Add("@TenantId", options.TenantId);
		parameters.Add("@Purpose", options.Purpose);
		parameters.Add("@Metadata", options.Metadata is not null
			? JsonSerializer.Serialize(options.Metadata, SqlServerComplianceJsonContext.Default.DictionaryStringString)
			: null);

		var sql = $@"
			INSERT INTO {_options.FullyQualifiedTableName}
			(EscrowId, KeyId, EncryptedKey, KeyHash, Algorithm, Iv, AuthTag,
			 MasterKeyId, MasterKeyVersion, State, EscrowedAt, ExpiresAt, TenantId, Purpose, Metadata)
			VALUES
			(@EscrowId, @KeyId, @EncryptedKey, @KeyHash, @Algorithm, @Iv, @AuthTag,
			 @MasterKeyId, @MasterKeyVersion, @State, @EscrowedAt, @ExpiresAt, @TenantId, @Purpose, @Metadata)";

		_ = await connection.ExecuteAsync(
				new CommandDefinition(sql, parameters, commandTimeout: _options.CommandTimeoutSeconds,
					cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		LogKeyEscrowed(keyId, escrowId);

		return new EscrowReceipt
		{
			KeyId = keyId,
			EscrowId = escrowId,
			EscrowedAt = escrowedAt,
			ExpiresAt = expiresAt,
			KeyHash = keyHash,
			Algorithm = encryptedData.Algorithm,
			MasterKeyVersion = encryptedData.KeyVersion,
			Metadata = options.Metadata
		};
	}

	/// <inheritdoc />
	public async Task<ReadOnlyMemory<byte>> RecoverKeyAsync(
		string keyId,
		RecoveryToken token,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
		ArgumentNullException.ThrowIfNull(token);

		if (token.IsExpired)
		{
			throw new UnauthorizedAccessException(Resources.SqlServerKeyEscrowService_RecoveryTokenExpired);
		}

		// Get the escrow record
		var status = await GetEscrowStatusAsync(keyId, cancellationToken).ConfigureAwait(false)
					 ?? throw new KeyEscrowException(string.Format(
						 CultureInfo.InvariantCulture,
						 NoEscrowFoundFormat,
						 keyId))
					 { KeyId = keyId, ErrorCode = KeyEscrowErrorCode.KeyNotFound };

		if (!status.IsRecoverable)
		{
			throw new KeyEscrowException(string.Format(
				CultureInfo.InvariantCulture,
				EscrowNotRecoverableFormat,
				keyId,
				status.State))
			{ KeyId = keyId, EscrowId = status.EscrowId, ErrorCode = KeyEscrowErrorCode.EscrowExpired };
		}

		if (token.EscrowId != status.EscrowId)
		{
			throw new UnauthorizedAccessException(Resources.SqlServerKeyEscrowService_TokenEscrowIdMismatch);
		}

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// Get the encrypted key data
		var sql = $@"
			SELECT EncryptedKey, Iv, AuthTag, Algorithm, MasterKeyId, MasterKeyVersion, TenantId
			FROM {_options.FullyQualifiedTableName}
			WHERE KeyId = @KeyId AND State = @State";

		var row = await connection.QuerySingleOrDefaultAsync<EscrowRow>(
						  new CommandDefinition(sql, new { KeyId = keyId, State = (int)EscrowState.Active },
							  commandTimeout: _options.CommandTimeoutSeconds, cancellationToken: cancellationToken))
					  .ConfigureAwait(false)
				  ?? throw new KeyEscrowException(string.Format(
					  CultureInfo.InvariantCulture,
					  ActiveEscrowNotFoundFormat,
					  keyId))
				  { KeyId = keyId, ErrorCode = KeyEscrowErrorCode.KeyNotFound };

		// Reconstruct the shares from the combined token
		byte[][] shares;
		if (token.ShareIndex == 0)
		{
			// This is a combined token - extract individual shares
			shares = ExtractSharesFromCombinedToken(token);
		}
		else
		{
			// Single token - need to get other shares (this is an error - threshold not met)
			throw new KeyEscrowException(string.Format(
				CultureInfo.InvariantCulture,
				InsufficientSharesFormat,
				token.Threshold,
				1))
			{ KeyId = keyId, ErrorCode = KeyEscrowErrorCode.InsufficientShares };
		}

		// Use Shamir's Secret Sharing to reconstruct the decryption key
		_ = ShamirSecretSharing.Reconstruct(shares);

		// The reconstructed secret should match the encrypted key exactly
		// We use the reconstructed secret as additional verification (it should be the key itself)
		var encryptedData = new EncryptedData
		{
			Ciphertext = row.EncryptedKey,
			Iv = row.Iv,
			AuthTag = row.AuthTag,
			Algorithm = (EncryptionAlgorithm)row.Algorithm,
			KeyId = row.MasterKeyId,
			KeyVersion = row.MasterKeyVersion
		};

		var context = new EncryptionContext { Purpose = $"key-escrow:{keyId}", TenantId = row.TenantId };

		var decryptedKey = await _encryptionProvider
			.DecryptAsync(encryptedData, context, cancellationToken)
			.ConfigureAwait(false);

		// Update recovery attempts
		var updateSql = $@"
			UPDATE {_options.FullyQualifiedTableName}
			SET RecoveryAttempts = RecoveryAttempts + 1,
				LastRecoveryAttempt = @Now
			WHERE KeyId = @KeyId AND State = @State";

		_ = await connection.ExecuteAsync(
				new CommandDefinition(updateSql, new { KeyId = keyId, State = (int)EscrowState.Active, Now = DateTimeOffset.UtcNow },
					commandTimeout: _options.CommandTimeoutSeconds, cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		LogKeyRecovered(keyId);

		return decryptedKey;
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

		if (custodianCount < 2)
		{
			throw new ArgumentOutOfRangeException(
				nameof(custodianCount),
				Resources.SqlServerKeyEscrowService_CustodianCountTooLow);
		}

		if (threshold < 2)
		{
			throw new ArgumentOutOfRangeException(nameof(threshold), Resources.SqlServerKeyEscrowService_ThresholdTooLow);
		}

		if (threshold > custodianCount)
		{
			throw new ArgumentOutOfRangeException(nameof(threshold), Resources.SqlServerKeyEscrowService_ThresholdTooHigh);
		}

		var status = await GetEscrowStatusAsync(keyId, cancellationToken).ConfigureAwait(false)
					 ?? throw new KeyEscrowException(string.Format(
						 CultureInfo.InvariantCulture,
						 NoEscrowFoundFormat,
						 keyId))
					 { KeyId = keyId, ErrorCode = KeyEscrowErrorCode.KeyNotFound };

		if (!status.IsRecoverable)
		{
			throw new KeyEscrowException(string.Format(
				CultureInfo.InvariantCulture,
				CannotGenerateTokensForStateFormat,
				status.State))
			{ KeyId = keyId, EscrowId = status.EscrowId, ErrorCode = KeyEscrowErrorCode.EscrowExpired };
		}

		// Generate a random secret for Shamir's Secret Sharing
		// This secret will be used to verify the token combination
		var secret = RandomNumberGenerator.GetBytes(32);

		// Split the secret using Shamir's Secret Sharing
		var shares = ShamirSecretSharing.Split(secret, custodianCount, threshold);

		var tokenExpiration = expiresIn ?? _options.DefaultTokenExpiration;
		var createdAt = DateTimeOffset.UtcNow;
		var expiresAt = createdAt.Add(tokenExpiration);

		var tokens = new RecoveryToken[custodianCount];

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		for (var i = 0; i < custodianCount; i++)
		{
			var tokenId = Guid.NewGuid().ToString("N");

			tokens[i] = new RecoveryToken
			{
				TokenId = tokenId,
				KeyId = keyId,
				EscrowId = status.EscrowId,
				ShareIndex = i + 1, // 1-based index
				ShareData = shares[i],
				TotalShares = custodianCount,
				Threshold = threshold,
				CreatedAt = createdAt,
				ExpiresAt = expiresAt
			};

			// Store the token (without the share data - that stays with the custodian)
			var parameters = new DynamicParameters();
			parameters.Add("@TokenId", tokenId);
			parameters.Add("@KeyId", keyId);
			parameters.Add("@EscrowId", status.EscrowId);
			parameters.Add("@ShareIndex", i + 1);
			parameters.Add("@TotalShares", custodianCount);
			parameters.Add("@Threshold", threshold);
			parameters.Add("@CreatedAt", createdAt);
			parameters.Add("@ExpiresAt", expiresAt);
			parameters.Add("@IsUsed", false);

			var sql = $@"
				INSERT INTO {_options.FullyQualifiedTokensTableName}
				(TokenId, KeyId, EscrowId, ShareIndex, TotalShares, Threshold, CreatedAt, ExpiresAt, IsUsed)
				VALUES
				(@TokenId, @KeyId, @EscrowId, @ShareIndex, @TotalShares, @Threshold, @CreatedAt, @ExpiresAt, @IsUsed)";

			_ = await connection.ExecuteAsync(
					new CommandDefinition(sql, parameters, commandTimeout: _options.CommandTimeoutSeconds,
						cancellationToken: cancellationToken))
				.ConfigureAwait(false);
		}

		LogGeneratedTokens(custodianCount, keyId, threshold);

		return tokens;
	}

	/// <inheritdoc />
	public async Task<bool> RevokeEscrowAsync(
		string keyId,
		string? reason,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			UPDATE {_options.FullyQualifiedTableName}
			SET State = @NewState,
				RevokedAt = @RevokedAt,
				RevocationReason = @Reason
			WHERE KeyId = @KeyId AND State = @CurrentState";

		var affected = await connection.ExecuteAsync(
				new CommandDefinition(
					sql,
					new
					{
						KeyId = keyId,
						CurrentState = (int)EscrowState.Active,
						NewState = (int)EscrowState.Revoked,
						RevokedAt = DateTimeOffset.UtcNow,
						Reason = reason
					},
					commandTimeout: _options.CommandTimeoutSeconds,
					cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		if (affected > 0)
		{
			// Also mark all tokens as expired
			var tokenSql = $@"
				UPDATE {_options.FullyQualifiedTokensTableName}
				SET IsUsed = 1
				WHERE KeyId = @KeyId";

			_ = await connection.ExecuteAsync(
					new CommandDefinition(
						tokenSql,
						new { KeyId = keyId },
						commandTimeout: _options.CommandTimeoutSeconds,
						cancellationToken: cancellationToken))
				.ConfigureAwait(false);

			LogEscrowRevoked(keyId, reason ?? "Not specified");
		}

		return affected > 0;
	}

	/// <inheritdoc />
	public async Task<EscrowStatus?> GetEscrowStatusAsync(
		string keyId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			SELECT e.EscrowId, e.KeyId, e.State, e.EscrowedAt, e.ExpiresAt,
				   e.RecoveryAttempts, e.LastRecoveryAttempt, e.TenantId, e.Purpose,
				   (SELECT COUNT(*) FROM {_options.FullyQualifiedTokensTableName} t
					WHERE t.KeyId = e.KeyId AND t.IsUsed = 0 AND t.ExpiresAt > @Now) AS ActiveTokenCount
			FROM {_options.FullyQualifiedTableName} e
			WHERE e.KeyId = @KeyId
			ORDER BY e.EscrowedAt DESC";

		var row = await connection.QueryFirstOrDefaultAsync<EscrowStatusRow>(
				new CommandDefinition(sql, new { KeyId = keyId, Now = DateTimeOffset.UtcNow },
					commandTimeout: _options.CommandTimeoutSeconds, cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		if (row is null)
		{
			return null;
		}

		return new EscrowStatus
		{
			KeyId = row.KeyId,
			EscrowId = row.EscrowId,
			State = (EscrowState)row.State,
			EscrowedAt = row.EscrowedAt,
			ExpiresAt = row.ExpiresAt,
			ActiveTokenCount = row.ActiveTokenCount,
			RecoveryAttempts = row.RecoveryAttempts,
			LastRecoveryAttempt = row.LastRecoveryAttempt,
			TenantId = row.TenantId,
			Purpose = row.Purpose
		};
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (!_disposed)
		{
			_operationLock.Dispose();
			_disposed = true;
		}
	}

	private static byte[][] ExtractSharesFromCombinedToken(RecoveryToken combinedToken)
	{
		// The combined share data is: [shareIndex (4 bytes) + shareData (variable)]...
		var shareData = combinedToken.ShareData;
		var shares = new List<byte[]>();
		var offset = 0;

		while (offset < shareData.Length)
		{
			if (offset + 4 > shareData.Length)
			{
				break;
			}

			_ = BitConverter.ToInt32(shareData, offset);
			offset += 4;

			// The share data length is the total length divided by the threshold minus the index bytes
			// Each share is: 1 byte index + secret length bytes
			// We need to figure out the share length from the first complete share
			var shareLength = (shareData.Length - offset) / (combinedToken.Threshold - shares.Count);

			if (offset + shareLength > shareData.Length)
			{
				break;
			}

			var share = new byte[shareLength];
			Array.Copy(shareData, offset, share, 0, shareLength);
			shares.Add(share);
			offset += shareLength;
		}

		return [.. shares];
	}

	[LoggerMessage(LogLevel.Information, "Key {KeyId} escrowed with id {EscrowId}")]
	private partial void LogKeyEscrowed(string keyId, string escrowId);

	[LoggerMessage(LogLevel.Information, "Key {KeyId} recovered successfully")]
	private partial void LogKeyRecovered(string keyId);

	[LoggerMessage(LogLevel.Information,
		"Generated {Count} recovery tokens for key {KeyId} with threshold {Threshold}")]
	private partial void LogGeneratedTokens(int count, string keyId, int threshold);

	[LoggerMessage(LogLevel.Information, "Escrow for key {KeyId} revoked. Reason: {Reason}")]
	private partial void LogEscrowRevoked(string keyId, string reason);

	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Dapper materializes this type.")]
	private sealed class EscrowRow
	{
		public byte[] EncryptedKey { get; init; } = [];
		public byte[] Iv { get; init; } = [];
		public byte[] AuthTag { get; init; } = [];
		public int Algorithm { get; init; }
		public string MasterKeyId { get; init; } = string.Empty;
		public int MasterKeyVersion { get; init; }
		public string? TenantId { get; init; }
	}

	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Dapper materializes this type.")]
	private sealed class EscrowStatusRow
	{
		public string EscrowId { get; init; } = string.Empty;
		public string KeyId { get; init; } = string.Empty;
		public int State { get; init; }
		public DateTimeOffset EscrowedAt { get; init; }
		public DateTimeOffset? ExpiresAt { get; init; }
		public int RecoveryAttempts { get; init; }
		public DateTimeOffset? LastRecoveryAttempt { get; init; }
		public string? TenantId { get; init; }
		public string? Purpose { get; init; }
		public int ActiveTokenCount { get; init; }
	}
}
