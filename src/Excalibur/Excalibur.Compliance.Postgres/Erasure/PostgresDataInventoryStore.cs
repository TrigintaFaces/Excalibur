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
/// Postgres implementation of <see cref="IDataInventoryStore"/> and <see cref="IDataInventoryQueryStore"/> using Dapper.
/// </summary>
/// <remarks>
/// This store provides:
/// <list type="bullet">
/// <item>Persistence of data location registrations for GDPR compliance</item>
/// <item>Recording of discovered personal data locations per data subject</item>
/// <item>Query operations for RoPA (Records of Processing Activities) reporting</item>
/// <item>Support for automatic and manual data discovery</item>
/// </list>
/// </remarks>
public sealed partial class PostgresDataInventoryStore : IDataInventoryStore, IDataInventoryQueryStore
{
	private readonly PostgresDataInventoryStoreOptions _options;
	private readonly ILogger<PostgresDataInventoryStore> _logger;
	private volatile bool _initialized;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresDataInventoryStore"/> class.
	/// </summary>
	public PostgresDataInventoryStore(
		IOptions<PostgresDataInventoryStoreOptions> options,
		ILogger<PostgresDataInventoryStore> logger)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		_options.Validate();
	}

	/// <inheritdoc />
	public async Task SaveRegistrationAsync(
		DataLocationRegistration registration,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(registration);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			INSERT INTO {_options.FullRegistrationsTableName}
				(table_name, field_name, data_category, data_subject_id_column, id_type,
				 key_id_column, tenant_id_column, description, created_at, updated_at)
			VALUES
				(@TableName, @FieldName, @DataCategory, @DataSubjectIdColumn, @IdType,
				 @KeyIdColumn, @TenantIdColumn, @Description, @Now, @Now)
			ON CONFLICT (table_name, field_name) DO UPDATE SET
				data_category = EXCLUDED.data_category,
				data_subject_id_column = EXCLUDED.data_subject_id_column,
				id_type = EXCLUDED.id_type,
				key_id_column = EXCLUDED.key_id_column,
				tenant_id_column = EXCLUDED.tenant_id_column,
				description = EXCLUDED.description,
				updated_at = EXCLUDED.updated_at";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(new CommandDefinition(sql, new
		{
			registration.TableName,
			registration.FieldName,
			registration.DataCategory,
			registration.DataSubjectIdColumn,
			IdType = (int)registration.IdType,
			registration.KeyIdColumn,
			registration.TenantIdColumn,
			registration.Description,
			Now = DateTimeOffset.UtcNow
		}, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		LogSavedRegistration(registration.TableName, registration.FieldName);
	}

	/// <inheritdoc />
	public async Task<bool> RemoveRegistrationAsync(
		string tableName,
		string fieldName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			DELETE FROM {_options.FullRegistrationsTableName}
			WHERE table_name = @TableName AND field_name = @FieldName";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var affected = await connection.ExecuteAsync(new CommandDefinition(sql,
			new { TableName = tableName, FieldName = fieldName },
			cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		return affected > 0;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<DataLocationRegistration>> GetAllRegistrationsAsync(
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			SELECT table_name, field_name, data_category, data_subject_id_column, id_type,
				   key_id_column, tenant_id_column, description
			FROM {_options.FullRegistrationsTableName}
			ORDER BY table_name, field_name";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<RegistrationRow>(
			new CommandDefinition(sql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		return rows.Select(r => r.ToRegistration()).ToList();
	}

	/// <inheritdoc />
	public async Task RecordDiscoveredLocationAsync(
		DataLocation location,
		string dataSubjectId,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(location);
		ArgumentException.ThrowIfNullOrWhiteSpace(dataSubjectId);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var dataSubjectIdHash = HashDataSubjectId(dataSubjectId);

		var sql = $@"
			INSERT INTO {_options.FullDiscoveredLocationsTableName}
				(data_subject_id_hash, table_name, field_name, record_id, data_category,
				 key_id, is_auto_discovered, created_at, updated_at)
			VALUES
				(@DataSubjectIdHash, @TableName, @FieldName, @RecordId, @DataCategory,
				 @KeyId, @IsAutoDiscovered, @Now, @Now)
			ON CONFLICT (data_subject_id_hash, table_name, field_name, record_id) DO UPDATE SET
				data_category = EXCLUDED.data_category,
				key_id = EXCLUDED.key_id,
				is_auto_discovered = EXCLUDED.is_auto_discovered,
				updated_at = EXCLUDED.updated_at";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(new CommandDefinition(sql, new
		{
			DataSubjectIdHash = dataSubjectIdHash,
			location.TableName,
			location.FieldName,
			location.RecordId,
			location.DataCategory,
			location.KeyId,
			location.IsAutoDiscovered,
			Now = DateTimeOffset.UtcNow
		}, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		LogRecordedLocation(location.TableName, location.FieldName, dataSubjectIdHash);
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType == typeof(IDataInventoryQueryStore))
		{
			return this;
		}

		return null;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<DataLocationRegistration>> FindRegistrationsForDataSubjectAsync(
		string dataSubjectId,
		DataSubjectIdType idType,
		string? tenantId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(dataSubjectId);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var whereClauses = new List<string> { "id_type = @IdType" };
		var parameters = new DynamicParameters();
		parameters.Add("IdType", (int)idType);

		if (!string.IsNullOrEmpty(tenantId))
		{
			whereClauses.Add("tenant_id_column IS NOT NULL");
		}

		var whereClause = string.Join(" AND ", whereClauses);

		var sql = $@"
			SELECT table_name, field_name, data_category, data_subject_id_column, id_type,
				   key_id_column, tenant_id_column, description
			FROM {_options.FullRegistrationsTableName}
			WHERE {whereClause}
			ORDER BY table_name, field_name";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<RegistrationRow>(
			new CommandDefinition(sql, parameters, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		return rows.Select(r => r.ToRegistration()).ToList();
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<DataLocation>> GetDiscoveredLocationsAsync(
		string dataSubjectId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(dataSubjectId);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			SELECT table_name, field_name, data_category, record_id, key_id, is_auto_discovered
			FROM {_options.FullDiscoveredLocationsTableName}
			WHERE data_subject_id_hash = @DataSubjectIdHash
			ORDER BY table_name, field_name";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<DiscoveredLocationRow>(
			new CommandDefinition(sql, new { DataSubjectIdHash = HashDataSubjectId(dataSubjectId) },
				cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		return rows.Select(r => r.ToDataLocation()).ToList();
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<DataMapEntry>> GetDataMapEntriesAsync(
		string? tenantId,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			SELECT r.table_name, r.field_name, r.data_category, r.description,
				   FALSE AS is_auto_discovered,
				   (SELECT COUNT(*) FROM {_options.FullDiscoveredLocationsTableName} d
				    WHERE d.table_name = r.table_name AND d.field_name = r.field_name) AS record_count
			FROM {_options.FullRegistrationsTableName} r
			{(tenantId is not null ? "WHERE r.tenant_id_column IS NOT NULL" : string.Empty)}
			ORDER BY r.table_name, r.field_name";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<DataMapEntryRow>(
			new CommandDefinition(sql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		return rows.Select(r => r.ToDataMapEntry()).ToList();
	}

	private static string HashDataSubjectId(string dataSubjectId) =>
		DataSubjectHasher.HashDataSubjectId(dataSubjectId);

	[LoggerMessage(LogLevel.Debug, "Saved data inventory registration for {TableName}.{FieldName}")]
	private partial void LogSavedRegistration(string tableName, string fieldName);

	[LoggerMessage(LogLevel.Debug, "Recorded discovered location {TableName}.{FieldName} for data subject hash {DataSubjectIdHash}")]
	private partial void LogRecordedLocation(string tableName, string fieldName, string dataSubjectIdHash);

	[LoggerMessage(LogLevel.Debug, "Ensured Postgres data inventory schema and tables exist")]
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

		var createRegistrationsTableSql = $@"
			CREATE TABLE IF NOT EXISTS {_options.FullRegistrationsTableName} (
				table_name VARCHAR(256) NOT NULL,
				field_name VARCHAR(256) NOT NULL,
				data_category VARCHAR(256) NOT NULL,
				data_subject_id_column VARCHAR(256) NOT NULL,
				id_type INT NOT NULL,
				key_id_column VARCHAR(256) NOT NULL,
				tenant_id_column VARCHAR(256) NULL,
				description VARCHAR(1000) NULL,
				created_at TIMESTAMPTZ NOT NULL,
				updated_at TIMESTAMPTZ NOT NULL,
				PRIMARY KEY (table_name, field_name)
			)";

		var createRegistrationsIndexSql = $@"
			CREATE INDEX IF NOT EXISTS ix_{_options.RegistrationsTableName}_category
				ON {_options.FullRegistrationsTableName} (data_category)";

		var createDiscoveredLocationsTableSql = $@"
			CREATE TABLE IF NOT EXISTS {_options.FullDiscoveredLocationsTableName} (
				data_subject_id_hash VARCHAR(128) NOT NULL,
				table_name VARCHAR(256) NOT NULL,
				field_name VARCHAR(256) NOT NULL,
				record_id VARCHAR(256) NOT NULL,
				data_category VARCHAR(256) NOT NULL,
				key_id VARCHAR(256) NOT NULL,
				is_auto_discovered BOOLEAN NOT NULL DEFAULT TRUE,
				created_at TIMESTAMPTZ NOT NULL,
				updated_at TIMESTAMPTZ NOT NULL,
				PRIMARY KEY (data_subject_id_hash, table_name, field_name, record_id)
			)";

		var createDiscoveredLocationsIndexesSql = $@"
			CREATE INDEX IF NOT EXISTS ix_{_options.DiscoveredLocationsTableName}_subject
				ON {_options.FullDiscoveredLocationsTableName} (data_subject_id_hash);
			CREATE INDEX IF NOT EXISTS ix_{_options.DiscoveredLocationsTableName}_table
				ON {_options.FullDiscoveredLocationsTableName} (table_name, field_name)";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(new CommandDefinition(createSchemaSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);
		_ = await connection.ExecuteAsync(new CommandDefinition(createRegistrationsTableSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);
		_ = await connection.ExecuteAsync(new CommandDefinition(createRegistrationsIndexSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);
		_ = await connection.ExecuteAsync(new CommandDefinition(createDiscoveredLocationsTableSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);
		_ = await connection.ExecuteAsync(new CommandDefinition(createDiscoveredLocationsIndexesSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);

		LogSchemaEnsured();
	}

	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Dapper materializes this type.")]
	private sealed class RegistrationRow
	{
		// ReSharper disable InconsistentNaming - Column names use snake_case
		public string table_name { get; init; } = string.Empty;
		public string field_name { get; init; } = string.Empty;
		public string data_category { get; init; } = string.Empty;
		public string data_subject_id_column { get; init; } = string.Empty;
		public int id_type { get; init; }
		public string key_id_column { get; init; } = string.Empty;
		public string? tenant_id_column { get; init; }
		public string? description { get; init; }
		// ReSharper restore InconsistentNaming

		public DataLocationRegistration ToRegistration() => new()
		{
			TableName = table_name,
			FieldName = field_name,
			DataCategory = data_category,
			DataSubjectIdColumn = data_subject_id_column,
			IdType = (DataSubjectIdType)id_type,
			KeyIdColumn = key_id_column,
			TenantIdColumn = tenant_id_column,
			Description = description
		};
	}

	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Dapper materializes this type.")]
	private sealed class DiscoveredLocationRow
	{
		// ReSharper disable InconsistentNaming - Column names use snake_case
		public string table_name { get; init; } = string.Empty;
		public string field_name { get; init; } = string.Empty;
		public string data_category { get; init; } = string.Empty;
		public string record_id { get; init; } = string.Empty;
		public string key_id { get; init; } = string.Empty;
		public bool is_auto_discovered { get; init; }
		// ReSharper restore InconsistentNaming

		public DataLocation ToDataLocation() => new()
		{
			TableName = table_name,
			FieldName = field_name,
			DataCategory = data_category,
			RecordId = record_id,
			KeyId = key_id,
			IsAutoDiscovered = is_auto_discovered
		};
	}

	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Dapper materializes this type.")]
	private sealed class DataMapEntryRow
	{
		// ReSharper disable InconsistentNaming - Column names use snake_case
		public string table_name { get; init; } = string.Empty;
		public string field_name { get; init; } = string.Empty;
		public string data_category { get; init; } = string.Empty;
		public bool is_auto_discovered { get; init; }
		public long record_count { get; init; }
		public string? description { get; init; }
		// ReSharper restore InconsistentNaming

		public DataMapEntry ToDataMapEntry() => new()
		{
			TableName = table_name,
			FieldName = field_name,
			DataCategory = data_category,
			IsAutoDiscovered = is_auto_discovered,
			RecordCount = record_count,
			Description = description
		};
	}
}
