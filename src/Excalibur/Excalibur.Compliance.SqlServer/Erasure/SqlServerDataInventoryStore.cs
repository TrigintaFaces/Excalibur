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
/// SQL Server implementation of <see cref="IDataInventoryStore"/> and <see cref="IDataInventoryQueryStore"/> using Dapper.
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
public sealed partial class SqlServerDataInventoryStore : IDataInventoryStore, IDataInventoryQueryStore
{
	private readonly SqlServerDataInventoryStoreOptions _options;
	private readonly ILogger<SqlServerDataInventoryStore> _logger;
	private volatile bool _initialized;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerDataInventoryStore"/> class.
	/// </summary>
	public SqlServerDataInventoryStore(
		IOptions<SqlServerDataInventoryStoreOptions> options,
		ILogger<SqlServerDataInventoryStore> logger)
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
			MERGE {_options.FullRegistrationsTableName} AS target
			USING (VALUES (@TableName, @FieldName)) AS source (TableName, FieldName)
			ON target.TableName = source.TableName AND target.FieldName = source.FieldName
			WHEN MATCHED THEN
				UPDATE SET DataCategory = @DataCategory,
						   DataSubjectIdColumn = @DataSubjectIdColumn,
						   IdType = @IdType,
						   KeyIdColumn = @KeyIdColumn,
						   TenantIdColumn = @TenantIdColumn,
						   Description = @Description,
						   UpdatedAt = @Now
			WHEN NOT MATCHED THEN
				INSERT (TableName, FieldName, DataCategory, DataSubjectIdColumn, IdType,
						KeyIdColumn, TenantIdColumn, Description, CreatedAt, UpdatedAt)
				VALUES (@TableName, @FieldName, @DataCategory, @DataSubjectIdColumn, @IdType,
						@KeyIdColumn, @TenantIdColumn, @Description, @Now, @Now);";

