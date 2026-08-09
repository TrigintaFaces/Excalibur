// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Dapper;

using Excalibur.Dispatch;

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
public sealed partial class PostgresLegalHoldStore : ILegalHoldStore, ILegalHoldQueryStore, IDisposable
{
	private readonly PostgresLegalHoldStoreOptions _options;
	private readonly ILogger<PostgresLegalHoldStore> _logger;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private readonly ITenantContext? _tenantContext;
	private readonly bool _requireTenant;
	private volatile bool _disposed;
	private volatile bool _initialized;

	/// <summary>
	/// Gets the tenant scope bound to every tenant-facing statement, for both the write and the match.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the single place the tenant term is derived. Every tenant-facing statement in this class
	/// reads it; none binds a tenant value by hand. That is what makes the leak inexpressible: the defect
	/// was that each read <em>branched on a caller-supplied nullable</em>, so a caller who passed nothing
	/// got no predicate at all and a caller who passed another tenant's identifier got that tenant's holds.
	/// With the term derived here, there is no per-call-site opportunity to omit it, and a caller-supplied
	/// identifier can only ever be <em>added</em> to it — narrowing the result, never widening it.
	/// </para>
	/// <para>
	/// Deployment mode decides the shape. A deployment that has not opted into multi-tenancy resolves
	/// <see cref="TenantScope.None"/>: no predicate, no bound parameter, and rows keep whatever tenant value
	/// the caller supplied — byte-identical to the single-tenant behaviour, so no stored hold becomes
	/// unreachable. Mode is "did the consumer opt in", read from
	/// <see cref="TenantContextOptions.RequireTenant"/>, and deliberately not "is an
	/// <see cref="ITenantContext"/> present" — the framework always registers a single-tenant default.
	/// </para>
	/// <para>
	/// Multi-tenancy active with no resolved tenant fails closed: it throws rather than reaching a
	/// predicate-less statement. A missing context is the same failure and is stated as such, because
	/// degrading it to <see cref="TenantScope.None"/> would emit no predicate at all.
	/// </para>
	/// </remarks>
	/// <exception cref="TenantRequiredException">
	/// Multi-tenancy is active but no ambient tenant is established.
	/// </exception>
	private TenantScope AmbientScope
	{
		get
		{
			if (!_requireTenant)
			{
				return TenantScope.None;
			}

			return _tenantContext is null
				? throw new TenantRequiredException()
				: TenantScope.FromContext(_tenantContext);
		}
	}

	/// <summary>
	/// Builds the tenant predicate for a legal-hold MUTATION, which is strict equality rather than the
	/// read form's <c>OR tenant is absent</c>.
	/// </summary>
	/// <remarks>
	/// The asymmetry between reading and mutating is the whole point, and getting it wrong fails open on a
	/// blocking control. A tenant must SEE a global hold, because it blocks that tenant's erasures. A tenant
	/// must not MUTATE one: reusing the read predicate here would let any tenant match the global row and
	/// write its own tenant onto it, re-homing an estate-wide preservation order into a single tenant's
	/// partition and silently lifting it for everyone else. Releasing or re-homing a global hold is an
	/// estate-level act, so a tenant-facing mutation matches only rows the tenant actually owns.
	/// </remarks>
	private static string TenantOwnershipPredicate(TenantScope tenant, string column) =>
		tenant.IsScoped ? $" AND {column} = @AmbientTenantId" : string.Empty;

