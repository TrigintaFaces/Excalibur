// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Dapper;

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.Compliance.Postgres.Erasure;

/// <summary>
/// Postgres implementation of <see cref="ILegalHoldStore"/> and <see cref="ILegalHoldQueryStore"/> using Dapper.
/// </summary>
/// <remarks>
/// This store provides:
/// <list type="bullet">
/// <item>CRUD operations for legal holds that block GDPR erasure</item>
/// <item>Query operations for listing and filtering holds</item>
/// <item>Support for GDPR Article 17(3) exception tracking</item>
/// <item>Automatic expiration detection for hold lifecycle management</item>
/// </list>
/// </remarks>
public sealed partial class PostgresLegalHoldStore : ILegalHoldStore, ILegalHoldQueryStore
{
	private readonly PostgresLegalHoldStoreOptions _options;
	private readonly ILogger<PostgresLegalHoldStore> _logger;
	private volatile bool _initialized;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresLegalHoldStore"/> class.
	/// </summary>
	public PostgresLegalHoldStore(
		IOptions<PostgresLegalHoldStoreOptions> options,
		ILogger<PostgresLegalHoldStore> logger)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		_options.Validate();
	}

	/// <inheritdoc />
	public async Task SaveHoldAsync(
		LegalHold hold,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(hold);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			INSERT INTO {_options.FullTableName}
				(hold_id, data_subject_id_hash, id_type, tenant_id, basis, case_reference,
				 description, is_active, expires_at, created_by, created_at,
				 released_by, released_at, release_reason)
			VALUES
				(@HoldId, @DataSubjectIdHash, @IdType, @TenantId, @Basis, @CaseReference,
				 @Description, @IsActive, @ExpiresAt, @CreatedBy, @CreatedAt,
				 @ReleasedBy, @ReleasedAt, @ReleaseReason)";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(new CommandDefinition(sql, new
		{
			hold.HoldId,
			hold.DataSubjectIdHash,
			IdType = hold.IdType.HasValue ? (int?)hold.IdType.Value : null,
			hold.TenantId,
			Basis = (int)hold.Basis,
			hold.CaseReference,
			hold.Description,
			hold.IsActive,
			hold.ExpiresAt,
			hold.CreatedBy,
			hold.CreatedAt,
			hold.ReleasedBy,
			hold.ReleasedAt,
			hold.ReleaseReason
		}, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		LogSavedHold(hold.HoldId, hold.CaseReference);
	}

	/// <inheritdoc />
	public async Task<LegalHold?> GetHoldAsync(
		Guid holdId,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			SELECT hold_id, data_subject_id_hash, id_type, tenant_id, basis, case_reference,
				   description, is_active, expires_at, created_by, created_at,
				   released_by, released_at, release_reason
			FROM {_options.FullTableName}
			WHERE hold_id = @HoldId";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var row = await connection.QuerySingleOrDefaultAsync<LegalHoldRow>(
				new CommandDefinition(sql, new { HoldId = holdId }, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);

		return row?.ToLegalHold();
	}

	/// <inheritdoc />
	public async Task<bool> UpdateHoldAsync(
		LegalHold hold,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(hold);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			UPDATE {_options.FullTableName}
			SET data_subject_id_hash = @DataSubjectIdHash,
				id_type = @IdType,
				tenant_id = @TenantId,
				basis = @Basis,
				case_reference = @CaseReference,
				description = @Description,
				is_active = @IsActive,
				expires_at = @ExpiresAt,
				released_by = @ReleasedBy,
				released_at = @ReleasedAt,
				release_reason = @ReleaseReason
			WHERE hold_id = @HoldId";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var affected = await connection.ExecuteAsync(new CommandDefinition(sql, new
		{
			hold.HoldId,
			hold.DataSubjectIdHash,
			IdType = hold.IdType.HasValue ? (int?)hold.IdType.Value : null,
			hold.TenantId,
			Basis = (int)hold.Basis,
			hold.CaseReference,
			hold.Description,
			hold.IsActive,
			hold.ExpiresAt,
			hold.ReleasedBy,
			hold.ReleasedAt,
			hold.ReleaseReason
		}, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		return affected > 0;
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType == typeof(ILegalHoldQueryStore))
		{
			return this;
		}

		return null;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<LegalHold>> GetActiveHoldsForDataSubjectAsync(
		string dataSubjectIdHash,
		string? tenantId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(dataSubjectIdHash);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var sql = tenantId is not null
			? $@"
				SELECT hold_id, data_subject_id_hash, id_type, tenant_id, basis, case_reference,
					   description, is_active, expires_at, created_by, created_at,
					   released_by, released_at, release_reason
				FROM {_options.FullTableName}
				WHERE data_subject_id_hash = @DataSubjectIdHash
				  AND tenant_id = @TenantId
				  AND is_active = TRUE
				ORDER BY created_at DESC"
			: $@"
				SELECT hold_id, data_subject_id_hash, id_type, tenant_id, basis, case_reference,
					   description, is_active, expires_at, created_by, created_at,
					   released_by, released_at, release_reason
				FROM {_options.FullTableName}
				WHERE data_subject_id_hash = @DataSubjectIdHash
				  AND is_active = TRUE
				ORDER BY created_at DESC";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<LegalHoldRow>(
			new CommandDefinition(sql, new { DataSubjectIdHash = dataSubjectIdHash, TenantId = tenantId },
				cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		return rows.Select(r => r.ToLegalHold()).ToList();
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<LegalHold>> GetActiveHoldsForTenantAsync(
		string tenantId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			SELECT hold_id, data_subject_id_hash, id_type, tenant_id, basis, case_reference,
				   description, is_active, expires_at, created_by, created_at,
				   released_by, released_at, release_reason
			FROM {_options.FullTableName}
			WHERE tenant_id = @TenantId
			  AND is_active = TRUE
			ORDER BY created_at DESC";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<LegalHoldRow>(
			new CommandDefinition(sql, new { TenantId = tenantId },
				cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		return rows.Select(r => r.ToLegalHold()).ToList();
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<LegalHold>> ListActiveHoldsAsync(
		string? tenantId,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var whereClauses = new List<string> { "is_active = TRUE" };
		var parameters = new DynamicParameters();

		if (!string.IsNullOrEmpty(tenantId))
		{
			whereClauses.Add("tenant_id = @TenantId");
			parameters.Add("TenantId", tenantId);
		}

		var whereClause = string.Join(" AND ", whereClauses);

		var sql = $@"
			SELECT hold_id, data_subject_id_hash, id_type, tenant_id, basis, case_reference,
				   description, is_active, expires_at, created_by, created_at,
				   released_by, released_at, release_reason
			FROM {_options.FullTableName}
			WHERE {whereClause}
			ORDER BY created_at DESC";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<LegalHoldRow>(
			new CommandDefinition(sql, parameters, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		return rows.Select(r => r.ToLegalHold()).ToList();
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<LegalHold>> ListAllHoldsAsync(
		string? tenantId,
		DateTimeOffset? fromDate,
		DateTimeOffset? toDate,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var whereClauses = new List<string>();
		var parameters = new DynamicParameters();

		if (!string.IsNullOrEmpty(tenantId))
		{
			whereClauses.Add("tenant_id = @TenantId");
			parameters.Add("TenantId", tenantId);
		}

		if (fromDate.HasValue)
		{
			whereClauses.Add("created_at >= @FromDate");
			parameters.Add("FromDate", fromDate.Value);
		}

		if (toDate.HasValue)
		{
			whereClauses.Add("created_at <= @ToDate");
			parameters.Add("ToDate", toDate.Value);
		}

		var whereClause = whereClauses.Count > 0
			? "WHERE " + string.Join(" AND ", whereClauses)
			: string.Empty;

		var sql = $@"
			SELECT hold_id, data_subject_id_hash, id_type, tenant_id, basis, case_reference,
				   description, is_active, expires_at, created_by, created_at,
				   released_by, released_at, release_reason
			FROM {_options.FullTableName}
			{whereClause}
			ORDER BY created_at DESC";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<LegalHoldRow>(
			new CommandDefinition(sql, parameters, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		return rows.Select(r => r.ToLegalHold()).ToList();
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<LegalHold>> GetExpiredHoldsAsync(
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			SELECT hold_id, data_subject_id_hash, id_type, tenant_id, basis, case_reference,
				   description, is_active, expires_at, created_by, created_at,
				   released_by, released_at, release_reason
			FROM {_options.FullTableName}
			WHERE is_active = TRUE
			  AND expires_at IS NOT NULL
			  AND expires_at <= @Now
			ORDER BY expires_at";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<LegalHoldRow>(
			new CommandDefinition(sql, new { Now = DateTimeOffset.UtcNow },
				cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		return rows.Select(r => r.ToLegalHold()).ToList();
	}

	[LoggerMessage(LogLevel.Debug, "Saved legal hold {HoldId} for case {CaseReference}")]
	private partial void LogSavedHold(Guid holdId, string caseReference);

	[LoggerMessage(LogLevel.Debug, "Ensured Postgres legal hold schema and tables exist")]
	private partial void LogSchemaEnsured();

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		if (_initialized)
		{
			return;
		}

		if (_options.AutoCreateSchema)
		{
			await CreateSchemaIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
		}

		_initialized = true;
	}

	private async Task CreateSchemaIfNotExistsAsync(CancellationToken cancellationToken)
	{
		var createSchemaSql = $@"CREATE SCHEMA IF NOT EXISTS ""{_options.SchemaName}""";

		var createTableSql = $@"
			CREATE TABLE IF NOT EXISTS {_options.FullTableName} (
				hold_id UUID NOT NULL PRIMARY KEY,
				data_subject_id_hash VARCHAR(128) NULL,
				id_type INT NULL,
				tenant_id VARCHAR(256) NULL,
				basis INT NOT NULL,
				case_reference VARCHAR(256) NOT NULL,
				description VARCHAR(2000) NOT NULL,
				is_active BOOLEAN NOT NULL DEFAULT TRUE,
				expires_at TIMESTAMPTZ NULL,
				created_by VARCHAR(256) NOT NULL,
				created_at TIMESTAMPTZ NOT NULL,
				released_by VARCHAR(256) NULL,
				released_at TIMESTAMPTZ NULL,
				release_reason VARCHAR(1000) NULL
			)";

		var createIndexesSql = $@"
			CREATE INDEX IF NOT EXISTS ix_{_options.TableName}_subject
				ON {_options.FullTableName} (data_subject_id_hash, is_active);
			CREATE INDEX IF NOT EXISTS ix_{_options.TableName}_tenant
				ON {_options.FullTableName} (tenant_id, is_active);
			CREATE INDEX IF NOT EXISTS ix_{_options.TableName}_expires
				ON {_options.FullTableName} (is_active, expires_at)
				WHERE is_active = TRUE AND expires_at IS NOT NULL";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(new CommandDefinition(createSchemaSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);
		_ = await connection.ExecuteAsync(new CommandDefinition(createTableSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);
		_ = await connection.ExecuteAsync(new CommandDefinition(createIndexesSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);

		LogSchemaEnsured();
	}

	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Dapper materializes this type.")]
	private sealed class LegalHoldRow
	{
		// ReSharper disable InconsistentNaming - Column names use snake_case
		public Guid hold_id { get; init; }
		public string? data_subject_id_hash { get; init; }
		public int? id_type { get; init; }
		public string? tenant_id { get; init; }
		public int basis { get; init; }
		public string case_reference { get; init; } = string.Empty;
		public string description { get; init; } = string.Empty;
		public bool is_active { get; init; }
		public DateTimeOffset? expires_at { get; init; }
		public string created_by { get; init; } = string.Empty;
		public DateTimeOffset created_at { get; init; }
		public string? released_by { get; init; }
		public DateTimeOffset? released_at { get; init; }
		public string? release_reason { get; init; }
		// ReSharper restore InconsistentNaming

		public LegalHold ToLegalHold() => new()
		{
			HoldId = hold_id,
			DataSubjectIdHash = data_subject_id_hash,
			IdType = id_type.HasValue ? (DataSubjectIdType)id_type.Value : null,
			TenantId = tenant_id,
			Basis = (LegalHoldBasis)basis,
			CaseReference = case_reference,
			Description = description,
			IsActive = is_active,
			ExpiresAt = expires_at,
			CreatedBy = created_by,
			CreatedAt = created_at,
			ReleasedBy = released_by,
			ReleasedAt = released_at,
			ReleaseReason = release_reason
		};
	}
}