		await using var connection = new SqlConnection(_options.ConnectionString);
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
		}, cancellationToken: cancellationToken)).ConfigureAwait(false);

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
			WHERE TableName = @TableName AND FieldName = @FieldName";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var affected = await connection.ExecuteAsync(new CommandDefinition(sql,
			new { TableName = tableName, FieldName = fieldName },
			cancellationToken: cancellationToken)).ConfigureAwait(false);

		return affected > 0;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<DataLocationRegistration>> GetAllRegistrationsAsync(
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			SELECT TableName, FieldName, DataCategory, DataSubjectIdColumn, IdType,
				   KeyIdColumn, TenantIdColumn, Description
			FROM {_options.FullRegistrationsTableName}
			ORDER BY TableName, FieldName";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<RegistrationRow>(
			new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);

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
			MERGE {_options.FullDiscoveredLocationsTableName} AS target
			USING (VALUES (@DataSubjectIdHash, @TableName, @FieldName, @RecordId)) AS source
				(DataSubjectIdHash, TableName, FieldName, RecordId)
			ON target.DataSubjectIdHash = source.DataSubjectIdHash
			   AND target.TableName = source.TableName
			   AND target.FieldName = source.FieldName
			   AND target.RecordId = source.RecordId
			WHEN MATCHED THEN
				UPDATE SET DataCategory = @DataCategory,
						   KeyId = @KeyId,
						   IsAutoDiscovered = @IsAutoDiscovered,
						   UpdatedAt = @Now
			WHEN NOT MATCHED THEN
				INSERT (DataSubjectIdHash, TableName, FieldName, RecordId, DataCategory,
						KeyId, IsAutoDiscovered, CreatedAt, UpdatedAt)
				VALUES (@DataSubjectIdHash, @TableName, @FieldName, @RecordId, @DataCategory,
						@KeyId, @IsAutoDiscovered, @Now, @Now);";

		await using var connection = new SqlConnection(_options.ConnectionString);
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
		}, cancellationToken: cancellationToken)).ConfigureAwait(false);

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

		var whereClauses = new List<string> { "IdType = @IdType" };
		var parameters = new DynamicParameters();
		parameters.Add("IdType", (int)idType);

		if (!string.IsNullOrEmpty(tenantId))
		{
			whereClauses.Add("TenantIdColumn IS NOT NULL");
		}

		var whereClause = string.Join(" AND ", whereClauses);

		var sql = $@"
			SELECT TableName, FieldName, DataCategory, DataSubjectIdColumn, IdType,
				   KeyIdColumn, TenantIdColumn, Description
			FROM {_options.FullRegistrationsTableName}
			WHERE {whereClause}
			ORDER BY TableName, FieldName";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<RegistrationRow>(
			new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)).ConfigureAwait(false);

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
			SELECT TableName, FieldName, DataCategory, RecordId, KeyId, IsAutoDiscovered
			FROM {_options.FullDiscoveredLocationsTableName}
			WHERE DataSubjectIdHash = @DataSubjectIdHash
			ORDER BY TableName, FieldName";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<DiscoveredLocationRow>(
			new CommandDefinition(sql, new { DataSubjectIdHash = HashDataSubjectId(dataSubjectId) },
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		return rows.Select(r => r.ToDataLocation()).ToList();
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<DataMapEntry>> GetDataMapEntriesAsync(
		string? tenantId,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Build data map from registrations + discovered locations count
		var sql = $@"
			SELECT r.TableName, r.FieldName, r.DataCategory, r.Description,
				   CAST(0 AS BIT) AS IsAutoDiscovered,
				   (SELECT COUNT(*) FROM {_options.FullDiscoveredLocationsTableName} d
				    WHERE d.TableName = r.TableName AND d.FieldName = r.FieldName) AS RecordCount
			FROM {_options.FullRegistrationsTableName} r
			{(tenantId is not null ? "WHERE r.TenantIdColumn IS NOT NULL" : string.Empty)}
			ORDER BY r.TableName, r.FieldName";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<DataMapEntryRow>(
			new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);

		return rows.Select(r => r.ToDataMapEntry()).ToList();
	}

	private static string HashDataSubjectId(string dataSubjectId) =>
		DataSubjectHasher.HashDataSubjectId(dataSubjectId);

	[LoggerMessage(LogLevel.Debug, "Saved data inventory registration for {TableName}.{FieldName}")]
	private partial void LogSavedRegistration(string tableName, string fieldName);

	[LoggerMessage(LogLevel.Debug, "Recorded discovered location {TableName}.{FieldName} for data subject hash {DataSubjectIdHash}")]
	private partial void LogRecordedLocation(string tableName, string fieldName, string dataSubjectIdHash);

	[LoggerMessage(LogLevel.Debug, "Ensured SQL Server data inventory schema and tables exist")]
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

		var createRegistrationsTableSql = $@"
			IF NOT EXISTS (SELECT 1 FROM sys.tables t
				JOIN sys.schemas s ON t.schema_id = s.schema_id
				WHERE s.name = '{_options.SchemaName}' AND t.name = '{_options.RegistrationsTableName}')
			BEGIN
				CREATE TABLE {_options.FullRegistrationsTableName} (
					TableName NVARCHAR(256) NOT NULL,
					FieldName NVARCHAR(256) NOT NULL,
					DataCategory NVARCHAR(256) NOT NULL,
					DataSubjectIdColumn NVARCHAR(256) NOT NULL,
					IdType INT NOT NULL,
					KeyIdColumn NVARCHAR(256) NOT NULL,
					TenantIdColumn NVARCHAR(256) NULL,
					Description NVARCHAR(1000) NULL,
					CreatedAt DATETIMEOFFSET NOT NULL,
					UpdatedAt DATETIMEOFFSET NOT NULL,
					CONSTRAINT PK_{_options.RegistrationsTableName} PRIMARY KEY (TableName, FieldName),
					INDEX IX_{_options.RegistrationsTableName}_DataCategory (DataCategory)
				)
			END";

		var createDiscoveredLocationsTableSql = $@"
			IF NOT EXISTS (SELECT 1 FROM sys.tables t
				JOIN sys.schemas s ON t.schema_id = s.schema_id
				WHERE s.name = '{_options.SchemaName}' AND t.name = '{_options.DiscoveredLocationsTableName}')
			BEGIN
				CREATE TABLE {_options.FullDiscoveredLocationsTableName} (
					DataSubjectIdHash NVARCHAR(128) NOT NULL,
					TableName NVARCHAR(256) NOT NULL,
					FieldName NVARCHAR(256) NOT NULL,
					RecordId NVARCHAR(256) NOT NULL,
					DataCategory NVARCHAR(256) NOT NULL,
					KeyId NVARCHAR(256) NOT NULL,
					IsAutoDiscovered BIT NOT NULL DEFAULT 1,
					CreatedAt DATETIMEOFFSET NOT NULL,
					UpdatedAt DATETIMEOFFSET NOT NULL,
					CONSTRAINT PK_{_options.DiscoveredLocationsTableName}
						PRIMARY KEY (DataSubjectIdHash, TableName, FieldName, RecordId),
					INDEX IX_{_options.DiscoveredLocationsTableName}_DataSubject (DataSubjectIdHash),
					INDEX IX_{_options.DiscoveredLocationsTableName}_Table (TableName, FieldName)
				)
			END";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(new CommandDefinition(createSchemaSql, cancellationToken: cancellationToken))
			.ConfigureAwait(false);
		_ = await connection.ExecuteAsync(new CommandDefinition(createRegistrationsTableSql, cancellationToken: cancellationToken))
			.ConfigureAwait(false);
		_ = await connection.ExecuteAsync(new CommandDefinition(createDiscoveredLocationsTableSql, cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		LogSchemaEnsured();
	}

	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Dapper materializes this type.")]
	private sealed class RegistrationRow
	{
		public string TableName { get; init; } = string.Empty;
		public string FieldName { get; init; } = string.Empty;
		public string DataCategory { get; init; } = string.Empty;
		public string DataSubjectIdColumn { get; init; } = string.Empty;
		public int IdType { get; init; }
		public string KeyIdColumn { get; init; } = string.Empty;
		public string? TenantIdColumn { get; init; }
		public string? Description { get; init; }

		public DataLocationRegistration ToRegistration() => new()
		{
			TableName = TableName,
			FieldName = FieldName,
			DataCategory = DataCategory,
			DataSubjectIdColumn = DataSubjectIdColumn,
			IdType = (DataSubjectIdType)IdType,
			KeyIdColumn = KeyIdColumn,
			TenantIdColumn = TenantIdColumn,
			Description = Description
		};
	}

	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Dapper materializes this type.")]
	private sealed class DiscoveredLocationRow
	{
		public string TableName { get; init; } = string.Empty;
		public string FieldName { get; init; } = string.Empty;
		public string DataCategory { get; init; } = string.Empty;
		public string RecordId { get; init; } = string.Empty;
		public string KeyId { get; init; } = string.Empty;
		public bool IsAutoDiscovered { get; init; }

		public DataLocation ToDataLocation() => new()
		{
			TableName = TableName,
			FieldName = FieldName,
			DataCategory = DataCategory,
			RecordId = RecordId,
			KeyId = KeyId,
			IsAutoDiscovered = IsAutoDiscovered
		};
	}

	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Dapper materializes this type.")]
	private sealed class DataMapEntryRow
	{
		public string TableName { get; init; } = string.Empty;
		public string FieldName { get; init; } = string.Empty;
		public string DataCategory { get; init; } = string.Empty;
		public bool IsAutoDiscovered { get; init; }
		public long RecordCount { get; init; }
		public string? Description { get; init; }

		public DataMapEntry ToDataMapEntry() => new()
		{
			TableName = TableName,
			FieldName = FieldName,
			DataCategory = DataCategory,
			IsAutoDiscovered = IsAutoDiscovered,
			RecordCount = RecordCount,
			Description = Description
		};
	}
}