	/// <summary>
	/// Builds the tenant predicate for a legal-hold read.
	/// </summary>
	/// <remarks>
	/// A hold with no tenant is a <em>global</em> hold that blocks erasure for every tenant, so the term is
	/// <c>tenant matches OR tenant is absent</c> rather than a bare equality. A bare equality would drop
	/// global holds from a tenant's view, and a legal hold is a control that <em>blocks</em> erasure —
	/// losing one does not fail safe, it erases data a court order says to keep. It still excludes every
	/// other tenant's holds, which is the isolation this exists to provide.
	/// </remarks>
	private static string TenantPredicate(TenantScope tenant, string column) =>
		tenant.IsScoped ? $" AND ({column} = @AmbientTenantId OR {column} IS NULL)" : string.Empty;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresLegalHoldStore"/> class without an ambient
	/// tenant context — the single-tenant deployment shape.
	/// </summary>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <remarks>
	/// Equivalent to supplying no tenant context and no tenant options: the store resolves
	/// <see cref="TenantScope.None"/> and emits no tenant predicate. A multi-tenant host must use the
	/// tenant-aware overload, which the tenant-scoped registration seam calls on its behalf.
	/// </remarks>
	public PostgresLegalHoldStore(
		IOptions<PostgresLegalHoldStoreOptions> options,
		ILogger<PostgresLegalHoldStore> logger)
		: this(options, logger, tenantContext: null, tenantContextOptions: null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresLegalHoldStore"/> class.
	/// </summary>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// Ambient tenant context. Under multi-tenancy every tenant-facing statement carries the resolved
	/// tenant, and the write path stamps it rather than the value on the incoming hold, so one tenant cannot
	/// place a hold into another tenant's partition. <c>GetExpiredHoldsAsync</c> is deliberately estate-wide
	/// and documented as such at its call site.
	/// </param>
	/// <param name="tenantContextOptions">
	/// The tenant-context options. Its <see cref="TenantContextOptions.RequireTenant"/> (set by
	/// <c>AddMultiTenancy()</c>) selects the deployment mode.
	/// </param>
	public PostgresLegalHoldStore(
		IOptions<PostgresLegalHoldStoreOptions> options,
		ILogger<PostgresLegalHoldStore> logger,
		ITenantContext? tenantContext,
		IOptions<TenantContextOptions>? tenantContextOptions)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_tenantContext = tenantContext;
		_requireTenant = tenantContextOptions?.Value.RequireTenant ?? false;

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

		// The ambient term is authoritative on the write. Stamping the hold's own TenantId would let one
		// tenant place a hold in another tenant's partition — or, by leaving it null, a global hold that
		// blocks every other tenant's erasures. Creating a genuinely global hold is an estate-level
		// operation, not a tenant-facing one, and is not reachable through this path once a tenant is
		// ambient.
		var tenant = AmbientScope;

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			_ = await connection.ExecuteAsync(new CommandDefinition(sql, new
			{
				hold.HoldId,
				hold.DataSubjectIdHash,
				IdType = hold.IdType.HasValue ? (int?)hold.IdType.Value : null,
				TenantId = tenant.IsScoped ? tenant.TenantId : hold.TenantId,
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
		}
		catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
		{
			// Save-not-upsert, same as every other implementation: a re-used HoldId is a caller error and
			// must surface as InvalidOperationException rather than a provider-specific exception. This path
			// had NO conformance coverage — the kit is bound only by the SQL Server and in-memory stores —
			// so the divergence was invisible rather than absent.
			throw new InvalidOperationException($"Legal hold {hold.HoldId} already exists", ex);
		}

		LogSavedHold(hold.HoldId, hold.CaseReference);
	}

	/// <inheritdoc />
	public async Task<LegalHold?> GetHoldAsync(
		Guid holdId,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var tenant = AmbientScope;

		var sql = $@"
			SELECT hold_id, data_subject_id_hash, id_type, tenant_id, basis, case_reference,
				   description, is_active, expires_at, created_by, created_at,
				   released_by, released_at, release_reason
			FROM {_options.FullTableName}
			WHERE hold_id = @HoldId{TenantPredicate(tenant, "tenant_id")}";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var row = await connection.QuerySingleOrDefaultAsync<LegalHoldRow>(
				new CommandDefinition(sql, new { HoldId = holdId, AmbientTenantId = tenant.TenantId }, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
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

		// Scoped on BOTH sides: the row must already belong to this tenant to be matched, and the value
		// written back is the ambient term, so an update can neither reach nor re-home another tenant's hold.
		var tenant = AmbientScope;

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
			WHERE hold_id = @HoldId{TenantOwnershipPredicate(tenant, "tenant_id")}";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var affected = await connection.ExecuteAsync(new CommandDefinition(sql, new
		{
			hold.HoldId,
			hold.DataSubjectIdHash,
			IdType = hold.IdType.HasValue ? (int?)hold.IdType.Value : null,
			TenantId = tenant.IsScoped ? tenant.TenantId : hold.TenantId,
			AmbientTenantId = tenant.TenantId,
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

		// This branch WAS the leak: with no tenantId the query carried no tenant term at all and returned
		// every tenant's holds for that data subject. The ambient term is now unconditional under
		// multi-tenancy and the caller's argument is appended to it, so the argument can only narrow.
		var tenant = AmbientScope;
		var callerPredicate = tenantId is not null ? " AND tenant_id = @TenantId" : string.Empty;

		var sql = $@"
			SELECT hold_id, data_subject_id_hash, id_type, tenant_id, basis, case_reference,
				   description, is_active, expires_at, created_by, created_at,
				   released_by, released_at, release_reason
			FROM {_options.FullTableName}
			WHERE data_subject_id_hash = @DataSubjectIdHash
			  AND is_active = TRUE{TenantPredicate(tenant, "tenant_id")}{callerPredicate}
			ORDER BY created_at DESC";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<LegalHoldRow>(
			new CommandDefinition(sql, new { DataSubjectIdHash = dataSubjectIdHash, TenantId = tenantId, AmbientTenantId = tenant.TenantId },
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

		// The caller names a tenant, but the ambient term is still applied on top: asking for another
		// tenant's holds now intersects to the empty set instead of returning them.
		var tenant = AmbientScope;

		var sql = $@"
			SELECT hold_id, data_subject_id_hash, id_type, tenant_id, basis, case_reference,
				   description, is_active, expires_at, created_by, created_at,
				   released_by, released_at, release_reason
			FROM {_options.FullTableName}
			WHERE tenant_id = @TenantId
			  AND is_active = TRUE{TenantPredicate(tenant, "tenant_id")}
			ORDER BY created_at DESC";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<LegalHoldRow>(
			new CommandDefinition(sql, new { TenantId = tenantId, AmbientTenantId = tenant.TenantId },
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

		// The ambient term is added FIRST and unconditionally under multi-tenancy, so it is a floor rather
		// than an alternative. The caller's own tenantId is then ANDed onto it below: two equality terms can
		// only intersect, so asking for another tenant yields the empty set instead of that tenant's holds,
		// and omitting the argument no longer removes the predicate.
		var tenant = AmbientScope;
		if (tenant.IsScoped)
		{
			whereClauses.Add("(tenant_id = @AmbientTenantId OR tenant_id IS NULL)");
			parameters.Add("AmbientTenantId", tenant.TenantId);
		}

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

		// Ambient first, caller's argument ANDed onto it — the caller can only narrow, never widen.
		var tenant = AmbientScope;
		if (tenant.IsScoped)
		{
			whereClauses.Add("(tenant_id = @AmbientTenantId OR tenant_id IS NULL)");
			parameters.Add("AmbientTenantId", tenant.TenantId);
		}

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
	/// <remarks>
	/// ESTATE-WIDE BY DESIGN. The hold-expiry sweep runs from a background service with no ambient tenant
	/// and must retire every tenant's lapsed holds in one pass; scoping it would leave every tenant but one
	/// under holds that should have expired, blocking their erasures indefinitely. Each row carries its own
	/// tenant, and the surface is reachable only through <see cref="ILegalHoldQueryStore"/>.
	/// </remarks>
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
		_initLock?.Dispose();
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
		const string ExistsSql = "SELECT to_regclass(@TableName) IS NOT NULL";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		foreach (var tableName in new[] { _options.FullTableName })
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
					$"Either create the schema out of band, or set {nameof(PostgresLegalHoldStoreOptions)}."
					+ $"{nameof(PostgresLegalHoldStoreOptions.AutoCreateSchema)} to true to provision it on startup.");
			}
		}
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
