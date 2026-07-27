// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Dapper;

using Excalibur.Compliance.KeyManagement;
using Excalibur.Data.Validation;

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

	private static readonly CompositeFormat CannotGenerateTokensForStateFormat =
		CompositeFormat.Parse(Resources.SqlServerKeyEscrowService_CannotGenerateTokensForState);

	// Multi-recipient envelope (e6batc): the escrowed DEK is wrapped once per token batch under a KEK derived
	// from that batch's quorum secret. Recovery requires a reconstructed quorum; the master key alone strips
	// only the outer layer and can never derive the KEK — closing the lone-holder bypass. v2 is the only wrap
	// format (greenfield); the version column is retained for future crypto agility.
	private const int WrapVersion = 2;

	private const int KekSaltBytes = 32;

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

		// Defense-in-depth: validate SQL identifiers even if IValidateOptions ran at startup
		SqlIdentifierValidator.ThrowIfInvalid(_options.Schema, nameof(_options.Schema));
		SqlIdentifierValidator.ThrowIfInvalid(_options.TableName, nameof(_options.TableName));
		SqlIdentifierValidator.ThrowIfInvalid(_options.TokensTableName, nameof(_options.TokensTableName));
		SqlIdentifierValidator.ThrowIfInvalid(_options.WrapTableName, nameof(_options.WrapTableName));
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

		// Fail-closed M-of-N quorum, owned by the provider-agnostic QuorumRecoverySeam: reject a single token,
		// reconstruct via real Shamir, and verify the reconstructed secret against the SERVER-side commitment(s)
		// this store persisted. Any failure throws with zero key material derived. The verified secret AND the
		// commitment it matched are returned — the commitment identifies WHICH batch's KEK wrap to unwrap
		// (multi-recipient envelope: each batch splits a distinct secret).
		var storedCommitments = await LoadStoredCommitmentsAsync(connection, token.EscrowId, cancellationToken)
			.ConfigureAwait(false);

		var quorum = QuorumRecoverySeam.RecoverAndVerifyQuorumSecretForBatch(token, keyId, storedCommitments);

		byte[] decryptedKey;
		try
		{
			// Load the per-batch envelope wrap the reconstructed quorum matched. The escrowed DEK is bound under
			// KEK = HKDF(S_batch); the master key alone strips only the outer layer and can NEVER derive the KEK,
			// so a lone master-key holder cannot recover the key without a reconstructed quorum.
			var wrap = await LoadBatchWrapAsync(connection, token.EscrowId, quorum.MatchedCommitment, cancellationToken)
					.ConfigureAwait(false)
				?? throw new KeyEscrowException(string.Format(
						CultureInfo.InvariantCulture,
						ActiveEscrowNotFoundFormat,
						keyId))
				{ KeyId = keyId, EscrowId = token.EscrowId, ErrorCode = KeyEscrowErrorCode.KeyNotFound };

			// Outer layer (defense in depth): master-decrypt the wrapped inner ciphertext.
			var outerEncryptedData = new EncryptedData
			{
				Ciphertext = wrap.WrappedInnerKey,
				Iv = wrap.OuterIv,
				AuthTag = wrap.OuterAuthTag,
				Algorithm = (EncryptionAlgorithm)wrap.OuterAlgorithm,
				KeyId = wrap.OuterMasterKeyId,
				KeyVersion = wrap.OuterMasterKeyVersion
			};

			var context = new EncryptionContext { Purpose = $"key-escrow-envelope:{keyId}", TenantId = wrap.TenantId };

			var innerCiphertext = await _encryptionProvider
				.DecryptAsync(outerEncryptedData, context, cancellationToken)
				.ConfigureAwait(false);

			// Inner layer: derive the batch KEK from the reconstructed quorum secret and AES-GCM-unwrap the DEK.
			// A wrong quorum or a tampered wrap throws (tag mismatch) = fail closed, zero key material.
			var kek = QuorumRecoverySeam.DeriveKek(quorum.Secret, wrap.KekSalt, BuildKekInfo(keyId, wrap.BatchId));
			try
			{
				decryptedKey = QuorumRecoverySeam.UnwrapDek(innerCiphertext, kek, wrap.InnerIv, wrap.InnerAuthTag);
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

		// Multi-recipient envelope (e6batc): generate a fresh quorum secret S for THIS batch and bind the
		// escrowed DEK under KEK = HKDF(S). Ownership of S transfers here; it is zeroed immediately after
		// wrapping (finally). Recovery verifies the reconstructed secret against the SERVER-side commitment.
		var quorum = QuorumRecoverySeam.GenerateQuorumSharesWithSecret(custodianCount, threshold);
		var shares = quorum.Shares;
		var secretCommitment = quorum.SecretCommitment;

		var batchId = Guid.NewGuid().ToString("N");
		var tokenExpiration = expiresIn ?? _options.DefaultTokenExpiration;
		var createdAt = DateTimeOffset.UtcNow;
		var expiresAt = createdAt.Add(tokenExpiration);

		try
		{
			await using var connection = new SqlConnection(_options.ConnectionString);
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
			await using var transaction =
				(SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

			try
			{
				// Bind the escrowed DEK to this batch's quorum KEK (envelope) and SEAL the master-only copy —
				// after this, recovery requires a reconstructed quorum. Fails closed if already sealed.
				await WrapKeyForBatchAsync(connection, transaction, keyId, status.EscrowId, batchId,
						secretCommitment, quorum.Secret, cancellationToken)
					.ConfigureAwait(false);

				// Persist the tokens for this batch (share data stays with the custodian).
				var tokens = await InsertBatchTokensAsync(connection, transaction, keyId, status.EscrowId, batchId,
						shares, secretCommitment, custodianCount, threshold, createdAt, expiresAt, cancellationToken)
					.ConfigureAwait(false);

				await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

				LogGeneratedTokens(custodianCount, keyId, threshold);
				return tokens;
			}
			catch
			{
				await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
				throw;
			}
		}
		finally
		{
			// The quorum secret is owned here (GenerateQuorumSharesWithSecret) — zero it on every path.
			CryptographicOperations.ZeroMemory(quorum.Secret);
		}
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

	/// <summary>
	/// Loads the server-side quorum commitment(s) persisted for the escrow's token batch(es). These are the
	/// values <see cref="QuorumRecoverySeam.RecoverAndVerifyQuorumSecret"/> verifies the reconstructed secret
	/// against — closing the fabricated-share bypass, since the commitment lives in the store, not the token.
	/// </summary>
	private async Task<IReadOnlyCollection<byte[]>> LoadStoredCommitmentsAsync(
		SqlConnection connection,
		string escrowId,
		CancellationToken cancellationToken)
	{
		var commitmentSql = $@"
			SELECT DISTINCT SecretCommitment
			FROM {_options.FullyQualifiedTokensTableName}
			WHERE EscrowId = @EscrowId AND SecretCommitment IS NOT NULL";

		return (await connection.QueryAsync<byte[]>(
				new CommandDefinition(commitmentSql, new { EscrowId = escrowId },
					commandTimeout: _options.CommandTimeoutSeconds, cancellationToken: cancellationToken))
			.ConfigureAwait(false)).ToList();
	}

	// The HKDF context/label binding the batch KEK to this key and batch — MUST be identical at wrap and
	// unwrap. Domain-separates escrow KEKs from any other HKDF use of the same quorum secret.
	private static string BuildKekInfo(string keyId, string batchId) =>
		$"excalibur:key-escrow:kek:v2|{keyId}|batch:{batchId}";

	/// <summary>
	/// Inserts the per-batch envelope wrap: the escrowed DEK bound under <c>KEK = HKDF(S_batch)</c> (inner
	/// AES-GCM) then master-encrypted (outer layer). One row per token batch; recovery selects it by the
	/// server commitment the reconstructed quorum matched.
	/// </summary>
	private async Task InsertBatchWrapAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		string escrowId,
		string batchId,
		byte[] secretCommitment,
		byte[] kekSalt,
		byte[] innerIv,
		byte[] innerAuthTag,
		EncryptedData outer,
		CancellationToken cancellationToken)
	{
		var parameters = new DynamicParameters();
		parameters.Add("@EscrowId", escrowId);
		parameters.Add("@BatchId", batchId);
		parameters.Add("@SecretCommitment", secretCommitment, DbType.Binary, size: 32);
		parameters.Add("@KekSalt", kekSalt);
		parameters.Add("@InnerIv", innerIv);
		parameters.Add("@InnerAuthTag", innerAuthTag);
		parameters.Add("@WrappedInnerKey", outer.Ciphertext);
		parameters.Add("@OuterIv", outer.Iv);
		parameters.Add("@OuterAuthTag", outer.AuthTag);
		parameters.Add("@OuterAlgorithm", (int)outer.Algorithm);
		parameters.Add("@OuterMasterKeyId", outer.KeyId);
		parameters.Add("@OuterMasterKeyVersion", outer.KeyVersion);
		parameters.Add("@WrapVersion", WrapVersion);

		var sql = $@"
			INSERT INTO {_options.FullyQualifiedWrapTableName}
			(EscrowId, BatchId, SecretCommitment, KekSalt, InnerIv, InnerAuthTag, WrappedInnerKey,
			 OuterIv, OuterAuthTag, OuterAlgorithm, OuterMasterKeyId, OuterMasterKeyVersion, WrapVersion)
			VALUES
			(@EscrowId, @BatchId, @SecretCommitment, @KekSalt, @InnerIv, @InnerAuthTag, @WrappedInnerKey,
			 @OuterIv, @OuterAuthTag, @OuterAlgorithm, @OuterMasterKeyId, @OuterMasterKeyVersion, @WrapVersion)";

		_ = await connection.ExecuteAsync(
				new CommandDefinition(sql, parameters, transaction,
					commandTimeout: _options.CommandTimeoutSeconds, cancellationToken: cancellationToken))
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Loads the per-batch envelope wrap the reconstructed quorum matched (by server commitment), joined to the
	/// escrow row for the tenant scope needed to master-decrypt the outer layer.
	/// </summary>
	private async Task<WrapRow?> LoadBatchWrapAsync(
		SqlConnection connection,
		string escrowId,
		byte[] matchedCommitment,
		CancellationToken cancellationToken)
	{
		var sql = $@"
			SELECT w.BatchId, w.KekSalt, w.InnerIv, w.InnerAuthTag, w.WrappedInnerKey,
				   w.OuterIv, w.OuterAuthTag, w.OuterAlgorithm, w.OuterMasterKeyId, w.OuterMasterKeyVersion,
				   e.TenantId
			FROM {_options.FullyQualifiedWrapTableName} w
			INNER JOIN {_options.FullyQualifiedTableName} e ON e.EscrowId = w.EscrowId
			WHERE w.EscrowId = @EscrowId AND w.SecretCommitment = @SecretCommitment";

		return await connection.QuerySingleOrDefaultAsync<WrapRow>(
				new CommandDefinition(sql, new { EscrowId = escrowId, SecretCommitment = matchedCommitment },
					commandTimeout: _options.CommandTimeoutSeconds, cancellationToken: cancellationToken))
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Reads the escrowed DEK under a serializable lock, binds it to the batch's quorum KEK
	/// (inner <c>AES-GCM(DEK, HKDF(S))</c> then master-encrypted outer), persists the wrap, and SEALS the
	/// escrow (<c>EncryptedKey -&gt; NULL</c>). Fails closed if the escrow is already sealed — adding a
	/// custodian batch requires an existing quorum's consent (rotation), not supported on this path.
	/// </summary>
	private async Task WrapKeyForBatchAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		string keyId,
		string escrowId,
		string batchId,
		byte[] secretCommitment,
		byte[] quorumSecret,
		CancellationToken cancellationToken)
	{
		var escrow = await connection.QuerySingleOrDefaultAsync<DekSourceRow>(
				new CommandDefinition(
					$@"SELECT EncryptedKey, Iv, AuthTag, Algorithm, MasterKeyId, MasterKeyVersion, TenantId
					   FROM {_options.FullyQualifiedTableName} WITH (UPDLOCK, HOLDLOCK)
					   WHERE KeyId = @KeyId AND State = @State",
					new { KeyId = keyId, State = (int)EscrowState.Active },
					transaction, commandTimeout: _options.CommandTimeoutSeconds, cancellationToken: cancellationToken))
			.ConfigureAwait(false)
			?? throw new KeyEscrowException(string.Format(CultureInfo.InvariantCulture, ActiveEscrowNotFoundFormat, keyId))
			{ KeyId = keyId, EscrowId = escrowId, ErrorCode = KeyEscrowErrorCode.KeyNotFound };

		if (escrow.EncryptedKey is null or { Length: 0 })
		{
			throw new KeyEscrowException(
				"Recovery tokens have already been generated for this escrow. Adding another custodian batch " +
				"requires an existing quorum's consent (rotation), which is not supported on this path.")
			{ KeyId = keyId, EscrowId = escrowId, ErrorCode = KeyEscrowErrorCode.EscrowExpired };
		}

		// Master-decrypt the DEK — the ONLY point it is in plaintext, and only to re-wrap under the quorum KEK.
		var dek = await _encryptionProvider.DecryptAsync(
				new EncryptedData
				{
					Ciphertext = escrow.EncryptedKey,
					Iv = escrow.Iv,
					AuthTag = escrow.AuthTag,
					Algorithm = (EncryptionAlgorithm)escrow.Algorithm,
					KeyId = escrow.MasterKeyId,
					KeyVersion = escrow.MasterKeyVersion
				},
				new EncryptionContext { Purpose = $"key-escrow:{keyId}", TenantId = escrow.TenantId },
				cancellationToken)
			.ConfigureAwait(false);

		try
		{
			// Inner layer: KEK = HKDF(S, salt); Wrap = AES-256-GCM(DEK, KEK).
			var kekSalt = RandomNumberGenerator.GetBytes(KekSaltBytes);
			var kek = QuorumRecoverySeam.DeriveKek(quorumSecret, kekSalt, BuildKekInfo(keyId, batchId));
			QuorumKeyWrap innerWrap;
			try
			{
				innerWrap = QuorumRecoverySeam.WrapDek(dek, kek);
			}
			finally
			{
				CryptographicOperations.ZeroMemory(kek);
			}

			// Outer layer (defense in depth): master-encrypt the inner wrapped ciphertext.
			var outer = await _encryptionProvider.EncryptAsync(
					innerWrap.WrappedDek,
					new EncryptionContext { Purpose = $"key-escrow-envelope:{keyId}", TenantId = escrow.TenantId },
					cancellationToken)
				.ConfigureAwait(false);

			await InsertBatchWrapAsync(connection, transaction, escrowId, batchId, secretCommitment,
					kekSalt, innerWrap.Iv, innerWrap.Tag, outer, cancellationToken)
				.ConfigureAwait(false);
		}
		finally
		{
			CryptographicOperations.ZeroMemory(dek);
		}

		// SEAL: null the master-only EncryptedKey so the key is now recoverable ONLY via the quorum envelope.
		_ = await connection.ExecuteAsync(
				new CommandDefinition(
					$@"UPDATE {_options.FullyQualifiedTableName} SET EncryptedKey = NULL
					   WHERE KeyId = @KeyId AND State = @State",
					new { KeyId = keyId, State = (int)EscrowState.Active },
					transaction, commandTimeout: _options.CommandTimeoutSeconds, cancellationToken: cancellationToken))
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Persists one recovery-token row per custodian for a batch (share data stays with the custodian) and
	/// returns the tokens to hand back to the caller.
	/// </summary>
	private async Task<RecoveryToken[]> InsertBatchTokensAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		string keyId,
		string escrowId,
		string batchId,
		byte[][] shares,
		byte[] secretCommitment,
		int custodianCount,
		int threshold,
		DateTimeOffset createdAt,
		DateTimeOffset expiresAt,
		CancellationToken cancellationToken)
	{
		var tokens = new RecoveryToken[custodianCount];
		for (var i = 0; i < custodianCount; i++)
		{
			var tokenId = Guid.NewGuid().ToString("N");

			tokens[i] = new RecoveryToken
			{
				TokenId = tokenId,
				KeyId = keyId,
				EscrowId = escrowId,
				ShareIndex = i + 1, // 1-based index
				ShareData = shares[i],
				TotalShares = custodianCount,
				Threshold = threshold,
				CreatedAt = createdAt,
				ExpiresAt = expiresAt
			};

			var parameters = new DynamicParameters();
			parameters.Add("@TokenId", tokenId);
			parameters.Add("@KeyId", keyId);
			parameters.Add("@EscrowId", escrowId);
			parameters.Add("@BatchId", batchId);
			parameters.Add("@ShareIndex", i + 1);
			parameters.Add("@TotalShares", custodianCount);
			parameters.Add("@Threshold", threshold);
			parameters.Add("@CreatedAt", createdAt);
			parameters.Add("@ExpiresAt", expiresAt);
			parameters.Add("@IsUsed", false);
			parameters.Add("@SecretCommitment", secretCommitment, DbType.Binary, size: 32);

			var sql = $@"
				INSERT INTO {_options.FullyQualifiedTokensTableName}
				(TokenId, KeyId, EscrowId, BatchId, ShareIndex, TotalShares, Threshold, CreatedAt, ExpiresAt, IsUsed, SecretCommitment)
				VALUES
				(@TokenId, @KeyId, @EscrowId, @BatchId, @ShareIndex, @TotalShares, @Threshold, @CreatedAt, @ExpiresAt, @IsUsed, @SecretCommitment)";

			_ = await connection.ExecuteAsync(
					new CommandDefinition(sql, parameters, transaction,
						commandTimeout: _options.CommandTimeoutSeconds, cancellationToken: cancellationToken))
				.ConfigureAwait(false);
		}

		return tokens;
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
	private sealed class DekSourceRow
	{
		// Nullable: NULL once the escrow is SEALED (a token batch was generated) — the key is then recoverable
		// only via the quorum envelope, never the master key alone.
		public byte[]? EncryptedKey { get; init; }
		public byte[] Iv { get; init; } = [];
		public byte[]? AuthTag { get; init; }
		public int Algorithm { get; init; }
		public string MasterKeyId { get; init; } = string.Empty;
		public int MasterKeyVersion { get; init; }
		public string? TenantId { get; init; }
	}

	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Dapper materializes this type.")]
	private sealed class WrapRow
	{
		public string BatchId { get; init; } = string.Empty;
		public byte[] KekSalt { get; init; } = [];
		public byte[] InnerIv { get; init; } = [];
		public byte[] InnerAuthTag { get; init; } = [];
		public byte[] WrappedInnerKey { get; init; } = [];
		public byte[] OuterIv { get; init; } = [];
		public byte[]? OuterAuthTag { get; init; }
		public int OuterAlgorithm { get; init; }
		public string OuterMasterKeyId { get; init; } = string.Empty;
		public int OuterMasterKeyVersion { get; init; }
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
