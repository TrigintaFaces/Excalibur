// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Dapper;

using Excalibur.Compliance.Erasure;
using Excalibur.Dispatch;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.Compliance.Postgres.Erasure;

/// <summary>
/// Postgres implementation of <see cref="IErasureStore"/> using Dapper.
/// </summary>
/// <remarks>
/// This store provides:
/// <list type="bullet">
/// <item>Secure storage of erasure requests with hashed data subject IDs</item>
/// <item>Compliance certificate persistence for audit trails</item>
/// <item>Support for GDPR 30-day deadline tracking</item>
/// <item>7-year certificate retention for regulatory compliance</item>
/// </list>
/// </remarks>
public sealed partial class PostgresErasureStore
	: IErasureStore, IErasureCertificateStore, IErasureQueryStore, IErasureSchemaValidator,
		IKeyDestructionLedger, IDisposable
{
	private readonly PostgresErasureStoreOptions _options;
	private readonly IDataSubjectHasher _dataSubjectHasher;
	private readonly ILogger<PostgresErasureStore> _logger;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private readonly ITenantContext _tenantContext;
	/// <summary>
	/// Gets the tenant scope this store runs under, resolved in one place so every statement it builds binds
	/// the same term. The tenant context is required, and the conversion yields either a scoped term or the
	/// reserved untenanted sentinel &#8212; never an absent one, so there is no state in which the partition is
	/// undecided. A single-tenant host receives the framework default context and operates as the one canonical
	/// tenant; it does not cause the predicate to be omitted.
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
	/// got no predicate at all and a caller who passed another tenant's identifier got that tenant's rows.
	/// With the term derived here instead of from the argument, there is no per-call-site opportunity to
	/// omit it, and a caller-supplied identifier can only ever be <em>added</em> to this one — narrowing
	/// the result, never widening it.
	/// </para>
	/// <para>
	/// Deployment mode decides the shape. A deployment that has not opted into multi-tenancy resolves
	/// no predicate and no bound parameter, and rows keep whatever tenant
	/// value the caller supplied — byte-identical to the single-tenant behaviour, so no stored row becomes
	/// unreachable. A multi-tenant deployment resolves a scoped term that rides every tenant-facing path.
	/// Mode is "did the consumer opt in", read from <see cref="TenantContextOptions.RequireTenant"/>, and
	/// deliberately not "is an <see cref="ITenantContext"/> present" — the framework always registers a
	/// single-tenant default, so presence would make every deployment look multi-tenant.
	/// </para>
	/// <para>
	/// Multi-tenancy active with no resolved tenant fails closed: it throws rather than reaching a
	/// predicate-less statement. A missing context is the same failure and is stated as such, because
	/// degrading it to an unscoped read would emit no predicate at all — the exact
	/// cross-tenant read this property exists to remove.
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
	/// Initializes a new instance of the <see cref="PostgresErasureStore"/> class.
	/// </summary>
	/// <param name="options">The configuration options.</param>
	/// <param name="dataSubjectHasher">The keyed hasher used to pseudonymize data-subject identifiers.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// Ambient tenant context. Under multi-tenancy every tenant-facing statement carries the resolved
	/// tenant, and the write path stamps it rather than the value on the incoming request, so one tenant
	/// cannot file a request into another tenant's partition. The estate-wide background surfaces
	/// (<c>GetScheduledRequestsAsync</c>, <c>CleanupExpiredCertificatesAsync</c>) are deliberately
	/// unscoped and documented as such at their call sites.
	/// </param>
	/// <param name="tenantContextOptions">
	/// The tenant-context options. Its <see cref="TenantContextOptions.RequireTenant"/> (set by
	/// <c>AddMultiTenancy()</c>) selects the deployment mode. Required: it used to be nullable and fold a
	/// missing value onto single-tenant, so omitting the registration silently selected the mode that
	/// applies no tenant predicate - a decision nobody made, taken by default, on the path that decides
	/// isolation. The registration extensions call <c>AddDefaultTenantContext()</c> first, so the value
	/// always resolves; a host that reaches this constructor without one is misconfigured and says so.
	/// </param>
	public PostgresErasureStore(
		IOptions<PostgresErasureStoreOptions> options,
		IDataSubjectHasher dataSubjectHasher,
		ILogger<PostgresErasureStore> logger,
		ITenantContext tenantContext,
		IOptions<TenantContextOptions> tenantContextOptions)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_dataSubjectHasher = dataSubjectHasher ?? throw new ArgumentNullException(nameof(dataSubjectHasher));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
		_requireTenant = (tenantContextOptions ?? throw new ArgumentNullException(nameof(tenantContextOptions)))
			.Value.RequireTenant;

		_options.Validate();
	}

	/// <inheritdoc />
	public async Task SaveRequestAsync(
		ErasureRequest request,
		DateTimeOffset scheduledExecutionTime,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			INSERT INTO {_options.FullRequestsTableName}
				(request_id, data_subject_id_hash, id_type, tenant_id, scope, legal_basis_v2,
				 external_reference, requested_by, requested_at, scheduled_execution_at,
				 status, data_categories, created_at, updated_at)
			VALUES
				(@RequestId, @DataSubjectIdHash, @IdType, @TenantId, @Scope, @LegalBasisV2,
				 @ExternalReference, @RequestedBy, @RequestedAt, @ScheduledExecutionAt,
				 @Status, @DataCategories::jsonb, @CreatedAt, @UpdatedAt)";

		// The ambient term is authoritative on the write. Stamping the request's own TenantId would let a
		// caller file a request into another tenant's partition — and, because every scoped read matches on
		// the ambient term, that row would then be readable only by the tenant it was planted on.
		var tenant = AmbientScope;

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var now = DateTimeOffset.UtcNow;
		try
		{
			_ = await connection.ExecuteAsync(new CommandDefinition(sql, new
		{
			request.RequestId,
			DataSubjectIdHash = HashDataSubjectId(request.DataSubjectId),
			IdType = (int)request.IdType,
			TenantId = KeyedTenantPartition.FromStoredValue(
				_requireTenant ? tenant.TenantId : request.TenantId).TenantId,
			Scope = (int)request.Scope,
			LegalBasisV2 = (int)request.LegalBasis,
			request.ExternalReference,
			request.RequestedBy,
			request.RequestedAt,
			ScheduledExecutionAt = scheduledExecutionTime,
			Status = (int)ErasureRequestStatus.Scheduled,
			DataCategories = request.DataCategories is not null
				? JsonSerializer.Serialize(
					request.DataCategories,
					PostgresComplianceJsonContext.Default.IReadOnlyListString)
				: null,
			CreatedAt = now,
			UpdatedAt = now
		}, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);
		}
		catch (PostgresException ex) when (IsUniqueViolation(ex))
		{
			// This method inserts; it does not upsert. A caller that re-files an existing request id is
			// making a mistake, and the raw provider type is the wrong way to tell them: it forces every
			// consumer to reference Npgsql and to know its error codes just to handle a condition the
			// abstraction already defines. The filter is narrow on purpose - only a unique violation is
			// translated, so a connection failure, a timeout or a constraint we did not anticipate still
			// surfaces unchanged rather than being reported as a duplicate.
			//
			// The type is specific for the same reason the filter is narrow. InvalidOperationException is
			// also what an unprovisioned schema and an unresolved tenant would surface as, so a caller
			// branching on the base type would read those as "already on file" and never re-file a request
			// that was never stored.
			throw DuplicateErasureRequestException.ForRequestId(request.RequestId, ex);
		}

		LogSavedRequest(request.RequestId, scheduledExecutionTime);
	}

	/// <inheritdoc />
	public async Task<ErasureStatus?> GetStatusAsync(
		Guid requestId,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var tenant = AmbientScope;
		var tenantPredicate = _requireTenant ? " AND tenant_id = @AmbientTenantId" : string.Empty;

		var sql = $@"
			SELECT request_id, data_subject_id_hash, id_type, tenant_id, scope, legal_basis_v2,
				   external_reference, requested_by, requested_at, scheduled_execution_at,
				   executed_at, completed_at, cancelled_at, cancellation_reason, cancelled_by,
				   status, keys_deleted, records_affected, certificate_id, error_message, updated_at
			FROM {_options.FullRequestsTableName}
			WHERE request_id = @RequestId{tenantPredicate}";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var row = await connection.QuerySingleOrDefaultAsync<ErasureRequestRow>(
				new CommandDefinition(sql, new { RequestId = requestId, AmbientTenantId = tenant.TenantId }, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);

		if (row is null)
		{
			return null;
		}

		// Read only after the request itself resolved in this tenant, so the handles cannot be returned for a
		// request the caller is not entitled to see. No tenant predicate is needed here for the same reason:
		// the rows are reachable only through a request id that already passed the check above.
		var destroyedHandles = await connection.QueryAsync<string>(new CommandDefinition(
			$"SELECT key_handle FROM {_options.FullDestroyedKeysTableName} WHERE request_id = @RequestId",
			new { RequestId = requestId },
			cancellationToken: cancellationToken,
			commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		return row.ToStatus() with { DestroyedKeyHandles = [.. destroyedHandles] };
	}

	/// <inheritdoc />
	public async Task StageKeyDestructionAsync(
		Guid requestId,
		string keyHandle,
		string keyGeneration,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(keyHandle);
		ArgumentException.ThrowIfNullOrEmpty(keyGeneration);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var tenant = AmbientScope;
		var tenantPredicate = _requireTenant ? " AND r.tenant_id = @AmbientTenantId" : string.Empty;

		// A SEPARATE TABLE from the ledger, and that is the load-bearing part of this design. Between this
		// INSERT and the destruction the row names LIVE material, so if the read predicate could match it a
		// live key would report as destroyed. Holding the two states in one table behind a nullable column
		// would make that a discipline -- one forgotten WHERE clause away. Two tables make it inexpressible:
		// the predicate resolves only against the ledger, and a ledger row exists only after a destruction.
		var sql = $@"
			INSERT INTO {_options.FullDestructionIntentsTableName} (request_id, key_handle, key_generation, staged_at)
			SELECT @RequestId, @KeyHandle, @KeyGeneration, @Now
			FROM {_options.FullRequestsTableName} r
			WHERE r.request_id = @RequestId{tenantPredicate}
			ON CONFLICT (request_id, key_handle, key_generation) DO NOTHING";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var affected = await connection.ExecuteAsync(new CommandDefinition(
			sql,
			new
			{
				RequestId = requestId,
				KeyHandle = keyHandle,
				KeyGeneration = keyGeneration,
				Now = DateTimeOffset.UtcNow,
				AmbientTenantId = tenant.TenantId,
			},
			cancellationToken: cancellationToken,
			commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		// Zero rows has two causes and only one is benign. Already staged is benign; no such request in this
		// tenant is not, and it must not pass as "staged" -- the caller is about to perform an irreversible
		// destruction believing the generation is recoverable from this row.
		if (affected == 0)
		{
			// Same discriminator as the record below: ask whether the REQUEST exists, which is what decides
			// whether zero rows was the benign already-staged case or a caller naming a request that is not here.
			var requestExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
				$"SELECT COUNT(1) FROM {_options.FullRequestsTableName} r "
				+ $"WHERE r.request_id = @RequestId{tenantPredicate}",
				new { RequestId = requestId, AmbientTenantId = tenant.TenantId },
				cancellationToken: cancellationToken,
				commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

			if (requestExists == 0)
			{
				throw new KeyNotFoundException(
					$"No erasure request with id '{requestId}' exists in this tenant, so a destruction cannot "
					+ "be staged against it. This throws rather than returning quietly: the staged intent is "
					+ "the only copy of the generation that survives the destruction, so proceeding without it "
					+ "would leave a crashed pass with nothing to recover from.");
			}
		}
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<string>> GetStagedKeyGenerationsAsync(
		Guid requestId,
		string keyHandle,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(keyHandle);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var tenant = AmbientScope;
		var tenantPredicate = _requireTenant ? " AND r.tenant_id = @AmbientTenantId" : string.Empty;

		// Joined to the request so another tenant's staged intents read as absent: a staged generation is a
		// backend token for live material, and one tenant must not be able to enumerate another's.
		var sql = $@"
			SELECT i.key_generation
			FROM {_options.FullDestructionIntentsTableName} i
			JOIN {_options.FullRequestsTableName} r ON r.request_id = i.request_id
			WHERE i.request_id = @RequestId AND i.key_handle = @KeyHandle{tenantPredicate}";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var generations = await connection.QueryAsync<string>(new CommandDefinition(
			sql,
			new { RequestId = requestId, KeyHandle = keyHandle, AmbientTenantId = tenant.TenantId },
			cancellationToken: cancellationToken,
			commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		return [.. generations];
	}

	/// <inheritdoc />
	public async Task RecordKeyDestroyedAsync(
		Guid requestId,
		string keyHandle,
		string keyGeneration,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(keyHandle);
		ArgumentException.ThrowIfNullOrEmpty(keyGeneration);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var tenant = AmbientScope;
		var tenantPredicate = _requireTenant ? " AND r.tenant_id = @AmbientTenantId" : string.Empty;

		// The INSERT is gated on the request EXISTING IN THIS TENANT, in the same statement, so a record can
		// never be written into another tenant's partition and the check cannot drift from the write.
		//
		// THE CONFLICT TARGET IS THE GENERATION ALONE, and that is a correction rather than a detail. It used
		// to name (request_id, key_handle), whose comment reasoned only about two passes recording the same
		// handle -- so a SECOND destruction at one handle, which is a different generation and a real
		// destruction, was silently absorbed as a duplicate and never recorded. A generation is minted once and
		// never reused, so a conflict on the generation is the only conflict that is genuinely a repeat.
		var sql = $@"
			INSERT INTO {_options.FullDestroyedKeysTableName} (key_generation, request_id, key_handle, destroyed_at, recorded_by)
			SELECT @KeyGeneration, @RequestId, @KeyHandle, @Now, '{RecordedBy.FrameworkErasure}'
			FROM {_options.FullRequestsTableName} r
			WHERE r.request_id = @RequestId{tenantPredicate}
			ON CONFLICT (key_handle, key_generation) DO NOTHING";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var affected = await connection.ExecuteAsync(new CommandDefinition(
			sql,
			new
			{
				KeyGeneration = keyGeneration,
				RequestId = requestId,
				KeyHandle = keyHandle,
				Now = DateTimeOffset.UtcNow,
				AmbientTenantId = tenant.TenantId,
			},
			cancellationToken: cancellationToken,
			commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		// Zero rows has two causes and only one of them is benign. Already recorded is benign; no such request
		// in this tenant is not, and it must not pass as "recorded" -- the caller is about to attest a
		// destruction on the strength of this record existing.
		if (affected == 0)
		{
			// WHICH QUESTION THIS ASKS IS THE WHOLE POINT, so it asks the one that decides. The two causes of
			// zero rows are "the generation was already on file" (benign) and "no such request in this tenant"
			// (not benign, and the caller is about to attest a destruction on a record that does not exist).
			// Asking whether the REQUEST exists separates them exactly. Asking whether the GENERATION exists is a
			// proxy, and the wrong one: a pair on file under any other request would answer yes and let a bad
			// requestId pass as benign.
			var requestExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
				$"SELECT COUNT(1) FROM {_options.FullRequestsTableName} r "
				+ $"WHERE r.request_id = @RequestId{tenantPredicate}",
				new { RequestId = requestId, AmbientTenantId = tenant.TenantId },
				cancellationToken: cancellationToken,
				commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

			if (requestExists == 0)
			{
				throw new KeyNotFoundException(
					$"No erasure request with id '{requestId}' exists in this tenant, so a destroyed key cannot "
					+ "be recorded against it. This throws rather than returning quietly: the record is what "
					+ "lets a retry attest a destruction an earlier pass performed and what lets a read report "
					+ "the subject's erasure, so losing it silently would make that erasure permanently "
					+ "uncertifiable.");
			}
		}

		// ONLY HERE, and the position in this method is the whole safety argument. Control reaches this line
		// only once the ledger row for this generation is known to EXIST -- either this statement inserted it
		// or the conflict check above found it already there. The staged intent then carries nothing the
		// ledger does not, so removing it loses no recovery information.
		//
		// IT NEEDS ORDERING, NOT ATOMICITY, which is why there is no transaction around the pair. A crash
		// between the insert and the delete leaves a stale intent, and a stale intent is harmless by
		// construction: the read predicate cannot name this table. The reverse order is the one that is
		// unsafe -- an intent deleted before its ledger row exists recreates the unrepairable window the
		// staging table was introduced to close -- so the delete is in a method of its own, called from this
		// one place, after the success determination.
		await DeleteStagedIntentAsync(connection, requestId, keyHandle, keyGeneration, cancellationToken)
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Removes a staged intent whose destruction is now recorded in the ledger.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Housekeeping, and it FAILS OPEN on purpose. By the time this runs the destruction is irreversible and
	/// its ledger row is committed, so the request must be able to reach completion; letting a cleanup failure
	/// propagate would turn a successful, recorded erasure into a failed one and leave the subject
	/// uncertifiable over a row nobody reads. The cost of the failure is one orphaned row in a table the read
	/// predicate cannot see.
	/// </para>
	/// <para>
	/// Without this the intents table grows by one row per key per request, forever. It is bounded here rather
	/// than by a sweep because the exact moment the row becomes redundant is known precisely at this call
	/// site and nowhere else.
	/// </para>
	/// </remarks>
	private async Task DeleteStagedIntentAsync(
		NpgsqlConnection connection,
		Guid requestId,
		string keyHandle,
		string keyGeneration,
		CancellationToken cancellationToken)
	{
		try
		{
			_ = await connection.ExecuteAsync(new CommandDefinition(
				$"DELETE FROM {_options.FullDestructionIntentsTableName} "
				+ "WHERE request_id = @RequestId AND key_handle = @KeyHandle "
				+ "AND key_generation = @KeyGeneration",
				new { RequestId = requestId, KeyHandle = keyHandle, KeyGeneration = keyGeneration },
				cancellationToken: cancellationToken,
				commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);
		}
		catch (NpgsqlException ex)
		{
			LogStagedIntentNotCleanedUp(keyHandle, ex);
		}
	}

	/// <inheritdoc />
	public async Task RecordDestroyedGenerationAsync(
		string keyHandle,
		string keyGeneration,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(keyHandle);
		ArgumentException.ThrowIfNullOrEmpty(keyGeneration);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// NO request, and it is NULL rather than a sentinel because the caller genuinely has none -- a consumer
		// who destroyed a key through their own process has no erasure request to name. The HANDLE is supplied,
		// because it is part of the ledger's key and a caller asserting a destruction knows what it destroyed.
		// The provenance column is what records that this row rests on the caller's assertion; it is not
		// inferred from the null request.
		//
		// NOT gated on a request row existing, which is the one structural difference from the framework's own
		// write, and it is the whole point of this member: there is no request. The evidence is the caller's
		// assertion, which the contract states they own.
		//
		// ON CONFLICT DO NOTHING, so re-asserting a pair already on file is a no-op and the FIRST instant
		// stands. A later assertion must not move a recorded destruction's timestamp.
		var sql = $@"
			INSERT INTO {_options.FullDestroyedKeysTableName} (key_generation, request_id, key_handle, destroyed_at, recorded_by)
			VALUES (@KeyGeneration, NULL, @KeyHandle, @Now, '{RecordedBy.CallerAssertion}')
			ON CONFLICT (key_handle, key_generation) DO NOTHING";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// Not caught, by contract: a caller that believed it recorded a destruction it did not would see the
		// subject's reads fail with no indication why.
		_ = await connection.ExecuteAsync(new CommandDefinition(
			sql,
			new { KeyHandle = keyHandle, KeyGeneration = keyGeneration, Now = DateTimeOffset.UtcNow },
			cancellationToken: cancellationToken,
			commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async ValueTask<bool> IsGenerationDestroyedAsync(
		string keyHandle,
		string keyGeneration,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(keyHandle);
		ArgumentException.ThrowIfNullOrEmpty(keyGeneration);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// NO tenant predicate, deliberately, and NOT an oversight. The statement is addressed by the ledger's
		// whole PRIMARY KEY, so it already selects at most one row; a tenant term on such a statement cannot
		// admit a foreign row and its only reachable effect is turning the correct row into none. Here that
		// effect is the catastrophic one in reverse: a read of a genuinely erased subject would stop reporting
		// their erasure and start failing, permanently. The HANDLE is a KEY COMPONENT and not a tenant term:
		// it is what makes "one tenant cannot name another's row" true by construction. This comment used to
		// rest that claim on a generation being minted by the backend and not derivable from a subject, which
		// is true of every shipped provider and is NOT something a consumer-supplied one is obliged to satisfy.
		//
		// The intents table is NOT named here, and must never be: a staged generation describes material that
		// may still be live.
		var sql = $"SELECT COUNT(1) FROM {_options.FullDestroyedKeysTableName} "
			+ "WHERE key_handle = @KeyHandle AND key_generation = @KeyGeneration";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var found = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
			sql,
			new { KeyHandle = keyHandle, KeyGeneration = keyGeneration },
			cancellationToken: cancellationToken,
			commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		return found > 0;
	}

	/// <inheritdoc />
	public async Task<bool> UpdateStatusAsync(
		Guid requestId,
		ErasureRequestStatus status,
		string? errorMessage,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var tenant = AmbientScope;
		var tenantPredicate = _requireTenant ? " AND tenant_id = @AmbientTenantId" : string.Empty;

		var sql = $@"
			UPDATE {_options.FullRequestsTableName}
			SET status = @Status,
				error_message = @ErrorMessage,
				executed_at = CASE WHEN @Status = {(int)ErasureRequestStatus.InProgress} THEN @Now ELSE executed_at END,
				updated_at = @Now
			WHERE request_id = @RequestId{tenantPredicate}
			  AND (@Status <> {(int)ErasureRequestStatus.InProgress}
			       OR status = {(int)ErasureRequestStatus.Scheduled})";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var affected = await connection.ExecuteAsync(new CommandDefinition(sql,
			new { RequestId = requestId, Status = (int)status, ErrorMessage = errorMessage, Now = DateTimeOffset.UtcNow, AmbientTenantId = tenant.TenantId },
			cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		return affected > 0;
	}

	/// <inheritdoc />
	public async Task RecordCompletionAsync(
		Guid requestId,
		int keysDeleted,
		int recordsAffected,
		Guid certificateId,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var tenant = AmbientScope;
		var tenantPredicate = _requireTenant ? " AND tenant_id = @AmbientTenantId" : string.Empty;

		var sql = $@"
			UPDATE {_options.FullRequestsTableName}
			SET status = @Status,
				keys_deleted = @KeysDeleted,
				records_affected = @RecordsAffected,
				certificate_id = @CertificateId,
				completed_at = @Now,
				updated_at = @Now
			WHERE request_id = @RequestId{tenantPredicate}
			  AND status IN ({(int)ErasureRequestStatus.Scheduled}, {(int)ErasureRequestStatus.InProgress}, {(int)ErasureRequestStatus.AwaitingKeyDestruction})";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// The affected count is the whole check, which is why it is no longer discarded. An UPDATE that
		// matches nothing is a perfectly successful statement, so throwing the result away turned
		// "there is no such request" into "recorded". This surface produces the evidence a consumer hands
		// to an auditor: a completion recorded against a request that does not exist attests to erasing
		// something nobody asked to erase, and nothing anywhere reports it.
		var affected = await connection.ExecuteAsync(new CommandDefinition(sql,
			new
			{
				RequestId = requestId,
				Status = (int)ErasureRequestStatus.Completed,
				KeysDeleted = keysDeleted,
				RecordsAffected = recordsAffected,
				CertificateId = certificateId,
				Now = DateTimeOffset.UtcNow,
				AmbientTenantId = tenant.TenantId
			}, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		if (affected == 0)
		{
			throw new KeyNotFoundException(
				$"No erasure request with id '{requestId}' is in a state from which a completion can be "
				+ "recorded: it does not exist, it belongs to another tenant, or its status changed while "
				+ "the run was executing. A legal hold or a cancellation recorded mid-run takes the request "
				+ "out of the run deliberately, and a completion must not overwrite it.");
		}
	}

	/// <inheritdoc />
	public async Task<bool> RecordCancellationAsync(
		Guid requestId,
		string reason,
		string cancelledBy,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var tenant = AmbientScope;
		var tenantPredicate = _requireTenant ? " AND tenant_id = @AmbientTenantId" : string.Empty;

		var sql = $@"
			UPDATE {_options.FullRequestsTableName}
			SET status = @Status,
				cancellation_reason = @Reason,
				cancelled_by = @CancelledBy,
				cancelled_at = @Now,
				updated_at = @Now
			WHERE request_id = @RequestId
			  AND status IN ({(int)ErasureRequestStatus.Pending}, {(int)ErasureRequestStatus.Scheduled}){tenantPredicate}";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var affected = await connection.ExecuteAsync(new CommandDefinition(sql,
			new
			{
				RequestId = requestId,
				Status = (int)ErasureRequestStatus.Cancelled,
				Reason = reason,
				CancelledBy = cancelledBy,
				Now = DateTimeOffset.UtcNow,
				AmbientTenantId = tenant.TenantId
			}, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		return affected > 0;
	}

	/// <inheritdoc />
	/// <remarks>
	/// ESTATE-WIDE BY DESIGN — deliberately not tenant-scoped, and the asymmetry is load-bearing. The
	/// erasure scheduler drains every tenant's due requests in one background pass with no ambient tenant
	/// established, exactly as the outbox drain does; scoping it would resolve the tenant as absent, return
	/// the empty set, and stall erasure permanently while still satisfying a safety-only test. Each row
	/// carries its own tenant, so the scheduler establishes a per-request scope as it drains. This surface
	/// is reachable only through <see cref="IErasureQueryStore"/>, which a per-tenant caller does not take
	/// a dependency on.
	/// </remarks>
	public async Task<IReadOnlyList<ErasureStatus>> GetScheduledRequestsAsync(
		int maxResults,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			SELECT request_id, data_subject_id_hash, id_type, tenant_id, scope, legal_basis_v2,
				   external_reference, requested_by, requested_at, scheduled_execution_at,
				   executed_at, completed_at, cancelled_at, cancellation_reason, cancelled_by,
				   status, keys_deleted, records_affected, certificate_id, error_message, updated_at
			FROM {_options.FullRequestsTableName}
			WHERE status = {(int)ErasureRequestStatus.Scheduled}
			  AND scheduled_execution_at <= @Now
			ORDER BY scheduled_execution_at
			LIMIT @MaxResults";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<ErasureRequestRow>(
			new CommandDefinition(sql, new { MaxResults = maxResults, Now = DateTimeOffset.UtcNow },
				cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		return rows.Select(r => r.ToStatus()).ToList();
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<ErasureStatus>> ListRequestsAsync(
		ErasureRequestStatus? status,
		string? tenantId,
		DateTimeOffset? fromDate,
		DateTimeOffset? toDate,
		int pageNumber,
		int pageSize,
		CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
		ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 1000);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var whereClauses = new List<string>();
		var parameters = new DynamicParameters();

		// The ambient term is added FIRST and unconditionally under multi-tenancy, so it is a floor rather
		// than an alternative. The caller's own tenantId is then ANDed onto it below: two equality terms can
		// only intersect, so asking for another tenant yields the empty set instead of that tenant's rows,
		// and omitting the argument no longer removes the predicate. Widening is not expressible here — it
		// would take changing this AND to an OR.
		var tenant = AmbientScope;
		if (_requireTenant)
		{
			whereClauses.Add("tenant_id = @AmbientTenantId");
			parameters.Add("AmbientTenantId", tenant.TenantId);
		}

		if (status.HasValue)
		{
			whereClauses.Add("status = @Status");
			parameters.Add("Status", (int)status.Value);
		}

		if (!string.IsNullOrEmpty(tenantId))
		{
			whereClauses.Add("tenant_id = @TenantId");
			parameters.Add("TenantId", tenantId);
		}

		if (fromDate.HasValue)
		{
			whereClauses.Add("requested_at >= @FromDate");
			parameters.Add("FromDate", fromDate.Value);
		}

		if (toDate.HasValue)
		{
			whereClauses.Add("requested_at <= @ToDate");
			parameters.Add("ToDate", toDate.Value);
		}

		var whereClause = whereClauses.Count > 0
			? "WHERE " + string.Join(" AND ", whereClauses)
			: string.Empty;

		var offset = (pageNumber - 1) * pageSize;
		parameters.Add("Offset", offset);
		parameters.Add("PageSize", pageSize);

		var sql = $@"
			SELECT request_id, data_subject_id_hash, id_type, tenant_id, scope, legal_basis_v2,
				   external_reference, requested_by, requested_at, scheduled_execution_at,
				   executed_at, completed_at, cancelled_at, cancellation_reason, cancelled_by,
				   status, keys_deleted, records_affected, certificate_id, error_message, updated_at
			FROM {_options.FullRequestsTableName}
			{whereClause}
			ORDER BY requested_at DESC
			LIMIT @PageSize OFFSET @Offset";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<ErasureRequestRow>(
			new CommandDefinition(sql, parameters, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		return rows.Select(r => r.ToStatus()).ToList();
	}

	/// <inheritdoc />
	public async Task SaveCertificateAsync(
		ErasureCertificate certificate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(certificate);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			INSERT INTO {_options.FullCertificatesTableName}
				(certificate_id, request_id, data_subject_reference, request_received_at, completed_at,
				 method, summary, verification, legal_basis_v2, signature, retain_until,
				 exceptions, generated_at, version, created_at, payload)
			VALUES
				(@CertificateId, @RequestId, @DataSubjectReference, @RequestReceivedAt, @CompletedAt,
				 @Method, @Summary::jsonb, @Verification::jsonb, @LegalBasisV2, @Signature, @RetainUntil,
				 @Exceptions::jsonb, @GeneratedAt, @Version, @CreatedAt, @Payload)";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			_ = await connection.ExecuteAsync(new CommandDefinition(sql, new
		{
			certificate.Payload.CertificateId,
			certificate.Payload.RequestId,
			certificate.Payload.DataSubjectReference,
			certificate.Payload.RequestReceivedAt,
			certificate.Payload.CompletedAt,
			Method = (int)certificate.Payload.Method,
			Summary = JsonSerializer.Serialize(
				certificate.Payload.Summary,
				PostgresComplianceJsonContext.Default.ErasureSummary),
			Verification = JsonSerializer.Serialize(
				certificate.Payload.Verification,
				PostgresComplianceJsonContext.Default.VerificationSummary),
			LegalBasisV2 = (int)certificate.Payload.LegalBasis,
			certificate.Signature,
			certificate.Payload.RetainUntil,
			Exceptions = JsonSerializer.Serialize(
				certificate.Payload.Exceptions,
				PostgresComplianceJsonContext.Default.IReadOnlyListErasureException),
			certificate.Payload.GeneratedAt,
			certificate.Payload.Version,
			CreatedAt = DateTimeOffset.UtcNow,

			// The document itself. Every column above is an INDEX -- something a query filters or joins on --
			// and nothing above is read back into the payload. A column has a type, and TIMESTAMPTZ cannot
			// hold a .NET DateTimeOffset: it keeps microseconds where the value carries hundreds of
			// nanoseconds, so a certificate reassembled from these columns came back with its instants
			// quietly rounded and no longer matched its own signature.
			Payload = ErasureCertificateCanonicalizer.ToCanonicalJson(certificate.Payload)
		}, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);
		}
		catch (PostgresException ex) when (IsUniqueViolation(ex))
		{
			// A certificate is the attestation itself, so silently replacing one would rewrite evidence
			// that has already been issued. Same narrow filter, and same specific type, as the request
			// insert.
			throw DuplicateErasureCertificateException.ForCertificateId(certificate.Payload.CertificateId, ex);
		}

		LogSavedCertificate(certificate.Payload.CertificateId, certificate.Payload.RequestId);
	}

	/// <inheritdoc />
	public async Task<ErasureCertificate?> GetCertificateAsync(
		Guid requestId,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// The certificate table carries no tenant column of its own: a certificate belongs to the request it
		// certifies, so its tenant is that request's. Scoping through the request is what keeps the join and
		// the row in agreement — adding a second tenant column would let the two disagree.
		var tenant = AmbientScope;
		var tenantPredicate = _requireTenant
			? $@" AND EXISTS (SELECT 1 FROM {_options.FullRequestsTableName} r
				  WHERE r.request_id = {_options.FullCertificatesTableName}.request_id AND r.tenant_id = @AmbientTenantId)"
			: string.Empty;

		// ORDER BY plus QueryFirstOrDefault, and the pairing IS the fix rather than a tidy-up. This read was
		// QuerySingleOrDefaultAsync against a table whose request key is a NON-UNIQUE index, so the moment a
		// request held two certificates Dapper threw InvalidOperationException here on EVERY subsequent call,
		// permanently: two signed documents stored and NEITHER retrievable by the lookup a consumer uses.
		//
		// Two certificates for one request is REACHABLE and not pathological. The partial branch issues one
		// and the completion branch issues another, each with a freshly minted id, so a request that is
		// partially completed and later completes legitimately holds two. A UNIQUE constraint would have been
		// the wrong fix: it forbids the second DOCUMENT rather than the ambiguity, and both documents are real
		// evidence an auditor may be entitled to.
		//
		// So the lookup answers with the CURRENT certificate and states that in the ordering. Newest first,
		// because a completion certificate is generated after the partial one it supersedes. The id tiebreak
		// makes the order TOTAL: without it, two certificates generated within the same tick leave the choice
		// to the engine, which is a different wrong answer per provider and per query plan.
		var sql = $@"
			SELECT certificate_id, request_id, data_subject_reference, request_received_at, completed_at,
				   method, summary, verification, legal_basis_v2, signature, retain_until,
				   exceptions, generated_at, version, payload
			FROM {_options.FullCertificatesTableName}
			WHERE request_id = @RequestId{tenantPredicate}
			ORDER BY generated_at DESC, certificate_id DESC";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var row = await connection.QueryFirstOrDefaultAsync<CertificateRow>(
				new CommandDefinition(sql, new { RequestId = requestId, AmbientTenantId = tenant.TenantId }, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);

		return row?.ToCertificate();
	}

	/// <inheritdoc />
	public async Task<ErasureCertificate?> GetCertificateByIdAsync(
		Guid certificateId,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Scoped through the certified request, for the same reason as the by-request lookup above.
		var tenant = AmbientScope;
		var tenantPredicate = _requireTenant
			? $@" AND EXISTS (SELECT 1 FROM {_options.FullRequestsTableName} r
				  WHERE r.request_id = {_options.FullCertificatesTableName}.request_id AND r.tenant_id = @AmbientTenantId)"
			: string.Empty;

		var sql = $@"
			SELECT certificate_id, request_id, data_subject_reference, request_received_at, completed_at,
				   method, summary, verification, legal_basis_v2, signature, retain_until,
				   exceptions, generated_at, version, payload
			FROM {_options.FullCertificatesTableName}
			WHERE certificate_id = @CertificateId{tenantPredicate}";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var row = await connection.QuerySingleOrDefaultAsync<CertificateRow>(
				new CommandDefinition(sql, new { CertificateId = certificateId, AmbientTenantId = tenant.TenantId }, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);

		return row?.ToCertificate();
	}

	/// <inheritdoc />
	/// <remarks>
	/// ESTATE-WIDE BY DESIGN, like <c>GetScheduledRequestsAsync</c>: a retention sweep that runs from a
	/// background service with no ambient tenant and must delete every tenant's expired certificates in one
	/// pass. Scoping it would silently stop honouring the retention limit for every tenant but one. It is
	/// reachable only through <see cref="IErasureCertificateStore"/>, not the per-tenant request path.
	/// </remarks>
	public async Task<int> CleanupExpiredCertificatesAsync(
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			DELETE FROM {_options.FullCertificatesTableName}
			WHERE retain_until < @Now";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var deleted = await connection.ExecuteAsync(
				new CommandDefinition(sql, new { Now = DateTimeOffset.UtcNow }, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);

		if (deleted > 0)
		{
			LogCleanedUpCertificates(deleted);
		}

		return deleted;
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType == typeof(IErasureCertificateStore))
		{
			return this;
		}

		if (serviceType == typeof(IErasureQueryStore))
		{
			return this;
		}

		return null;
	}

	private string HashDataSubjectId(string dataSubjectId) =>
		_dataSubjectHasher.HashDataSubjectId(dataSubjectId);

	// CreateDefaultVerificationSummary lived here: the read path called it when the verification column
	// could not be deserialized, fabricating "Verified = false, VerifiedAt = MinValue" for a certificate
	// whose verification record was in fact unreadable. Reading the payload whole removes both the caller
	// and the need: an unreadable document is now refused rather than replaced with a plausible one.

	[LoggerMessage(LogLevel.Debug, "Saved erasure request {RequestId} scheduled for {ScheduledTime}")]
	private partial void LogSavedRequest(Guid requestId, DateTimeOffset scheduledTime);

	[LoggerMessage(LogLevel.Debug, "Saved erasure certificate {CertificateId} for request {RequestId}")]
	private partial void LogSavedCertificate(Guid certificateId, Guid requestId);

	[LoggerMessage(LogLevel.Information, "Cleaned up {Count} expired erasure certificates")]
	private partial void LogCleanedUpCertificates(int count);

	// Warning, not error: the destruction is recorded and the erasure can complete. What is left behind is
	// one row in a table the read predicate cannot name. Logged so unbounded growth has a visible cause
	// rather than being discovered as a disk-space incident nobody can attribute.
	[LoggerMessage(LogLevel.Warning,
		"A destruction for key handle {KeyHandle} was recorded in the ledger, but its staged intent could not "
		+ "be removed. The erasure is unaffected; the intent row remains and is invisible to the read path")]
	private partial void LogStagedIntentNotCleanedUp(string keyHandle, Exception exception);

	[LoggerMessage(LogLevel.Debug, "Ensured Postgres erasure schema and tables exist")]
	private partial void LogSchemaEnsured();

	/// <inheritdoc />
	/// <remarks>
	/// Provisioning is settled here, once, at host startup — not on the path of every write. A store that
	/// verified its schema inside <c>SaveRequestAsync</c> reports a deployment fault as a failure of that
	/// one erasure request, at the moment a data subject's request is being filed. Running it here means a
	/// mis-provisioned deployment fails to start instead, and by the time any write executes the check is
	/// already satisfied. The first-use call that remains on each operation is the fail-closed floor for
	/// consumers that never run the hosted service.
	/// </remarks>
	public async ValueTask ValidateSchemaAsync(CancellationToken cancellationToken)
		=> await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

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

			// CREATE TABLE IF NOT EXISTS guards on table EXISTENCE, so a database provisioned before a
			// column was added creates nothing and reports success -- the table is there, it is simply
			// the wrong shape. Without this, that database initializes cleanly on the auto-create path and
			// fails later on first use with a raw undefined_column far from its cause. The verify-disabled
			// path below already runs this same check; the auto-create path needs it just as much.
			await VerifySchemaExistsAsync(cancellationToken).ConfigureAwait(false);
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
	/// <exception cref="ErasureStoreNotProvisionedException">
	/// A required table is absent, or is present but missing columns this store's statements bind.
	/// Deliberately outside the <see cref="InvalidOperationException"/> hierarchy: that is the hierarchy a
	/// duplicate request identifier uses, and a caller cannot be left unable to tell "this request is
	/// already on file" from "this database was never provisioned".
	/// </exception>
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
				throw new ErasureStoreNotProvisionedException(
					$"Required table '{tableName}' does not exist and automatic schema creation is disabled. " +
					$"Either create the schema out of band, or set {nameof(PostgresErasureStoreOptions)}."
					+ $"{nameof(PostgresErasureStoreOptions.AutoCreateSchema)} to true to provision it on startup.")
				{
					TableName = tableName,
				};
			}

			// Named, not counted. An operator reading this at startup needs to know WHICH columns are absent
			// to choose the migration; "the schema is stale" sends them to diff it by hand.
			var missing = requiredColumns
				.Where(required => !actualColumns.Contains(required, StringComparer.Ordinal))
				.ToList();

			if (missing.Count > 0)
			{
				throw new ErasureStoreNotProvisionedException(
					$"Table '{tableName}' exists but is missing {missing.Count} column(s) that this store's "
					+ $"statements bind: {string.Join(", ", missing)}. This is a schema provisioned before those "
					+ "columns were introduced. Enabling automatic schema creation will NOT repair it, because "
					+ "that path only creates tables that are absent. WE SHIP NO ALTER SCRIPT for this: the "
					+ "create scripts already declare these columns, so a freshly provisioned database is "
					+ "correct and only a pre-existing one reaches this. Either re-provision from the shipped "
					+ "create script, or add the listed column(s) by hand with a NOT NULL default matching a "
					+ "never-updated row. Then restart.")
				{
					TableName = tableName,
				};
			}
		}
	}

	/// <summary>
	/// Gets the columns every statement this store issues binds, per table.
	/// </summary>
	/// <remarks>
	/// The request columns are the union of the INSERT and the several UPDATE paths, not the INSERT alone.
	/// A request is created with a subset and then mutated through execution, completion and cancellation,
	/// so the columns only an UPDATE names -- the outcome and cancellation fields -- are exactly the ones a
	/// schema predating those features would lack, and the ones whose absence would otherwise surface at the
	/// end of an erasure rather than at startup. Compared with <see cref="StringComparer.Ordinal"/> because
	/// PostgreSQL folds unquoted identifiers to lower case and stores them that way, so these are written as
	/// the catalogue holds them rather than relying on a case-insensitive match to paper over a mismatch.
	/// </remarks>
	private IEnumerable<(string TableName, string[] RequiredColumns)> RequiredSchema =>
	[
		(_options.FullRequestsTableName,
		[
			"request_id", "data_subject_id_hash", "id_type", "tenant_id", "scope", "legal_basis_v2",
			"external_reference", "requested_by", "requested_at", "scheduled_execution_at",
			"executed_at", "completed_at", "cancelled_at", "cancellation_reason", "cancelled_by",
			"status", "keys_deleted", "records_affected", "certificate_id", "error_message",
			"data_categories", "created_at", "updated_at",
		]),
		(_options.FullCertificatesTableName,
		[
			"certificate_id", "request_id", "data_subject_reference", "request_received_at", "completed_at",
			"method", "summary", "verification", "legal_basis_v2", "signature", "retain_until",
			"exceptions", "generated_at", "version", "created_at", "payload",
		]),

		// Verified like the others, so a database provisioned before this table existed fails at STARTUP with
		// the table named, rather than mid-erasure. The failure is loud on purpose: without this table a
		// retried erasure silently attests less coverage than the request achieved, and the subject's
		// completion becomes unreachable with nothing reporting why.
		(_options.FullDestroyedKeysTableName,
		[
			"key_generation", "request_id", "key_handle", "destroyed_at", "recorded_by",
		]),

		// Verified for the same reason, and the failure it prevents is worse. Without this table a destruction
		// proceeds with nowhere to stage the generation it is about to annihilate, so a crash between the
		// destroy and the record leaves the subject's ciphertext permanently undecryptable AND their erasure
		// permanently unreportable, with no repair available at all. Naming the table at STARTUP is the only
		// place that is still cheap.
		(_options.FullDestructionIntentsTableName,
		[
			"request_id", "key_handle", "key_generation", "staged_at",
		]),
	];

	/// <summary>
	/// The ledger's provenance values. Stored as text because the audience for this table is a compliance
	/// auditor reading rows directly, and a self-describing value needs no lookup to interpret.
	/// </summary>
	/// <remarks>
	/// Provenance is an EXPLICIT column rather than something inferred from a null request id. Deriving a fact
	/// from the absence of a value is the exact reasoning this ledger exists to remove, and it would be no more
	/// sound here than at a key backend. It is an audit attribute only: the read predicate never names it, so a
	/// row is a row whichever writer produced it.
	/// </remarks>
	private static class RecordedBy
	{
		/// <summary>An erasure this framework performed: staged before the destroy, recorded after it.</summary>
		public const string FrameworkErasure = "framework-erasure";

		/// <summary>A destruction the CALLER performed and asserted. Nothing here re-verified it.</summary>
		public const string CallerAssertion = "caller-assertion";
	}

	/// <summary>
	/// Indicates whether a Postgres error is a unique-constraint violation (SQLSTATE 23505).
	/// </summary>
	/// <remarks>
	/// Used as an exception filter so only this one condition is translated. A broad catch would report
	/// an unrelated failure - a dropped connection, a timeout, a check constraint - as a duplicate, which
	/// is worse than not translating at all: the caller would be told the row exists when it does not.
	/// </remarks>
	private static bool IsUniqueViolation(PostgresException ex)
		=> string.Equals(ex.SqlState, PostgresErrorCodes.UniqueViolation, StringComparison.Ordinal);

	private async Task CreateSchemaIfNotExistsAsync(CancellationToken cancellationToken)
	{
		var createSchemaSql = $@"CREATE SCHEMA IF NOT EXISTS ""{_options.SchemaName}""";

		var createRequestsTableSql = $@"
			CREATE TABLE IF NOT EXISTS {_options.FullRequestsTableName} (
				request_id UUID NOT NULL PRIMARY KEY,
				data_subject_id_hash VARCHAR(128) NOT NULL,
				id_type INT NOT NULL,
				tenant_id VARCHAR(64) NOT NULL DEFAULT '{TenantScope.UntenantedSentinel}',
				scope INT NOT NULL,
				legal_basis_v2 INT NOT NULL,
				external_reference VARCHAR(256) NULL,
				requested_by VARCHAR(256) NOT NULL,
				requested_at TIMESTAMPTZ NOT NULL,
				scheduled_execution_at TIMESTAMPTZ NULL,
				executed_at TIMESTAMPTZ NULL,
				completed_at TIMESTAMPTZ NULL,
				cancelled_at TIMESTAMPTZ NULL,
				cancellation_reason VARCHAR(1000) NULL,
				cancelled_by VARCHAR(256) NULL,
				status INT NOT NULL,
				keys_deleted INT NULL,
				records_affected INT NULL,
				certificate_id UUID NULL,
				error_message VARCHAR(2000) NULL,
				data_categories JSONB NULL,
				created_at TIMESTAMPTZ NOT NULL,
				updated_at TIMESTAMPTZ NOT NULL
			)";

		var createRequestsIndexesSql = $@"
			CREATE INDEX IF NOT EXISTS ix_{_options.RequestsTableName}_status
				ON {_options.FullRequestsTableName} (status, scheduled_execution_at);
			CREATE INDEX IF NOT EXISTS ix_{_options.RequestsTableName}_tenant
				ON {_options.FullRequestsTableName} (tenant_id, requested_at);
			CREATE INDEX IF NOT EXISTS ix_{_options.RequestsTableName}_subject
				ON {_options.FullRequestsTableName} (data_subject_id_hash)";

		var createCertificatesTableSql = $@"
			CREATE TABLE IF NOT EXISTS {_options.FullCertificatesTableName} (
				certificate_id UUID NOT NULL PRIMARY KEY,
				request_id UUID NOT NULL,
				data_subject_reference VARCHAR(256) NOT NULL,
				request_received_at TIMESTAMPTZ NOT NULL,
				completed_at TIMESTAMPTZ NOT NULL,
				method INT NOT NULL,
				summary JSONB NOT NULL,
				verification JSONB NOT NULL,
				legal_basis_v2 INT NOT NULL,
				signature VARCHAR(512) NOT NULL,
				retain_until TIMESTAMPTZ NOT NULL,
				-- Every remaining payload claim gets a column. The signature covers the payload WHOLE, so a
				-- field with nowhere to live here does not merely go missing on read -- the reassembled
				-- payload no longer matches what was signed, and the certificate reports as TAMPERED.
				-- exceptions is the Article 17(3) record of data lawfully RETAINED; losing it makes the
				-- certificate attest a more complete erasure than occurred.
				exceptions JSONB NOT NULL,
				generated_at TIMESTAMPTZ NOT NULL,
				version VARCHAR(16) NOT NULL,
				created_at TIMESTAMPTZ NOT NULL,
				-- The signed document, verbatim. The columns above are indexes for querying; this is what a
				-- read reconstructs the certificate from, so what comes back is what was signed whatever the
				-- engine's column types can express.
				-- TEXT, not JSONB: jsonb normalizes what it stores -- it reorders keys and rewrites numbers --
				-- so the bytes read back would not be the bytes written. These bytes are the ones the
				-- signature covers and nothing queries inside them, so they are stored exactly as produced.
				payload TEXT NOT NULL
			)";

		var createCertificatesIndexesSql = $@"
			CREATE INDEX IF NOT EXISTS ix_{_options.CertificatesTableName}_request
				ON {_options.FullCertificatesTableName} (request_id);
			CREATE INDEX IF NOT EXISTS ix_{_options.CertificatesTableName}_retain
				ON {_options.FullCertificatesTableName} (retain_until)";

		// THE LEDGER. A row here IS the statement that this generation's material was irreversibly destroyed by
		// an erasure this framework performed, and it is the sole basis on which a read may report a field as
		// erased.
		//
		// THE PRIMARY KEY IS THE PAIR (handle, generation), and this paragraph used to argue for the
		// GENERATION ALONE -- that it "is minted once and never reused, so one generation is one
		// destruction", with the handle riding along as an audit attribute and never a key component.
		// Every clause of that was true of the SHIPPED providers and none of it was enforced. A
		// generation is minted once BY EVERY PROVIDER WE SHIP; a consumer-supplied provider may DERIVE
		// one instead, and the parse accepts any 32 hex characters because it checks shape and not
		// entropy. Two distinct handles would then share one row, and one tenant's destruction would
		// answer for another tenant's live key.
		//
		// So the generation stays in the key, for the reason the old paragraph gave -- a row's existence
		// IS the destruction statement -- and the key_handle joins it, which makes the assumption
		// unnecessary rather than documented. Keying on the handle ALONE would be wrong for the reason
		// the old paragraph feared: it would drop a second destruction at one handle. The pair admits
		// many generations per handle and refuses two rows for one pair.
		var createDestroyedKeysTableSql = $@"
			CREATE TABLE IF NOT EXISTS {_options.FullDestroyedKeysTableName} (
				-- TEXT compared with the database's default collation would be wrong if that collation folded
				-- case: two distinct generations would read as one, and one destroyed generation would then
				-- answer for a different, LIVE one. The C collation compares byte-for-byte, matching the
				-- framework's ordinal comparison exactly.
				key_generation TEXT COLLATE ""C"" NOT NULL,
				-- NULLABLE: a destruction asserted by the CONSUMER has no erasure request to name. The HANDLE
				-- is not optional -- it is half the primary key, and a caller asserting a destruction knows
				-- what it destroyed.
				request_id UUID NULL,
				key_handle TEXT COLLATE ""C"" NOT NULL,
				destroyed_at TIMESTAMPTZ NOT NULL,
				-- WHO asserted the destruction, stated explicitly rather than inferred from the nulls above.
				-- Deriving a fact from a missing value is the reasoning this whole table exists to replace.
				recorded_by TEXT NOT NULL,
				-- THE PAIR, not the generation alone. The generation, because it is minted once and never
				-- reused, so a row's existence IS the destruction statement. The handle, because keying on the
				-- generation alone rests on an assumption nothing enforces -- that one generation identifies
				-- material uniquely across every handle -- which a consumer-supplied provider deriving its
				-- generations would break, giving two tenants' distinct keys one row.
				PRIMARY KEY (key_handle, key_generation)
			)";

		// The retry reads this table by request to learn which handles its earlier passes destroyed, and that
		// is no longer the primary key, so it needs its own index.
		var createDestroyedKeysIndexesSql = $@"
			CREATE INDEX IF NOT EXISTS ix_{_options.DestroyedKeysTableName}_request
				ON {_options.FullDestroyedKeysTableName} (request_id, key_handle)";

		// THE STAGING TABLE, written BEFORE the destruction, and deliberately NOT the ledger.
		//
		// The destruction destroys this write's own input: on every backend the framework supports the
		// generation identifier IS backend material, unreadable once the material is gone. So a crash between
		// destroying and recording is not a window a retry repairs -- the generation is gone, the record can
		// never be written, and the subject is left permanently undecryptable and permanently unreportable.
		// Staging first is what leaves a retry something to work from.
		//
		// It is a SECOND TABLE because between the stage and the destruction a staged row names LIVE material.
		// A predicate that could match it would report a live key as destroyed, which is the catastrophic
		// direction. One table with a nullable destroyed_at would make that a discipline -- one forgotten WHERE
		// clause away; two tables make it inexpressible, because a row's presence in the ledger is itself the
		// statement and there is no clause to forget.
		var createDestructionIntentsTableSql = $@"
			CREATE TABLE IF NOT EXISTS {_options.FullDestructionIntentsTableName} (
				request_id UUID NOT NULL,
				key_handle TEXT COLLATE ""C"" NOT NULL,
				key_generation TEXT COLLATE ""C"" NOT NULL,
				staged_at TIMESTAMPTZ NOT NULL,
				PRIMARY KEY (request_id, key_handle, key_generation)
			)";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(new CommandDefinition(createSchemaSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);
		_ = await connection.ExecuteAsync(new CommandDefinition(createRequestsTableSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);
		_ = await connection.ExecuteAsync(new CommandDefinition(createRequestsIndexesSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);
		_ = await connection.ExecuteAsync(new CommandDefinition(createCertificatesTableSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);
		_ = await connection.ExecuteAsync(new CommandDefinition(createCertificatesIndexesSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);
		_ = await connection.ExecuteAsync(new CommandDefinition(createDestroyedKeysTableSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);
		_ = await connection.ExecuteAsync(new CommandDefinition(createDestroyedKeysIndexesSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);
		_ = await connection.ExecuteAsync(new CommandDefinition(createDestructionIntentsTableSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);

		LogSchemaEnsured();
	}

	// Internal row classes for Dapper mapping - Postgres uses snake_case column names
	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Dapper materializes this type.")]
	private sealed class ErasureRequestRow
	{
		// ReSharper disable InconsistentNaming - Column names use snake_case
		public Guid request_id { get; init; }
		public string data_subject_id_hash { get; init; } = string.Empty;
		public int id_type { get; init; }
		public string? tenant_id { get; init; }
		public int scope { get; init; }
		public int legal_basis_v2 { get; init; }
		public string? external_reference { get; init; }
		public string requested_by { get; init; } = string.Empty;
		public DateTimeOffset requested_at { get; init; }
		public DateTimeOffset? scheduled_execution_at { get; init; }
		public DateTimeOffset? executed_at { get; init; }
		public DateTimeOffset? completed_at { get; init; }
		public DateTimeOffset? cancelled_at { get; init; }
		public string? cancellation_reason { get; init; }
		public string? cancelled_by { get; init; }
		public int status { get; init; }
		public int? keys_deleted { get; init; }
		public int? records_affected { get; init; }
		public Guid? certificate_id { get; init; }
		public string? error_message { get; init; }
		public DateTimeOffset updated_at { get; init; }
		// ReSharper restore InconsistentNaming

		public ErasureStatus ToStatus() => new()
		{
			RequestId = request_id,
			DataSubjectIdHash = data_subject_id_hash,
			IdType = (DataSubjectIdType)id_type,
			TenantId = tenant_id,
			Scope = (ErasureScope)scope,
			LegalBasis = (ErasureLegalBasis)legal_basis_v2,
			ExternalReference = external_reference,
			RequestedBy = requested_by,
			RequestedAt = requested_at,
			ScheduledExecutionAt = scheduled_execution_at,
			ExecutedAt = executed_at,
			CompletedAt = completed_at,
			CancelledAt = cancelled_at,
			CancellationReason = cancellation_reason,
			CancelledBy = cancelled_by,
			Status = (ErasureRequestStatus)status,
			KeysDeleted = keys_deleted,
			RecordsAffected = records_affected,
			CertificateId = certificate_id,
			ErrorMessage = error_message,
			UpdatedAt = updated_at
		};
	}

	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Dapper materializes this type.")]
	private sealed class CertificateRow
	{
		// ReSharper disable InconsistentNaming - Column names use snake_case
		public Guid certificate_id { get; init; }
		public Guid request_id { get; init; }
		public string data_subject_reference { get; init; } = string.Empty;
		public DateTimeOffset request_received_at { get; init; }
		public DateTimeOffset completed_at { get; init; }
		public int method { get; init; }
		public string summary { get; init; } = string.Empty;
		public string verification { get; init; } = string.Empty;
		public int legal_basis_v2 { get; init; }
		public string signature { get; init; } = string.Empty;
		public DateTimeOffset retain_until { get; init; }
		public string exceptions { get; init; } = "[]";
		public DateTimeOffset generated_at { get; init; }
		public string version { get; init; } = string.Empty;
		public string payload { get; init; } = string.Empty;
		// ReSharper restore InconsistentNaming

		// Restored WHOLE from the one column that holds the signed document. Deliberately not reassembled
		// from the indexed columns: a hand-written projection has to agree with the serializer forever, and
		// it cannot express a claim whose column type is narrower than the value -- which is how three of
		// this table's timestamps used to come back rounded.
		public ErasureCertificate ToCertificate() => new()
		{
			Payload = ErasureCertificateCanonicalizer.FromCanonicalJson(payload),
			Signature = signature
		};
	}
}
