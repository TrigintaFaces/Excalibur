// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Dapper;

using Excalibur.Dispatch.Compliance;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.SqlServer.Erasure;

/// <summary>
/// SQL Server implementation of <see cref="ILegalHoldStore"/> and <see cref="ILegalHoldQueryStore"/> using Dapper.
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
public sealed partial class SqlServerLegalHoldStore : ILegalHoldStore, ILegalHoldQueryStore
{
	private readonly SqlServerLegalHoldStoreOptions _options;
	private readonly ILogger<SqlServerLegalHoldStore> _logger;
	private volatile bool _initialized;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerLegalHoldStore"/> class.
	/// </summary>
	public SqlServerLegalHoldStore(
		IOptions<SqlServerLegalHoldStoreOptions> options,
		ILogger<SqlServerLegalHoldStore> logger)
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
				(HoldId, DataSubjectIdHash, IdType, TenantId, Basis, CaseReference,
				 Description, IsActive, ExpiresAt, CreatedBy, CreatedAt,
				 ReleasedBy, ReleasedAt, ReleaseReason)
			VALUES
				(@HoldId, @DataSubjectIdHash, @IdType, @TenantId, @Basis, @CaseReference,
				 @Description, @IsActive, @ExpiresAt, @CreatedBy, @CreatedAt,
				 @ReleasedBy, @ReleasedAt, @ReleaseReason)";

