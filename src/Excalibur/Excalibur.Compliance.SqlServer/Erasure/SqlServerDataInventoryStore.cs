// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Dapper;

using Excalibur.Compliance.Erasure;
using Excalibur.Data.Validation;
using Excalibur.Dispatch;

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
public sealed partial class SqlServerDataInventoryStore : IDataInventoryStore, IDataInventoryQueryStore, IDisposable
{
	private readonly SqlServerDataInventoryStoreOptions _options;
	private readonly IDataSubjectHasher _dataSubjectHasher;
	private readonly ITenantContext? _tenantContext;
	private readonly ILogger<SqlServerDataInventoryStore> _logger;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private bool _disposed;
	private volatile bool _initialized;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerDataInventoryStore"/> class.
	/// </summary>
	/// <param name="options">The store options.</param>
	/// <param name="dataSubjectHasher">The data-subject hasher.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context, or <see langword="null"/> when multi-tenancy is not registered. Optional
	/// and last so existing callers keep compiling; a host without multi-tenancy resolves the reserved
	/// untenanted partition rather than an absent tenant term.
	/// </param>
	public SqlServerDataInventoryStore(
		IOptions<SqlServerDataInventoryStoreOptions> options,
		IDataSubjectHasher dataSubjectHasher,
		ILogger<SqlServerDataInventoryStore> logger,
		ITenantContext? tenantContext = null)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_dataSubjectHasher = dataSubjectHasher ?? throw new ArgumentNullException(nameof(dataSubjectHasher));
		_tenantContext = tenantContext;
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		_options.Validate();

