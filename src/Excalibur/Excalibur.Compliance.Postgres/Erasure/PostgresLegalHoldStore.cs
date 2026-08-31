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
	private readonly ITenantContext _tenantContext;
	/// <summary>
	/// Gets the tenant scope this store runs under, resolved in one place so every statement it builds binds
	/// the same term. When the deployment is not multi-tenant the store
	/// deliberately emits no tenant predicate. That decision is stated here and nowhere else: a conversion
	/// cannot make it on the store's behalf without inventing a tenant decision the host never made.
	/// </summary>
	private TenantScope CurrentTenantScope =>
		TenantScope.FromContext(_tenantContext);

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
	/// Deployment mode decides the shape, and it is read from this store's own configuration rather than
	/// inferred from a missing tenant term. A deployment that has not opted into multi-tenancy emits
	/// no predicate, no bound parameter, and rows keep whatever tenant value
	/// the caller supplied — byte-identical to the single-tenant behaviour, so no stored hold becomes
	/// unreachable. Mode is "did the consumer opt in", read from
	/// <see cref="TenantContextOptions.RequireTenant"/>, and deliberately not "is an
	/// <see cref="ITenantContext"/> present" — the framework always registers a single-tenant default.
	/// </para>
	/// <para>
	/// Multi-tenancy active with no resolved tenant fails closed: it throws rather than reaching a
	/// predicate-less statement. A missing context is the same failure and is stated as such, because
	/// degrading it to an unscoped read would emit no predicate at all.
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
				return TenantScope.Untenanted;
			}

			return CurrentTenantScope;
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
	private string TenantOwnershipPredicate(string column) =>
		_requireTenant ? $" AND {column} = @AmbientTenantId" : string.Empty;

	/// <summary>
	/// Builds the tenant predicate for a legal-hold read.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A hold with no tenant is a <em>global</em> hold that blocks erasure for every tenant, so the term is
	/// <c>tenant matches OR the hold is global</c> rather than a bare equality. A bare equality would drop
	/// global holds from a tenant's view, and a legal hold is a control that <em>blocks</em> erasure —
	/// losing one does not fail safe, it erases data a court order says to keep. It still excludes every
	/// other tenant's holds, which is the isolation this exists to provide.
	/// </para>
	/// <para>
	/// "Global" has TWO spellings on the wire, and both are matched on purpose.
	/// </para>
	/// <para>
	/// The reserved sentinel is the current one: the column is total, so a hold with no tenant holds a
	/// value rather than the absence of one. It is bound as a PARAMETER rather than written as a literal,
	/// so the reserved term is stated once in the framework and never re-spelled in SQL where a typo
	/// would silently match nothing.
	/// </para>
	/// <para>
	/// <c>IS NULL</c> is the legacy one, and it is TRANSITION TOLERANCE rather than dead weight. A
	/// consumer who upgrades this package before running the migration that makes the column total still
	/// has NULL in every global row. Without this arm their global holds would go dark the moment they
	/// upgraded — and because a hold blocks erasure, going dark means erasing data a court order says to
	/// keep. The arm costs an unsatisfiable disjunct once the column is <c>NOT NULL</c>.
	/// </para>
	/// <para>
	/// It is removable when a release no longer supports upgrading from a pre-migration database — that
	/// is the condition, and nothing else. Removing it because the column is total in a fresh install
	/// would strand exactly the consumers it exists for.
	/// </para>
	/// </remarks>
	private string TenantPredicate(string column) =>
		_requireTenant ? $" AND {TenantMatchClause(column)}" : string.Empty;

	/// <summary>
	/// The single spelling of "this hold is visible to the scoped tenant", shared by the suffix form above
	/// and by the query builders that assemble a <c>WHERE</c> list instead of appending a suffix.
	/// </summary>
	/// <param name="column">The tenant column to match, as named in the statement being built.</param>
	/// <param name="tenantParameter">
	/// The bound parameter naming the tenant to match — the ambient one by default, or the caller's own
	/// argument on the query paths that accept one.
	/// </param>
	/// <returns>The parenthesised match term, with no leading conjunction.</returns>
	/// <remarks>
	/// <para>
	/// This exists because the same disjunction was previously written out at three separate places in
	/// this class, and the two forms are not interchangeable — one appends <c>" AND ..."</c> to finished
	/// SQL, the other adds a bare term to a list that is joined later. Three copies of a predicate whose
	/// arms encode which holds a tenant can SEE is three chances to update two of them: a copy left on
	/// the old spelling does not fail, it silently stops matching global holds on whichever read path it
	/// governs, and erasure then proceeds against data a court order says to keep. Stating it once makes
	/// that divergence inexpressible rather than merely unlikely.
	/// </para>
	/// <para>
	/// The parameter is named rather than fixed because the caller-supplied tenant term must be the SAME
	/// disjunction as the ambient one. It used to be a bare equality, which made every query that accepted
	/// a tenant argument contradict itself: the ambient term admitted a global hold and the caller's term
	/// discarded it in the same statement. A caller who named their own tenant therefore received a result
	/// with every global preservation order removed, and erasure — which is irreversible — proceeded
	/// against data a court order says to keep. Stating both terms here is what stops them drifting apart
	/// again.
	/// </para>
	/// </remarks>
	private static string TenantMatchClause(string column, string tenantParameter = "@AmbientTenantId") =>
		$"({column} IN ({tenantParameter}, @UntenantedTenantId) OR {column} IS NULL)";

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
	/// <c>AddMultiTenancy()</c>) selects the deployment mode. Required: it used to be nullable and fold a
	/// missing value onto single-tenant, so omitting the registration silently selected the mode that
	/// applies no tenant predicate - a decision nobody made, taken by default, on the path that decides
	/// isolation. The registration extensions call <c>AddDefaultTenantContext()</c> first, so the value
	/// always resolves; a host that reaches this constructor without one is misconfigured and says so.
	/// </param>
	public PostgresLegalHoldStore(
		IOptions<PostgresLegalHoldStoreOptions> options,
		ILogger<PostgresLegalHoldStore> logger,
		ITenantContext tenantContext,
		IOptions<TenantContextOptions> tenantContextOptions)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
		_requireTenant = (tenantContextOptions ?? throw new ArgumentNullException(nameof(tenantContextOptions)))
			.Value.RequireTenant;

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
				TenantId = KeyedTenantPartition.FromStoredValue(
				_requireTenant ? tenant.TenantId : hold.TenantId).TenantId,
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
			throw DuplicateLegalHoldException.ForHoldId(hold.HoldId, ex);
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
			WHERE hold_id = @HoldId{TenantPredicate("tenant_id")}";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var row = await connection.QuerySingleOrDefaultAsync<LegalHoldRow>(
				new CommandDefinition(sql, new { HoldId = holdId, AmbientTenantId = tenant.TenantId, UntenantedTenantId = TenantScope.UntenantedSentinel }, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
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
			WHERE hold_id = @HoldId{TenantOwnershipPredicate("tenant_id")}";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var affected = await connection.ExecuteAsync(new CommandDefinition(sql, new
		{
			hold.HoldId,
			hold.DataSubjectIdHash,
			IdType = hold.IdType.HasValue ? (int?)hold.IdType.Value : null,
			TenantId = KeyedTenantPartition.FromStoredValue(
				_requireTenant ? tenant.TenantId : hold.TenantId).TenantId,
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

		// The caller's argument NARROWS the ambient set to their own tenant plus the holds that belong to
		// no tenant. It is the same disjunction the ambient term uses, and for the same reason: a global
		// hold blocks this tenant's erasures, so dropping it from their view does not fail safe.
		var callerPredicate = tenantId is not null
			? $" AND {TenantMatchClause("tenant_id", "@TenantId")}"
			: string.Empty;

		var sql = $@"
			SELECT hold_id, data_subject_id_hash, id_type, tenant_id, basis, case_reference,
				   description, is_active, expires_at, created_by, created_at,
				   released_by, released_at, release_reason
			FROM {_options.FullTableName}
			WHERE data_subject_id_hash = @DataSubjectIdHash
			  AND is_active = TRUE{TenantPredicate("tenant_id")}{callerPredicate}
			ORDER BY created_at DESC";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<LegalHoldRow>(
			new CommandDefinition(sql, new { DataSubjectIdHash = dataSubjectIdHash, TenantId = tenantId, AmbientTenantId = tenant.TenantId, UntenantedTenantId = TenantScope.UntenantedSentinel },
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
		//
		// The caller's own term admits the holds that belong to NO tenant, for the same reason the
		// ambient one does. This surface answers "which active holds are in force for this tenant",
		// and its caller is the erasure gate: a global preservation order is in force for every
		// tenant, and it carries no data subject, so the subject-scoped query cannot return it either.
		// A bare equality here therefore left a scoped erasure check seeing no global hold at all, and
		// the deletion it should have blocked is irreversible.
		var tenant = AmbientScope;

		var sql = $@"
			SELECT hold_id, data_subject_id_hash, id_type, tenant_id, basis, case_reference,
				   description, is_active, expires_at, created_by, created_at,
				   released_by, released_at, release_reason
			FROM {_options.FullTableName}
			WHERE {TenantMatchClause("tenant_id", "@TenantId")}
			  AND is_active = TRUE{TenantPredicate("tenant_id")}
			ORDER BY created_at DESC";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<LegalHoldRow>(
			new CommandDefinition(sql, new { TenantId = tenantId, AmbientTenantId = tenant.TenantId, UntenantedTenantId = TenantScope.UntenantedSentinel },
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
		if (_requireTenant)
		{
			whereClauses.Add(TenantMatchClause("tenant_id"));
			parameters.Add("AmbientTenantId", tenant.TenantId);
			parameters.Add("UntenantedTenantId", TenantScope.UntenantedSentinel);
		}

		if (!string.IsNullOrEmpty(tenantId))
		{
			// Same disjunction as the ambient term: the caller narrows to their own tenant PLUS the
			// holds that belong to no tenant. A bare equality here dropped every global preservation
			// order from a scoped caller's view, which does not fail safe on a control that blocks
			// an irreversible deletion.
			whereClauses.Add(TenantMatchClause("tenant_id", "@TenantId"));
			parameters.Add("TenantId", tenantId);
			parameters.Add("UntenantedTenantId", TenantScope.UntenantedSentinel);
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
		if (_requireTenant)
		{
			whereClauses.Add(TenantMatchClause("tenant_id"));
			parameters.Add("AmbientTenantId", tenant.TenantId);
			parameters.Add("UntenantedTenantId", TenantScope.UntenantedSentinel);
		}

		if (!string.IsNullOrEmpty(tenantId))
		{
			// Same disjunction as the ambient term: the caller narrows to their own tenant PLUS the
			// holds that belong to no tenant. A bare equality here dropped every global preservation
			// order from a scoped caller's view, which does not fail safe on a control that blocks
			// an irreversible deletion.
			whereClauses.Add(TenantMatchClause("tenant_id", "@TenantId"));
			parameters.Add("TenantId", tenantId);
			parameters.Add("UntenantedTenantId", TenantScope.UntenantedSentinel);
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
	/// <exception cref="InvalidOperationException">A required table is absent, or exists with a stale column set.</exception>
	private async Task VerifySchemaExistsAsync(CancellationToken cancellationToken)
	{
		// Reading the COLUMN catalogue rather than the table catalogue is the whole point of this method.
		// A probe that asks only whether the table exists reports healthy on precisely the database that is
		// broken: one provisioned before a column was added, where the table is present and the wrong shape.
		// The consumer then gets a dead store plus a check that told them it was fine, and the real failure
		// arrives later as a raw undefined_column far from its cause. Automatic schema creation does not
		// repair that database either -- CREATE TABLE IF NOT EXISTS only creates tables that are absent.
		//
		// Resolved through to_regclass rather than by splitting the configured name: the option already
		// carries a qualified identifier and to_regclass parses it the way the statements do. attnum > 0
		// excludes system columns; NOT attisdropped excludes columns dropped but not yet vacuumed, which
		// still occupy a pg_attribute row and would otherwise read as present.
		const string ColumnsSql =
			"SELECT attname FROM pg_attribute " +
			"WHERE attrelid = to_regclass(@TableName) AND attnum > 0 AND NOT attisdropped";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		foreach (var (tableName, requiredColumns) in RequiredSchema)
		{
			var actualColumns = (await connection.QueryAsync<string>(
				new CommandDefinition(
					ColumnsSql,
					new { TableName = tableName },
					cancellationToken: cancellationToken,
					commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false)).ToList();

			// No columns at all means no such table: to_regclass returns NULL for a table that does not
			// exist, so the same query answers both questions and they stay in step.
			if (actualColumns.Count == 0)
			{
				throw new InvalidOperationException(
					$"Required table '{tableName}' does not exist and automatic schema creation is disabled. " +
					$"Either create the schema out of band, or set {nameof(PostgresLegalHoldStoreOptions)}."
					+ $"{nameof(PostgresLegalHoldStoreOptions.AutoCreateSchema)} to true to provision it on startup.");
			}

			// Named, not counted. An operator reading this at startup needs to know WHICH columns are absent
			// to choose the migration; "the schema is stale" sends them to diff it by hand.
			var missing = requiredColumns
				.Where(required => !actualColumns.Contains(required, StringComparer.Ordinal))
				.ToList();

			if (missing.Count > 0)
			{
				throw new InvalidOperationException(
					$"Table '{tableName}' exists but is missing {missing.Count} column(s) that this store's "
					+ $"statements bind: {string.Join(", ", missing)}. This is a schema provisioned before those "
					+ "columns were introduced. Enabling automatic schema creation will NOT repair it, because "
					+ "that path only creates tables that are absent. Run the shipped migration scripts against "
					+ "this database, then restart.");
			}
		}
	}

	/// <summary>
	/// Gets the columns every statement this store issues binds, per table.
	/// </summary>
	/// <remarks>
	/// Kept beside the statements it mirrors: a column added to the INSERT above without a line here is a
	/// column the verification stops covering, which returns this check to the existence-only behaviour it
	/// exists to replace. Compared with <see cref="StringComparer.Ordinal"/> because PostgreSQL folds unquoted
	/// identifiers to lower case and stores them that way, so these are written as the catalogue holds them
	/// rather than relying on a case-insensitive match to paper over a mismatch.
	/// </remarks>
	private IEnumerable<(string TableName, string[] RequiredColumns)> RequiredSchema =>
	[
		(_options.FullTableName,
		[
			"hold_id", "data_subject_id_hash", "id_type", "tenant_id", "basis", "case_reference",
			"description", "is_active", "expires_at", "created_by", "created_at",
			"released_by", "released_at", "release_reason",
		]),
	];

	private async Task CreateSchemaIfNotExistsAsync(CancellationToken cancellationToken)
	{
		var createSchemaSql = $@"CREATE SCHEMA IF NOT EXISTS ""{_options.SchemaName}""";

		var createTableSql = $@"
			CREATE TABLE IF NOT EXISTS {_options.FullTableName} (
				hold_id UUID NOT NULL PRIMARY KEY,
				data_subject_id_hash VARCHAR(128) NULL,
				id_type INT NULL,
				tenant_id VARCHAR(64) NOT NULL DEFAULT '{TenantScope.UntenantedSentinel}',
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