		await using var connection = new SqlConnection(_options.ConnectionString);
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
		}, cancellationToken: cancellationToken)).ConfigureAwait(false);

		LogSavedHold(hold.HoldId, hold.CaseReference);
	}

	/// <inheritdoc />
	public async Task<LegalHold?> GetHoldAsync(
		Guid holdId,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			SELECT HoldId, DataSubjectIdHash, IdType, TenantId, Basis, CaseReference,
				   Description, IsActive, ExpiresAt, CreatedBy, CreatedAt,
				   ReleasedBy, ReleasedAt, ReleaseReason
			FROM {_options.FullTableName}
			WHERE HoldId = @HoldId";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var row = await connection.QuerySingleOrDefaultAsync<LegalHoldRow>(
				new CommandDefinition(sql, new { HoldId = holdId }, cancellationToken: cancellationToken))
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
			SET DataSubjectIdHash = @DataSubjectIdHash,
				IdType = @IdType,
				TenantId = @TenantId,
				Basis = @Basis,
				CaseReference = @CaseReference,
				Description = @Description,
				IsActive = @IsActive,
				ExpiresAt = @ExpiresAt,
				ReleasedBy = @ReleasedBy,
				ReleasedAt = @ReleasedAt,
				ReleaseReason = @ReleaseReason
			WHERE HoldId = @HoldId";

		await using var connection = new SqlConnection(_options.ConnectionString);
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
		}, cancellationToken: cancellationToken)).ConfigureAwait(false);

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
				SELECT HoldId, DataSubjectIdHash, IdType, TenantId, Basis, CaseReference,
					   Description, IsActive, ExpiresAt, CreatedBy, CreatedAt,
					   ReleasedBy, ReleasedAt, ReleaseReason
				FROM {_options.FullTableName}
				WHERE DataSubjectIdHash = @DataSubjectIdHash
				  AND TenantId = @TenantId
				  AND IsActive = 1
				ORDER BY CreatedAt DESC"
			: $@"
				SELECT HoldId, DataSubjectIdHash, IdType, TenantId, Basis, CaseReference,
					   Description, IsActive, ExpiresAt, CreatedBy, CreatedAt,
					   ReleasedBy, ReleasedAt, ReleaseReason
				FROM {_options.FullTableName}
				WHERE DataSubjectIdHash = @DataSubjectIdHash
				  AND IsActive = 1
				ORDER BY CreatedAt DESC";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<LegalHoldRow>(
			new CommandDefinition(sql, new { DataSubjectIdHash = dataSubjectIdHash, TenantId = tenantId },
				cancellationToken: cancellationToken)).ConfigureAwait(false);

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
			SELECT HoldId, DataSubjectIdHash, IdType, TenantId, Basis, CaseReference,
				   Description, IsActive, ExpiresAt, CreatedBy, CreatedAt,
				   ReleasedBy, ReleasedAt, ReleaseReason
			FROM {_options.FullTableName}
			WHERE TenantId = @TenantId
			  AND IsActive = 1
			ORDER BY CreatedAt DESC";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<LegalHoldRow>(
			new CommandDefinition(sql, new { TenantId = tenantId },
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		return rows.Select(r => r.ToLegalHold()).ToList();
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<LegalHold>> ListActiveHoldsAsync(
		string? tenantId,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var whereClauses = new List<string> { "IsActive = 1" };
		var parameters = new DynamicParameters();

		if (!string.IsNullOrEmpty(tenantId))
		{
			whereClauses.Add("TenantId = @TenantId");
			parameters.Add("TenantId", tenantId);
		}

		var whereClause = string.Join(" AND ", whereClauses);

		var sql = $@"
			SELECT HoldId, DataSubjectIdHash, IdType, TenantId, Basis, CaseReference,
				   Description, IsActive, ExpiresAt, CreatedBy, CreatedAt,
				   ReleasedBy, ReleasedAt, ReleaseReason
			FROM {_options.FullTableName}
			WHERE {whereClause}
			ORDER BY CreatedAt DESC";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<LegalHoldRow>(
			new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)).ConfigureAwait(false);

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
			whereClauses.Add("TenantId = @TenantId");
			parameters.Add("TenantId", tenantId);
		}

		if (fromDate.HasValue)
		{
			whereClauses.Add("CreatedAt >= @FromDate");
			parameters.Add("FromDate", fromDate.Value);
		}

		if (toDate.HasValue)
		{
			whereClauses.Add("CreatedAt <= @ToDate");
			parameters.Add("ToDate", toDate.Value);
		}

		var whereClause = whereClauses.Count > 0
			? "WHERE " + string.Join(" AND ", whereClauses)
			: string.Empty;

		var sql = $@"
			SELECT HoldId, DataSubjectIdHash, IdType, TenantId, Basis, CaseReference,
				   Description, IsActive, ExpiresAt, CreatedBy, CreatedAt,
				   ReleasedBy, ReleasedAt, ReleaseReason
			FROM {_options.FullTableName}
			{whereClause}
			ORDER BY CreatedAt DESC";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<LegalHoldRow>(
			new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)).ConfigureAwait(false);

		return rows.Select(r => r.ToLegalHold()).ToList();
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<LegalHold>> GetExpiredHoldsAsync(
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			SELECT HoldId, DataSubjectIdHash, IdType, TenantId, Basis, CaseReference,
				   Description, IsActive, ExpiresAt, CreatedBy, CreatedAt,
				   ReleasedBy, ReleasedAt, ReleaseReason
			FROM {_options.FullTableName}
			WHERE IsActive = 1
			  AND ExpiresAt IS NOT NULL
			  AND ExpiresAt <= @Now
			ORDER BY ExpiresAt";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<LegalHoldRow>(
			new CommandDefinition(sql, new { Now = DateTimeOffset.UtcNow },
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		return rows.Select(r => r.ToLegalHold()).ToList();
	}

	[LoggerMessage(LogLevel.Debug, "Saved legal hold {HoldId} for case {CaseReference}")]
	private partial void LogSavedHold(Guid holdId, string caseReference);

	[LoggerMessage(LogLevel.Debug, "Ensured SQL Server legal hold schema and tables exist")]
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
		var createSchemaSql = $@"
			IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = '{_options.SchemaName}')
			BEGIN
				EXEC('CREATE SCHEMA [{_options.SchemaName}]')
			END";

		var createTableSql = $@"
			IF NOT EXISTS (SELECT 1 FROM sys.tables t
				JOIN sys.schemas s ON t.schema_id = s.schema_id
				WHERE s.name = '{_options.SchemaName}' AND t.name = '{_options.TableName}')
			BEGIN
				CREATE TABLE {_options.FullTableName} (
					HoldId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
					DataSubjectIdHash NVARCHAR(128) NULL,
					IdType INT NULL,
					TenantId NVARCHAR(256) NULL,
					Basis INT NOT NULL,
					CaseReference NVARCHAR(256) NOT NULL,
					Description NVARCHAR(2000) NOT NULL,
					IsActive BIT NOT NULL DEFAULT 1,
					ExpiresAt DATETIMEOFFSET NULL,
					CreatedBy NVARCHAR(256) NOT NULL,
					CreatedAt DATETIMEOFFSET NOT NULL,
					ReleasedBy NVARCHAR(256) NULL,
					ReleasedAt DATETIMEOFFSET NULL,
					ReleaseReason NVARCHAR(1000) NULL,
					INDEX IX_{_options.TableName}_DataSubject (DataSubjectIdHash, IsActive),
					INDEX IX_{_options.TableName}_TenantId (TenantId, IsActive),
					INDEX IX_{_options.TableName}_ExpiresAt (IsActive, ExpiresAt) WHERE IsActive = 1 AND ExpiresAt IS NOT NULL
				)
			END";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(new CommandDefinition(createSchemaSql, cancellationToken: cancellationToken))
			.ConfigureAwait(false);
		_ = await connection.ExecuteAsync(new CommandDefinition(createTableSql, cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		LogSchemaEnsured();
	}

	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Dapper materializes this type.")]
	private sealed class LegalHoldRow
	{
		public Guid HoldId { get; init; }
		public string? DataSubjectIdHash { get; init; }
		public int? IdType { get; init; }
		public string? TenantId { get; init; }
		public int Basis { get; init; }
		public string CaseReference { get; init; } = string.Empty;
		public string Description { get; init; } = string.Empty;
		public bool IsActive { get; init; }
		public DateTimeOffset? ExpiresAt { get; init; }
		public string CreatedBy { get; init; } = string.Empty;
		public DateTimeOffset CreatedAt { get; init; }
		public string? ReleasedBy { get; init; }
		public DateTimeOffset? ReleasedAt { get; init; }
		public string? ReleaseReason { get; init; }

		public LegalHold ToLegalHold() => new()
		{
			HoldId = HoldId,
			DataSubjectIdHash = DataSubjectIdHash,
			IdType = IdType.HasValue ? (DataSubjectIdType)IdType.Value : null,
			TenantId = TenantId,
			Basis = (LegalHoldBasis)Basis,
			CaseReference = CaseReference,
			Description = Description,
			IsActive = IsActive,
			ExpiresAt = ExpiresAt,
			CreatedBy = CreatedBy,
			CreatedAt = CreatedAt,
			ReleasedBy = ReleasedBy,
			ReleasedAt = ReleasedAt,
			ReleaseReason = ReleaseReason
		};
	}
}