		// Defense-in-depth: validate SQL identifiers even if IValidateOptions ran at startup
		SqlIdentifierValidator.ThrowIfInvalid(_options.SchemaName, nameof(_options.SchemaName));
		SqlIdentifierValidator.ThrowIfInvalid(_options.RegistrationsTableName, nameof(_options.RegistrationsTableName));
		SqlIdentifierValidator.ThrowIfInvalid(_options.DiscoveredLocationsTableName, nameof(_options.DiscoveredLocationsTableName));
	}

	/// <summary>
	/// Resolves the tenant term every read and write of this store is confined to.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Resolved from ambient context per call rather than fixed at construction: the store is a singleton
	/// and a construction-time capture would bind every caller to whichever tenant happened to be current
	/// when the container built it.
	/// </para>
	/// <para>
	/// This is the tenant VALUE. It is unrelated to <c>TenantIdColumn</c>, which is the NAME of a column in
	/// the consumer's own table — the two were previously conflated, and that conflation is why a caller
	/// supplying a tenant received every tenant's registrations: the supplied value was used as a
	/// null-check on a column name and never bound as a term.
	/// </para>
	/// </remarks>
	private string CurrentTenantTerm =>
		KeyedTenantPartition.FromScope(TenantScope.FromContext(_tenantContext)).TenantId;

	/// <inheritdoc />
	public async Task SaveRegistrationAsync(
		DataLocationRegistration registration,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(registration);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			MERGE {_options.FullRegistrationsTableName} AS target
			USING (VALUES (@TableName, @FieldName, @TenantId)) AS source (TableName, FieldName, TenantId)
			ON target.TableName = source.TableName
			   AND target.FieldName = source.FieldName
			   AND target.TenantId = source.TenantId
			WHEN MATCHED THEN
				UPDATE SET DataCategory = @DataCategory,
						   DataSubjectIdColumn = @DataSubjectIdColumn,
						   IdType = @IdType,
						   KeyIdColumn = @KeyIdColumn,
						   TenantIdColumn = @TenantIdColumn,
						   Description = @Description,
						   UpdatedAt = @Now
			WHEN NOT MATCHED THEN
				INSERT (TableName, FieldName, TenantId, DataCategory, DataSubjectIdColumn, IdType,
						KeyIdColumn, TenantIdColumn, Description, CreatedAt, UpdatedAt)
				VALUES (@TableName, @FieldName, @TenantId, @DataCategory, @DataSubjectIdColumn, @IdType,
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
			// The tenant this registration BELONGS to, bound as a value and part of the merge key above.
			// Taken from ambient scope rather than from the registration, so a caller cannot write into
			// another tenant's partition by populating the field.
			TenantId = CurrentTenantTerm,
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

		// The tenant term is on the DELETE, and this is the most consequential predicate in the file.
		// Without it, deregistering a field removes EVERY tenant's registration for that table and field,
		// not merely the caller's: cross-tenant destruction from an ordinary public method. And because a
		// registration is what the erasure path uses to know a field holds personal data, destroying
		// another tenant's row silently removes that field from their erasure coverage — their next
		// erasure reports success and never visits it.
		var sql = $@"
			DELETE FROM {_options.FullRegistrationsTableName}
			WHERE TableName = @TableName AND FieldName = @FieldName AND TenantId = @ScopedTenantId";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var affected = await connection.ExecuteAsync(new CommandDefinition(sql,
			new { TableName = tableName, FieldName = fieldName, ScopedTenantId = CurrentTenantTerm },
			cancellationToken: cancellationToken)).ConfigureAwait(false);

		return affected > 0;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<DataLocationRegistration>> GetAllRegistrationsAsync(
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// "GetAll" means all of the CALLER'S — never all of everyone's. This query previously carried no
		// WHERE clause whatsoever, so a single call returned every tenant's registrations: the whole
		// compliance inventory of the estate, from a method whose name invites exactly that call.
		var sql = $@"
			SELECT TableName, FieldName, DataCategory, DataSubjectIdColumn, IdType,
				   KeyIdColumn, TenantIdColumn, Description
			FROM {_options.FullRegistrationsTableName}
			WHERE TenantId IN (@ScopedTenantId, @UntenantedTenantId)
			ORDER BY TableName, FieldName";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<RegistrationRow>(
			new CommandDefinition(sql,
				new { ScopedTenantId = CurrentTenantTerm, UntenantedTenantId = TenantScope.UntenantedSentinel },
				cancellationToken: cancellationToken)).ConfigureAwait(false);

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
			USING (VALUES (@DataSubjectIdHash, @TableName, @FieldName, @RecordId, @TenantId)) AS source
				(DataSubjectIdHash, TableName, FieldName, RecordId, TenantId)
			ON target.DataSubjectIdHash = source.DataSubjectIdHash
			   AND target.TableName = source.TableName
			   AND target.FieldName = source.FieldName
			   AND target.RecordId = source.RecordId
			   AND target.TenantId = source.TenantId
			WHEN MATCHED THEN
				UPDATE SET DataCategory = @DataCategory,
						   KeyId = @KeyId,
						   IsAutoDiscovered = @IsAutoDiscovered,
						   UpdatedAt = @Now
			WHEN NOT MATCHED THEN
				INSERT (DataSubjectIdHash, TableName, FieldName, RecordId, TenantId, DataCategory,
						KeyId, IsAutoDiscovered, CreatedAt, UpdatedAt)
				VALUES (@DataSubjectIdHash, @TableName, @FieldName, @RecordId, @TenantId, @DataCategory,
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
			// Part of the merge key above. A discovered location is evidence about one tenant's data
			// subject; without the tenant in the key, two tenants discovering the same record collapse
			// into one row and the second write overwrites the first tenant's finding.
			TenantId = CurrentTenantTerm,
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

		// The tenant term is a SCOPE, added UNCONDITIONALLY from ambient context — not a filter the caller
		// opts into. It previously sat behind `if (tenantId is not null)` and, when present, added
		// `TenantIdColumn IS NOT NULL`: a null-check on a COLUMN NAME. The caller's tenant was never bound,
		// so passing one changed nothing and omitting one changed nothing — every caller read every tenant.
		// Untenanted registrations are included alongside the caller's own. A registration is schema
		// metadata — it names a table, a field and a category, never a person — so an untenanted one
		// discloses nothing about another tenant. Excluding it is the harmful direction: these rows ARE the
		// sweep list the erasure path walks, so a registration the scope cannot see is a field that is
		// never erased and never reported as missed.
		//
		// This widening is REGISTRATIONS ONLY. Discovered locations, erasure requests and legal holds are
		// subject-linked and stay on strict equality.
		whereClauses.Add("TenantId IN (@ScopedTenantId, @UntenantedTenantId)");
		parameters.Add("ScopedTenantId", CurrentTenantTerm);
		parameters.Add("UntenantedTenantId", TenantScope.UntenantedSentinel);

		// The tenantId ARGUMENT is deliberately not consulted, matching the audit stores' settled contract:
		// a caller cannot widen the read by omitting it, nor redirect the read by naming another tenant.
		// There is no admin or estate-wide inventory interface in this framework, so there is no contract
		// under which an unchecked caller-supplied tenant would be legitimate. The parameter remains on the
		// shipped signature; honouring it would reintroduce exactly the authorisation hole this closes.
		_ = tenantId;

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
			WHERE DataSubjectIdHash = @DataSubjectIdHash AND TenantId = @ScopedTenantId
			ORDER BY TableName, FieldName";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<DiscoveredLocationRow>(
			new CommandDefinition(sql, new { DataSubjectIdHash = HashDataSubjectId(dataSubjectId), ScopedTenantId = CurrentTenantTerm },
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
				   -- The correlated count is scoped too. Correlating only on table and field made RecordCount
				   -- the number of discovered records ACROSS EVERY TENANT for that field, so a RoPA report
				   -- disclosed the volume of other tenants' personal data holdings — a count is smaller than
				   -- a row and still information about another tenant's data.
				   (SELECT COUNT(*) FROM {_options.FullDiscoveredLocationsTableName} d
				    WHERE d.TableName = r.TableName AND d.FieldName = r.FieldName
				      AND d.TenantId = r.TenantId) AS RecordCount
			FROM {_options.FullRegistrationsTableName} r
			WHERE r.TenantId IN (@ScopedTenantId, @UntenantedTenantId)
			ORDER BY r.TableName, r.FieldName";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// Both terms MUST be bound. The scoping predicate was added to this query without its parameters,
		// so every call threw "must declare the scalar variable" against a real server — a RoPA data map
		// that cannot be produced at all. No unit test caught it because none of them reach a database.
		var rows = await connection.QueryAsync<DataMapEntryRow>(
			new CommandDefinition(sql,
				new { ScopedTenantId = CurrentTenantTerm, UntenantedTenantId = TenantScope.UntenantedSentinel },
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		return rows.Select(r => r.ToDataMapEntry()).ToList();
	}

	private string HashDataSubjectId(string dataSubjectId) =>
		_dataSubjectHasher.HashDataSubjectId(dataSubjectId);

	[LoggerMessage(LogLevel.Debug, "Saved data inventory registration for {TableName}.{FieldName}")]
	private partial void LogSavedRegistration(string tableName, string fieldName);

	[LoggerMessage(LogLevel.Debug, "Recorded discovered location {TableName}.{FieldName} for data subject hash {DataSubjectIdHash}")]
	private partial void LogRecordedLocation(string tableName, string fieldName, string dataSubjectIdHash);

	[LoggerMessage(LogLevel.Debug, "Ensured SQL Server data inventory schema and tables exist")]
	private partial void LogSchemaEnsured();

	/// <summary>
	/// Provisions the schema once, however many callers arrive together.
	/// </summary>
	/// <remarks>
	/// Without the lock every concurrent first caller ran the provisioning body: the flag is only
	/// set after the work completes, so each of them reads it as false and proceeds. The DDL is
	/// written to be idempotent, but concurrent CREATE ... IF NOT EXISTS statements can still
	/// collide in the catalog, and a body that assigns more than one field would leave a later
	/// caller reading a field its predecessor had not reached yet. The re-check inside the lock is
	/// what makes it exactly once rather than merely serialised.
	/// </remarks>
	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_initialized)
		{
			return;
		}

		await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (_initialized)
			{
				return;
			}

			await InitializeCoreAsync(cancellationToken).ConfigureAwait(false);
			_initialized = true;
		}
		finally
		{
			_ = _initLock.Release();
		}
	}

	/// <summary>
	/// Releases the initialisation lock.
	/// </summary>
	/// <remarks>
	/// The flag is set before anything is released, so a caller that races disposal is refused by
	/// the guard above rather than reaching a half-torn-down store. This mirrors how the framework
	/// disposes its own lazily-connected caches.
	/// </remarks>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_initLock.Dispose();
	}

	private async Task InitializeCoreAsync(CancellationToken cancellationToken)
	{
		if (_options.AutoCreateSchema)
		{
			await CreateSchemaIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
		}
		else
		{
			await VerifySchemaExistsAsync(cancellationToken).ConfigureAwait(false);
		}

	}

	/// <summary>
	/// Confirms the required tables exist when automatic provisioning is disabled.
	/// </summary>
	/// <remarks>
	/// Initialization must never complete without either creating the schema or verifying it. Marking the store
	/// initialized after doing neither would defer the failure to the first query, where it surfaces as a raw
	/// provider error far from its cause. This method is the verification half of that guarantee.
	/// </remarks>
	/// <exception cref="InvalidOperationException">A required table is absent.</exception>
	private async Task VerifySchemaExistsAsync(CancellationToken cancellationToken)
	{
		const string ExistsSql = "SELECT CASE WHEN OBJECT_ID(@TableName, 'U') IS NULL THEN 0 ELSE 1 END";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		foreach (var tableName in new[] { _options.FullRegistrationsTableName, _options.FullDiscoveredLocationsTableName })
		{
			var exists = await connection.ExecuteScalarAsync<bool>(
				new CommandDefinition(
					ExistsSql,
					new { TableName = tableName },
					cancellationToken: cancellationToken,
					commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

			if (!exists)
			{
				throw new InvalidOperationException(
					$"Required table '{tableName}' does not exist and automatic schema creation is disabled. " +
					$"Either create the schema out of band, or set {nameof(SqlServerDataInventoryStoreOptions)}."
					+ $"{nameof(SqlServerDataInventoryStoreOptions.AutoCreateSchema)} to true to provision it on startup.");
			}
		}
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
					-- The NAME of a tenant column in the consumer's own table. Nullable because a consumer's
					-- table may genuinely have none. NOT a tenant identity — see TenantId below.
					TenantIdColumn NVARCHAR(256) NULL,
					-- The tenant this registration BELONGS to. NOT NULL with an explicit sentinel default:
					-- a nullable tenant makes global and forgot-to-set indistinguishable, and the store
					-- cannot tell which one it is holding.
					TenantId NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL
						CONSTRAINT DF_{_options.RegistrationsTableName}_TenantId DEFAULT '__untenanted__',
					Description NVARCHAR(1000) NULL,
					CreatedAt DATETIMEOFFSET NOT NULL,
					UpdatedAt DATETIMEOFFSET NOT NULL,
					-- TenantId is part of the KEY, not merely a column: without it two tenants registering
					-- the same table and field are ONE row, and the second write silently destroys the
					-- first — taking with it the erasure path's only record that the field exists.
					CONSTRAINT PK_{_options.RegistrationsTableName} PRIMARY KEY (TableName, FieldName, TenantId),
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
					-- The tenant this discovered location belongs to. NOT NULL with an explicit sentinel,
					-- for the same reason as the registrations table: a nullable tenant cannot distinguish
					-- global from forgot-to-set.
					TenantId NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL
						CONSTRAINT DF_{_options.DiscoveredLocationsTableName}_TenantId DEFAULT '__untenanted__',
					CreatedAt DATETIMEOFFSET NOT NULL,
					UpdatedAt DATETIMEOFFSET NOT NULL,
					-- TenantId is in the KEY: two tenants discovering the same record for the same data
					-- subject are two distinct findings, not one overwriting the other.
					CONSTRAINT PK_{_options.DiscoveredLocationsTableName}
						PRIMARY KEY (DataSubjectIdHash, TableName, FieldName, RecordId, TenantId),
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
