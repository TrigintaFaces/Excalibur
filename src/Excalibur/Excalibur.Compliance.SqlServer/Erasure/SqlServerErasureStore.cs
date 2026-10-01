// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Dapper;

using Excalibur.Compliance.Erasure;
using Excalibur.Data.Validation;
using Excalibur.Dispatch;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.SqlServer.Erasure;

/// <summary>
/// SQL Server implementation of <see cref="IErasureStore"/> using Dapper.
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
public sealed partial class SqlServerErasureStore
	: IErasureStore, IErasureCertificateStore, IErasureQueryStore, IErasureSchemaValidator,
		IKeyDestructionLedger, IDisposable
{
	private readonly SqlServerErasureStoreOptions _options;
	private readonly IDataSubjectHasher _dataSubjectHasher;
	private readonly ILogger<SqlServerErasureStore> _logger;
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
	/// Initializes a new instance of the <see cref="SqlServerErasureStore"/> class.
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
	public SqlServerErasureStore(
		IOptions<SqlServerErasureStoreOptions> options,
		IDataSubjectHasher dataSubjectHasher,
		ILogger<SqlServerErasureStore> logger,
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

		// Defense-in-depth: validate SQL identifiers even if IValidateOptions ran at startup
		SqlIdentifierValidator.ThrowIfInvalid(_options.SchemaName, nameof(_options.SchemaName));
		SqlIdentifierValidator.ThrowIfInvalid(_options.RequestsTableName, nameof(_options.RequestsTableName));
		SqlIdentifierValidator.ThrowIfInvalid(_options.CertificatesTableName, nameof(_options.CertificatesTableName));
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
				(RequestId, DataSubjectIdHash, IdType, TenantId, Scope, LegalBasisV2,
				 ExternalReference, RequestedBy, RequestedAt, ScheduledExecutionAt,
				 Status, DataCategories, CreatedAt, UpdatedAt)
			VALUES
				(@RequestId, @DataSubjectIdHash, @IdType, @TenantId, @Scope, @LegalBasisV2,
				 @ExternalReference, @RequestedBy, @RequestedAt, @ScheduledExecutionAt,
				 @Status, @DataCategories, @CreatedAt, @UpdatedAt)";

		// The ambient term is authoritative on the write. Stamping the request's own TenantId would let a
		// caller file a request into another tenant's partition — and, because every scoped read matches on
		// the ambient term, that row would then be readable only by the tenant it was planted on.
		var tenant = AmbientScope;

		await using var connection = new SqlConnection(_options.ConnectionString);
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
					SqlServerComplianceJsonContext.Default.IReadOnlyListString)
				: null,
			CreatedAt = now,
			UpdatedAt = now
		}, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);
		}
		catch (SqlException ex) when (IsDuplicateKeyViolation(ex))
		{
			// This method inserts; it does not upsert. A caller that re-files an existing request id is
			// making a mistake, and the raw provider type is the wrong way to tell them: it forces every
			// consumer to reference the SQL Server client and to know its error numbers just to handle a
			// condition the abstraction already defines. The filter is narrow on purpose - only a
			// duplicate-key violation is translated, so a connection failure, a timeout or a constraint we
			// did not anticipate still surfaces unchanged rather than being reported as a duplicate.
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
		var tenantPredicate = _requireTenant ? " AND TenantId = @AmbientTenantId" : string.Empty;

		var sql = $@"
			SELECT RequestId, DataSubjectIdHash, IdType, TenantId, Scope, LegalBasisV2,
				   ExternalReference, RequestedBy, RequestedAt, ScheduledExecutionAt,
				   ExecutedAt, CompletedAt, CancelledAt, CancellationReason, CancelledBy,
				   Status, KeysDeleted, RecordsAffected, CertificateId, ErrorMessage, UpdatedAt
			FROM {_options.FullRequestsTableName}
			WHERE RequestId = @RequestId{tenantPredicate}";

		await using var connection = new SqlConnection(_options.ConnectionString);
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
			$"SELECT KeyHandle FROM {_options.FullDestroyedKeysTableName} WHERE RequestId = @RequestId",
			new { RequestId = requestId },
			cancellationToken: cancellationToken,
			commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		return row.ToStatus() with { DestroyedKeyHandles = [.. destroyedHandles] };
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
		var tenantPredicate = _requireTenant ? " AND TenantId = @AmbientTenantId" : string.Empty;

		var sql = $@"
			UPDATE {_options.FullRequestsTableName}
			SET Status = @Status,
				ErrorMessage = @ErrorMessage,
				ExecutedAt = CASE WHEN @Status = {(int)ErasureRequestStatus.InProgress} THEN @Now ELSE ExecutedAt END,
				UpdatedAt = @Now
			WHERE RequestId = @RequestId{tenantPredicate}
			  AND (@Status <> {(int)ErasureRequestStatus.InProgress}
			       OR Status = {(int)ErasureRequestStatus.Scheduled})";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var affected = await connection.ExecuteAsync(new CommandDefinition(sql,
			new { RequestId = requestId, Status = (int)status, ErrorMessage = errorMessage, Now = DateTimeOffset.UtcNow, AmbientTenantId = tenant.TenantId },
			cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		return affected > 0;
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
		var tenantPredicate = _requireTenant ? " AND r.TenantId = @AmbientTenantId" : string.Empty;

		// A SEPARATE TABLE from the ledger, and that is the load-bearing part of this design. Between this
		// INSERT and the destruction the row names LIVE material, so if the read predicate could match it a
		// live key would report as destroyed. Holding the two states in one table behind a nullable column
		// would make that a discipline -- one forgotten WHERE clause away. Two tables make it inexpressible:
		// the predicate resolves only against the ledger, and a ledger row exists only after a destruction.
		var sql = $@"
			INSERT INTO {_options.FullDestructionIntentsTableName} (RequestId, KeyHandle, KeyGeneration, StagedAt)
			SELECT @RequestId, @KeyHandle, @KeyGeneration, @Now
			FROM {_options.FullRequestsTableName} r
			WHERE r.RequestId = @RequestId{tenantPredicate}
			  AND NOT EXISTS (
				  SELECT 1 FROM {_options.FullDestructionIntentsTableName} i
				  WHERE i.RequestId = @RequestId AND i.KeyHandle = @KeyHandle
				    AND i.KeyGeneration = @KeyGeneration)";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		try
		{
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
				// whether zero rows was the benign already-staged case or a caller naming a request that is not
				// here.
				var requestExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
					$"SELECT COUNT(1) FROM {_options.FullRequestsTableName} r "
					+ $"WHERE r.RequestId = @RequestId{tenantPredicate}",
					new { RequestId = requestId, AmbientTenantId = tenant.TenantId },
					cancellationToken: cancellationToken,
					commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

				if (requestExists == 0)
				{
					throw new KeyNotFoundException(
						$"No erasure request with id '{requestId}' exists in this tenant, so a destruction "
						+ "cannot be staged against it. This throws rather than returning quietly: the staged "
						+ "intent is the only copy of the generation that survives the destruction, so "
						+ "proceeding without it would leave a crashed pass with nothing to recover from.");
				}
			}
		}
		catch (SqlException ex) when (IsDuplicateKeyViolation(ex))
		{
			// Two passes raced past the NOT EXISTS. The row is there either way, which is the whole
			// postcondition, so this is the success path arriving by a different route.
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
		var tenantPredicate = _requireTenant ? " AND r.TenantId = @AmbientTenantId" : string.Empty;

		// Joined to the request so another tenant's staged intents read as absent: a staged generation is a
		// backend token for live material, and one tenant must not be able to enumerate another's.
		var sql = $@"
			SELECT i.KeyGeneration
			FROM {_options.FullDestructionIntentsTableName} i
			INNER JOIN {_options.FullRequestsTableName} r ON r.RequestId = i.RequestId
			WHERE i.RequestId = @RequestId AND i.KeyHandle = @KeyHandle{tenantPredicate}";

		await using var connection = new SqlConnection(_options.ConnectionString);
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
		var tenantPredicate = _requireTenant ? " AND r.TenantId = @AmbientTenantId" : string.Empty;

		// The INSERT is gated on the request EXISTING IN THIS TENANT, in the same statement, so a record can
		// never be written into another tenant's partition and the check cannot drift from the write.
		//
		// THE EXISTENCE TEST IS ON THE GENERATION ALONE, and that is a correction rather than a detail. It used
		// to test (RequestId, KeyHandle), whose comment reasoned only about two passes recording the same
		// handle -- so a SECOND destruction at one handle, which is a different generation and a real
		// destruction, was silently absorbed as a duplicate and never recorded. A generation is minted once and
		// never reused, so a conflict on the generation is the only conflict that is genuinely a repeat.
		var sql = $@"
			INSERT INTO {_options.FullDestroyedKeysTableName} (KeyGeneration, RequestId, KeyHandle, DestroyedAt, RecordedBy)
			SELECT @KeyGeneration, @RequestId, @KeyHandle, @Now, '{RecordedBy.FrameworkErasure}'
			FROM {_options.FullRequestsTableName} r
			WHERE r.RequestId = @RequestId{tenantPredicate}
			  AND NOT EXISTS (
				  SELECT 1 FROM {_options.FullDestroyedKeysTableName} d
				  WHERE d.KeyGeneration = @KeyGeneration)";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		try
		{
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

			// Zero rows has two causes and only one of them is benign. Already recorded is benign; no such
			// request in this tenant is not, and it must not pass as "recorded" -- the caller is about to
			// attest a destruction on the strength of this record existing.
			if (affected == 0)
			{
				// WHICH QUESTION THIS ASKS IS THE WHOLE POINT, so it asks the one that decides. The two causes of
				// zero rows are "the generation was already on file" (benign) and "no such request in this
				// tenant" (not benign, and the caller is about to attest a destruction on a record that does not
				// exist). Asking whether the REQUEST exists separates them exactly. Asking whether the GENERATION
				// exists is a proxy, and the wrong one: with the ledger keyed on the generation alone, a
				// generation on file under any other request would answer yes and let a bad requestId pass as
				// benign.
				var requestExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
					$"SELECT COUNT(1) FROM {_options.FullRequestsTableName} r "
					+ $"WHERE r.RequestId = @RequestId{tenantPredicate}",
					new { RequestId = requestId, AmbientTenantId = tenant.TenantId },
					cancellationToken: cancellationToken,
					commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

				if (requestExists == 0)
				{
					throw new KeyNotFoundException(
						$"No erasure request with id '{requestId}' exists in this tenant, so a destroyed key "
						+ "cannot be recorded against it. This throws rather than returning quietly: the record "
						+ "is what lets a retry attest a destruction an earlier pass performed and what lets a "
						+ "read report the subject's erasure, so losing it silently would make that erasure "
						+ "permanently uncertifiable.");
				}
			}
		}
		catch (SqlException ex) when (IsDuplicateKeyViolation(ex))
		{
			// Two passes raced past the NOT EXISTS. The row is there either way, which is the whole
			// postcondition, so this is the success path arriving by a different route.
		}

		// ONLY HERE, and the position in this method is the whole safety argument. Control reaches this line
		// only once the ledger row for this generation is known to EXIST -- this statement inserted it, the
		// existence check found it already there, or the duplicate-key filter above caught a race that left
		// it there. The staged intent then carries nothing the ledger does not, so removing it loses no
		// recovery information.
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
		SqlConnection connection,
		Guid requestId,
		string keyHandle,
		string keyGeneration,
		CancellationToken cancellationToken)
	{
		try
		{
			_ = await connection.ExecuteAsync(new CommandDefinition(
				$"DELETE FROM {_options.FullDestructionIntentsTableName} "
				+ "WHERE RequestId = @RequestId AND KeyHandle = @KeyHandle "
				+ "AND KeyGeneration = @KeyGeneration",
				new { RequestId = requestId, KeyHandle = keyHandle, KeyGeneration = keyGeneration },
				cancellationToken: cancellationToken,
				commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);
		}
		catch (SqlException ex)
		{
			LogStagedIntentNotCleanedUp(keyHandle, ex);
		}
	}

	/// <inheritdoc />
	public async Task RecordDestroyedGenerationAsync(string keyGeneration, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(keyGeneration);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// NO request and NO handle, and they are NULL rather than a sentinel because the caller genuinely has
		// neither -- a consumer who destroyed a key through their own process has no erasure request to name.
		// The provenance column is what records that this row rests on the caller's assertion; it is not
		// inferred from the nulls.
		//
		// NOT gated on a request row existing, which is the one structural difference from the framework's own
		// write, and it is the whole point of this member: there is no request. The evidence is the caller's
		// assertion, which the contract states they own.
		//
		// NOT EXISTS plus the duplicate-key filter below, so re-asserting a generation already on file is a
		// no-op and the FIRST instant stands. A later assertion must not move a recorded destruction's
		// timestamp.
		var sql = $@"
			INSERT INTO {_options.FullDestroyedKeysTableName} (KeyGeneration, RequestId, KeyHandle, DestroyedAt, RecordedBy)
			SELECT @KeyGeneration, NULL, NULL, @Now, '{RecordedBy.CallerAssertion}'
			WHERE NOT EXISTS (
				SELECT 1 FROM {_options.FullDestroyedKeysTableName} d
				WHERE d.KeyGeneration = @KeyGeneration)";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			// Not caught beyond the race below, by contract: a caller that believed it recorded a destruction
			// it did not would see the subject's reads fail with no indication why.
			_ = await connection.ExecuteAsync(new CommandDefinition(
				sql,
				new { KeyGeneration = keyGeneration, Now = DateTimeOffset.UtcNow },
				cancellationToken: cancellationToken,
				commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);
		}
		catch (SqlException ex) when (IsDuplicateKeyViolation(ex))
		{
			// Two callers raced past the NOT EXISTS. The row is there either way, which is the whole
			// postcondition, so this is the success path arriving by a different route.
		}
	}

	/// <inheritdoc />
	public async ValueTask<bool> IsGenerationDestroyedAsync(string keyGeneration, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(keyGeneration);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// NO tenant predicate, deliberately, and NOT an oversight. The statement is addressed by the ledger's
		// whole PRIMARY KEY, so it already selects at most one row; a tenant term on such a statement cannot
		// admit a foreign row and its only reachable effect is turning the correct row into none. Here that
		// effect is the catastrophic one in reverse: a read of a genuinely erased subject would stop reporting
		// their erasure and start failing, permanently. A generation is minted by the key backend and is not
		// derivable from a subject, so one tenant cannot name another's.
		//
		// The intents table is NOT named here, and must never be: a staged generation describes material that
		// may still be live.
		var sql = $"SELECT COUNT(1) FROM {_options.FullDestroyedKeysTableName} WHERE KeyGeneration = @KeyGeneration";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var found = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
			sql,
			new { KeyGeneration = keyGeneration },
			cancellationToken: cancellationToken,
			commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		return found > 0;
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
		var tenantPredicate = _requireTenant ? " AND TenantId = @AmbientTenantId" : string.Empty;

		var sql = $@"
			UPDATE {_options.FullRequestsTableName}
			SET Status = @Status,
				KeysDeleted = @KeysDeleted,
				RecordsAffected = @RecordsAffected,
				CertificateId = @CertificateId,
				CompletedAt = @Now,
				UpdatedAt = @Now
			WHERE RequestId = @RequestId{tenantPredicate}
			  AND Status IN ({(int)ErasureRequestStatus.Scheduled}, {(int)ErasureRequestStatus.InProgress}, {(int)ErasureRequestStatus.AwaitingKeyDestruction})";

		await using var connection = new SqlConnection(_options.ConnectionString);
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
		var tenantPredicate = _requireTenant ? " AND TenantId = @AmbientTenantId" : string.Empty;

		var sql = $@"
			UPDATE {_options.FullRequestsTableName}
			SET Status = @Status,
				CancellationReason = @Reason,
				CancelledBy = @CancelledBy,
				CancelledAt = @Now,
				UpdatedAt = @Now
			WHERE RequestId = @RequestId
			  AND Status IN ({(int)ErasureRequestStatus.Pending}, {(int)ErasureRequestStatus.Scheduled}){tenantPredicate}";

		await using var connection = new SqlConnection(_options.ConnectionString);
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
			SELECT TOP (@MaxResults)
				   RequestId, DataSubjectIdHash, IdType, TenantId, Scope, LegalBasisV2,
				   ExternalReference, RequestedBy, RequestedAt, ScheduledExecutionAt,
				   ExecutedAt, CompletedAt, CancelledAt, CancellationReason, CancelledBy,
				   Status, KeysDeleted, RecordsAffected, CertificateId, ErrorMessage, UpdatedAt
			FROM {_options.FullRequestsTableName}
			WHERE Status = {(int)ErasureRequestStatus.Scheduled}
			  AND ScheduledExecutionAt <= @Now
			ORDER BY ScheduledExecutionAt";

		await using var connection = new SqlConnection(_options.ConnectionString);
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
			whereClauses.Add("TenantId = @AmbientTenantId");
			parameters.Add("AmbientTenantId", tenant.TenantId);
		}

		if (status.HasValue)
		{
			whereClauses.Add("Status = @Status");
			parameters.Add("Status", (int)status.Value);
		}

		if (!string.IsNullOrEmpty(tenantId))
		{
			whereClauses.Add("TenantId = @TenantId");
			parameters.Add("TenantId", tenantId);
		}

		if (fromDate.HasValue)
		{
			whereClauses.Add("RequestedAt >= @FromDate");
			parameters.Add("FromDate", fromDate.Value);
		}

		if (toDate.HasValue)
		{
			whereClauses.Add("RequestedAt <= @ToDate");
			parameters.Add("ToDate", toDate.Value);
		}

		var whereClause = whereClauses.Count > 0
			? "WHERE " + string.Join(" AND ", whereClauses)
			: string.Empty;

		var offset = (pageNumber - 1) * pageSize;
		parameters.Add("Offset", offset);
		parameters.Add("PageSize", pageSize);

		var sql = $@"
			SELECT RequestId, DataSubjectIdHash, IdType, TenantId, Scope, LegalBasisV2,
				   ExternalReference, RequestedBy, RequestedAt, ScheduledExecutionAt,
				   ExecutedAt, CompletedAt, CancelledAt, CancellationReason, CancelledBy,
				   Status, KeysDeleted, RecordsAffected, CertificateId, ErrorMessage, UpdatedAt
			FROM {_options.FullRequestsTableName}
			{whereClause}
			ORDER BY RequestedAt DESC
			OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

		await using var connection = new SqlConnection(_options.ConnectionString);
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
				(CertificateId, RequestId, DataSubjectReference, RequestReceivedAt, CompletedAt,
				 Method, Summary, Verification, LegalBasisV2, Signature, RetainUntil,
				 Exceptions, UnreachedData, GeneratedAt, Version, CreatedAt)
			VALUES
				(@CertificateId, @RequestId, @DataSubjectReference, @RequestReceivedAt, @CompletedAt,
				 @Method, @Summary, @Verification, @LegalBasisV2, @Signature, @RetainUntil,
				 @Exceptions, @UnreachedData, @GeneratedAt, @Version, @CreatedAt)";

		await using var connection = new SqlConnection(_options.ConnectionString);
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
				SqlServerComplianceJsonContext.Default.ErasureSummary),
			Verification = JsonSerializer.Serialize(
				certificate.Payload.Verification,
				SqlServerComplianceJsonContext.Default.VerificationSummary),
			LegalBasisV2 = (int)certificate.Payload.LegalBasis,
			certificate.Signature,
			certificate.Payload.RetainUntil,
			Exceptions = JsonSerializer.Serialize(
				certificate.Payload.Exceptions,
				SqlServerComplianceJsonContext.Default.IReadOnlyListErasureException),

			// NULL, never "[]". The signature covers the canonical form, which OMITS this property when
			// it is null. Storing an empty array would restore a non-null empty list, the canonical form
			// would then emit it, and a certificate issued before this column existed would verify as a
			// forgery on its way back out of the store.
			UnreachedData = certificate.Payload.UnreachedData is null
				? null
				: JsonSerializer.Serialize(
					certificate.Payload.UnreachedData,
					SqlServerComplianceJsonContext.Default.IReadOnlyListUnreachedDataLocation),
			certificate.Payload.GeneratedAt,
			certificate.Payload.Version,
			CreatedAt = DateTimeOffset.UtcNow
		}, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);
		}
		catch (SqlException ex) when (IsDuplicateKeyViolation(ex))
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
				  WHERE r.RequestId = {_options.FullCertificatesTableName}.RequestId AND r.TenantId = @AmbientTenantId)"
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
			SELECT CertificateId, RequestId, DataSubjectReference, RequestReceivedAt, CompletedAt,
				   Method, Summary, Verification, LegalBasisV2, Signature, RetainUntil,
				   Exceptions, UnreachedData, GeneratedAt, Version
			FROM {_options.FullCertificatesTableName}
			WHERE RequestId = @RequestId{tenantPredicate}
			ORDER BY GeneratedAt DESC, CertificateId DESC";

		await using var connection = new SqlConnection(_options.ConnectionString);
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
				  WHERE r.RequestId = {_options.FullCertificatesTableName}.RequestId AND r.TenantId = @AmbientTenantId)"
			: string.Empty;

		var sql = $@"
			SELECT CertificateId, RequestId, DataSubjectReference, RequestReceivedAt, CompletedAt,
				   Method, Summary, Verification, LegalBasisV2, Signature, RetainUntil,
				   Exceptions, UnreachedData, GeneratedAt, Version
			FROM {_options.FullCertificatesTableName}
			WHERE CertificateId = @CertificateId{tenantPredicate}";

		await using var connection = new SqlConnection(_options.ConnectionString);
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
			WHERE RetainUntil < @Now";

		await using var connection = new SqlConnection(_options.ConnectionString);
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

	private static VerificationSummary CreateDefaultVerificationSummary() => new()
	{
		Verified = false,
		Methods = VerificationMethod.None,
		VerifiedAt = DateTimeOffset.MinValue
	};

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

	[LoggerMessage(LogLevel.Debug, "Ensured SQL Server erasure schema and tables exist")]
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
		// arrives later as a raw "Invalid column name" far from its cause. Automatic schema creation does not
		// repair that database either -- it only creates tables that are absent.
		const string ColumnsSql =
			"SELECT c.name FROM sys.columns c WHERE c.object_id = OBJECT_ID(@TableName, 'U')";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		foreach (var (tableName, requiredColumns) in RequiredSchema)
		{
			var actualColumns = (await connection.QueryAsync<string>(
				new CommandDefinition(
					ColumnsSql,
					new { TableName = tableName },
					cancellationToken: cancellationToken,
					commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false)).ToList();

			// No columns at all means no such table: sys.columns joins on OBJECT_ID, which is NULL for a
			// table that does not exist, so the same query answers both questions and they stay in step.
			if (actualColumns.Count == 0)
			{
				throw new ErasureStoreNotProvisionedException(
					$"Required table '{tableName}' does not exist and automatic schema creation is disabled. " +
					$"Either create the schema out of band, or set {nameof(SqlServerErasureStoreOptions)}."
					+ $"{nameof(SqlServerErasureStoreOptions.AutoCreateSchema)} to true to provision it on startup.")
				{
					TableName = tableName,
				};
			}

			// Named, not counted. An operator reading this at startup needs to know WHICH columns are absent
			// to choose the migration; "the schema is stale" sends them to diff it by hand.
			var missing = requiredColumns
				.Where(required => !actualColumns.Contains(required, StringComparer.OrdinalIgnoreCase))
				.ToList();

			if (missing.Count > 0)
			{
				throw new ErasureStoreNotProvisionedException(
					$"Table '{tableName}' exists but is missing {missing.Count} column(s) that this store's "
					+ $"statements bind: {string.Join(", ", missing)}. This is a schema provisioned before those "
					+ "columns were introduced. Enabling automatic schema creation will NOT repair it, because "
					+ "that path only creates tables that are absent. If the missing column names end in V2, "
					+ "the package ships the migration that adds them -- apply "
					+ "'003_ReplaceLegalBasisColumns.sql' and restart. For any other missing column we ship no "
					+ "alter script: the create scripts already declare them, so a freshly provisioned database "
					+ "is correct and only a pre-existing one reaches this. Either re-provision from the shipped "
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
	/// end of an erasure rather than at startup.
	/// </remarks>
	private IEnumerable<(string TableName, string[] RequiredColumns)> RequiredSchema =>
	[
		(_options.FullRequestsTableName,
		[
			"RequestId", "DataSubjectIdHash", "IdType", "TenantId", "Scope", "LegalBasisV2",
			"ExternalReference", "RequestedBy", "RequestedAt", "ScheduledExecutionAt",
			"ExecutedAt", "CompletedAt", "CancelledAt", "CancellationReason", "CancelledBy",
			"Status", "KeysDeleted", "RecordsAffected", "CertificateId", "ErrorMessage",
			"DataCategories", "CreatedAt", "UpdatedAt",
		]),
		(_options.FullCertificatesTableName,
		[
			"CertificateId", "RequestId", "DataSubjectReference", "RequestReceivedAt", "CompletedAt",
			"Method", "Summary", "Verification", "LegalBasisV2", "Signature", "RetainUntil",
			"Exceptions", "UnreachedData", "GeneratedAt", "Version", "CreatedAt",
		]),

		// Verified like the others, so a database provisioned before this table existed fails at STARTUP with
		// the table named, rather than mid-erasure. The failure is loud on purpose: without this table a
		// retried erasure silently attests less coverage than the request achieved, and the subject's
		// completion becomes unreachable with nothing reporting why.
		(_options.FullDestroyedKeysTableName,
		[
			"KeyGeneration", "RequestId", "KeyHandle", "DestroyedAt", "RecordedBy",
		]),

		// Verified for the same reason, and the failure it prevents is worse. Without this table a destruction
		// proceeds with nowhere to stage the generation it is about to annihilate, so a crash between the
		// destroy and the record leaves the subject's ciphertext permanently undecryptable AND their erasure
		// permanently unreportable, with no repair available at all. Naming the table at STARTUP is the only
		// place that is still cheap.
		(_options.FullDestructionIntentsTableName,
		[
			"RequestId", "KeyHandle", "KeyGeneration", "StagedAt",
		]),
	];

	/// <summary>
	/// Indicates whether a SQL Server error is a primary-key or unique-index violation (2627 / 2601).
	/// </summary>
	/// <remarks>
	/// Used as an exception filter so only this one condition is translated. A broad catch would report
	/// an unrelated failure - a dropped connection, a timeout, a check constraint - as a duplicate, which
	/// is worse than not translating at all: the caller would be told the row exists when it does not.
	/// </remarks>
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

	private static bool IsDuplicateKeyViolation(SqlException ex)
		=> ex.Number is 2627 or 2601;

	private async Task CreateSchemaIfNotExistsAsync(CancellationToken cancellationToken)
	{
		var createSchemaSql = $@"
			IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = '{_options.SchemaName}')
			BEGIN
				EXEC('CREATE SCHEMA [{_options.SchemaName}]')
			END";

		var createRequestsTableSql = $@"
			IF NOT EXISTS (SELECT 1 FROM sys.tables t
				JOIN sys.schemas s ON t.schema_id = s.schema_id
				WHERE s.name = '{_options.SchemaName}' AND t.name = '{_options.RequestsTableName}')
			BEGIN
				CREATE TABLE {_options.FullRequestsTableName} (
					RequestId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
					DataSubjectIdHash NVARCHAR(128) NOT NULL,
					IdType INT NOT NULL,
					TenantId NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL
						CONSTRAINT DF_{_options.RequestsTableName}_TenantId DEFAULT '{TenantScope.UntenantedSentinel}',
					Scope INT NOT NULL,
					LegalBasisV2 INT NOT NULL,
					ExternalReference NVARCHAR(256) NULL,
					RequestedBy NVARCHAR(256) NOT NULL,
					RequestedAt DATETIMEOFFSET NOT NULL,
					ScheduledExecutionAt DATETIMEOFFSET NULL,
					ExecutedAt DATETIMEOFFSET NULL,
					CompletedAt DATETIMEOFFSET NULL,
					CancelledAt DATETIMEOFFSET NULL,
					CancellationReason NVARCHAR(1000) NULL,
					CancelledBy NVARCHAR(256) NULL,
					Status INT NOT NULL,
					KeysDeleted INT NULL,
					RecordsAffected INT NULL,
					CertificateId UNIQUEIDENTIFIER NULL,
					ErrorMessage NVARCHAR(2000) NULL,
					DataCategories NVARCHAR(MAX) NULL,
					CreatedAt DATETIMEOFFSET NOT NULL,
					UpdatedAt DATETIMEOFFSET NOT NULL,
					INDEX IX_{_options.RequestsTableName}_Status (Status, ScheduledExecutionAt),
					INDEX IX_{_options.RequestsTableName}_TenantId (TenantId, RequestedAt),
					INDEX IX_{_options.RequestsTableName}_DataSubject (DataSubjectIdHash)
				)
			END";

		var createCertificatesTableSql = $@"
			IF NOT EXISTS (SELECT 1 FROM sys.tables t
				JOIN sys.schemas s ON t.schema_id = s.schema_id
				WHERE s.name = '{_options.SchemaName}' AND t.name = '{_options.CertificatesTableName}')
			BEGIN
				CREATE TABLE {_options.FullCertificatesTableName} (
					CertificateId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
					RequestId UNIQUEIDENTIFIER NOT NULL,
					DataSubjectReference NVARCHAR(256) NOT NULL,
					RequestReceivedAt DATETIMEOFFSET NOT NULL,
					CompletedAt DATETIMEOFFSET NOT NULL,
					Method INT NOT NULL,
					Summary NVARCHAR(MAX) NOT NULL,
					Verification NVARCHAR(MAX) NOT NULL,
					LegalBasisV2 INT NOT NULL,
					Signature NVARCHAR(512) NOT NULL,
					RetainUntil DATETIMEOFFSET NOT NULL,
					-- Every remaining payload claim gets a column. The signature covers the payload WHOLE, so a
					-- field with nowhere to live here does not merely go missing on read -- the reassembled
					-- payload no longer matches what was signed, and the certificate reports as TAMPERED.
					-- Exceptions is the Article 17(3) record of data lawfully RETAINED; losing it makes the
					-- certificate attest a more complete erasure than occurred.
					Exceptions NVARCHAR(MAX) NOT NULL,
					-- NULLABLE, unlike Exceptions, and that is load-bearing rather than incidental. The
					-- signature covers the payload's canonical form, which omits this property when it is
					-- null. A NOT NULL column defaulting to an empty array would restore an empty list, the
					-- canonical form would then emit it, and every certificate issued before this column
					-- existed would come back out of the store verifying as a forgery.
					UnreachedData NVARCHAR(MAX) NULL,
					GeneratedAt DATETIMEOFFSET NOT NULL,
					Version NVARCHAR(16) NOT NULL,
					CreatedAt DATETIMEOFFSET NOT NULL,
					INDEX IX_{_options.CertificatesTableName}_RequestId (RequestId),
					INDEX IX_{_options.CertificatesTableName}_RetainUntil (RetainUntil)
				)
			END";

		// THE LEDGER. A row here IS the statement that this generation's material was irreversibly destroyed by
		// an erasure this framework performed, and it is the sole basis on which a read may report a field as
		// erased.
		//
		// The PRIMARY KEY is the GENERATION ALONE. A generation is minted once and never reused, so one
		// generation is one destruction and two rows for it are a contradiction the database refuses -- which
		// DISSOLVES a retried erasure, two erasures of one subject, and a handle reused by a different subject
		// rather than defending against each. RequestId and KeyHandle ride along as audit attributes and are
		// never key components; the read predicate names neither.
		var createDestroyedKeysTableSql = $@"
			IF NOT EXISTS (SELECT 1 FROM sys.tables t
				JOIN sys.schemas s ON t.schema_id = s.schema_id
				WHERE s.name = '{_options.SchemaName}' AND t.name = '{_options.DestroyedKeysTableName}')
			BEGIN
				CREATE TABLE {_options.FullDestroyedKeysTableName} (
					-- Binary collation, so the database compares generations exactly as the framework does
					-- ordinally. A case-insensitive collation would treat two distinct generations as one, and
					-- one destroyed generation would then answer for a different, LIVE one.
					KeyGeneration NVARCHAR(256) COLLATE Latin1_General_BIN2 NOT NULL,
					-- NULLABLE, because a destruction asserted by the CONSUMER through the ledger's public
					-- write has no erasure request and no handle to name. Audit attributes either way; the
					-- read predicate names neither.
					RequestId UNIQUEIDENTIFIER NULL,
					KeyHandle NVARCHAR(256) COLLATE Latin1_General_BIN2 NULL,
					DestroyedAt DATETIMEOFFSET NOT NULL,
					-- WHO asserted the destruction, stated explicitly rather than inferred from the nulls
					-- above. Deriving a fact from a missing value is the reasoning this table exists to replace.
					RecordedBy NVARCHAR(32) NOT NULL,
					CONSTRAINT PK_{_options.DestroyedKeysTableName} PRIMARY KEY (KeyGeneration)
				);
				-- The retry reads this table by request to learn which handles its earlier passes destroyed,
				-- and that is no longer the primary key, so it needs its own index.
				CREATE INDEX IX_{_options.DestroyedKeysTableName}_Request
					ON {_options.FullDestroyedKeysTableName} (RequestId, KeyHandle);
			END";

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
		// direction. One table with a nullable DestroyedAt would make that a discipline -- one forgotten WHERE
		// clause away; two tables make it inexpressible, because a row's presence in the ledger is itself the
		// statement and there is no clause to forget.
		var createDestructionIntentsTableSql = $@"
			IF NOT EXISTS (SELECT 1 FROM sys.tables t
				JOIN sys.schemas s ON t.schema_id = s.schema_id
				WHERE s.name = '{_options.SchemaName}' AND t.name = '{_options.DestructionIntentsTableName}')
			BEGIN
				CREATE TABLE {_options.FullDestructionIntentsTableName} (
					RequestId UNIQUEIDENTIFIER NOT NULL,
					KeyHandle NVARCHAR(256) COLLATE Latin1_General_BIN2 NOT NULL,
					KeyGeneration NVARCHAR(256) COLLATE Latin1_General_BIN2 NOT NULL,
					StagedAt DATETIMEOFFSET NOT NULL,
					CONSTRAINT PK_{_options.DestructionIntentsTableName}
						PRIMARY KEY (RequestId, KeyHandle, KeyGeneration)
				)
			END";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(new CommandDefinition(createSchemaSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);
		_ = await connection.ExecuteAsync(new CommandDefinition(createRequestsTableSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);
		_ = await connection.ExecuteAsync(new CommandDefinition(createCertificatesTableSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);
		_ = await connection.ExecuteAsync(new CommandDefinition(createDestroyedKeysTableSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);
		_ = await connection.ExecuteAsync(new CommandDefinition(createDestructionIntentsTableSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);

		LogSchemaEnsured();
	}

	// Internal row classes for Dapper mapping
	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Dapper materializes this type.")]
	private sealed class ErasureRequestRow
	{
		public Guid RequestId { get; init; }
		public string DataSubjectIdHash { get; init; } = string.Empty;
		public int IdType { get; init; }
		public string? TenantId { get; init; }
		public int Scope { get; init; }
		public int LegalBasisV2 { get; init; }
		public string? ExternalReference { get; init; }
		public string RequestedBy { get; init; } = string.Empty;
		public DateTimeOffset RequestedAt { get; init; }
		public DateTimeOffset? ScheduledExecutionAt { get; init; }
		public DateTimeOffset? ExecutedAt { get; init; }
		public DateTimeOffset? CompletedAt { get; init; }
		public DateTimeOffset? CancelledAt { get; init; }
		public string? CancellationReason { get; init; }
		public string? CancelledBy { get; init; }
		public int Status { get; init; }
		public int? KeysDeleted { get; init; }
		public int? RecordsAffected { get; init; }
		public Guid? CertificateId { get; init; }
		public string? ErrorMessage { get; init; }
		public DateTimeOffset UpdatedAt { get; init; }

		public ErasureStatus ToStatus() => new()
		{
			RequestId = RequestId,
			DataSubjectIdHash = DataSubjectIdHash,
			IdType = (DataSubjectIdType)IdType,
			TenantId = TenantId,
			Scope = (ErasureScope)Scope,
			LegalBasis = (ErasureLegalBasis)LegalBasisV2,
			ExternalReference = ExternalReference,
			RequestedBy = RequestedBy,
			RequestedAt = RequestedAt,
			ScheduledExecutionAt = ScheduledExecutionAt,
			ExecutedAt = ExecutedAt,
			CompletedAt = CompletedAt,
			CancelledAt = CancelledAt,
			CancellationReason = CancellationReason,
			CancelledBy = CancelledBy,
			Status = (ErasureRequestStatus)Status,
			KeysDeleted = KeysDeleted,
			RecordsAffected = RecordsAffected,
			CertificateId = CertificateId,
			ErrorMessage = ErrorMessage,
			UpdatedAt = UpdatedAt
		};
	}

	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Dapper materializes this type.")]
	private sealed class CertificateRow
	{
		public Guid CertificateId { get; init; }
		public Guid RequestId { get; init; }
		public string DataSubjectReference { get; init; } = string.Empty;
		public DateTimeOffset RequestReceivedAt { get; init; }
		public DateTimeOffset CompletedAt { get; init; }
		public int Method { get; init; }
		public string Summary { get; init; } = string.Empty;
		public string Verification { get; init; } = string.Empty;
		public int LegalBasisV2 { get; init; }
		public string Signature { get; init; } = string.Empty;
		public DateTimeOffset RetainUntil { get; init; }
		public string Exceptions { get; init; } = "[]";
		public string? UnreachedData { get; init; }
		public DateTimeOffset GeneratedAt { get; init; }
		public string Version { get; init; } = string.Empty;

		public ErasureCertificate ToCertificate() => new()
		{
			Payload = new()
			{
				CertificateId = CertificateId,
				RequestId = RequestId,
				DataSubjectReference = DataSubjectReference,
				RequestReceivedAt = RequestReceivedAt,
				CompletedAt = CompletedAt,
				Method = (ErasureMethod)Method,
				Summary = JsonSerializer.Deserialize(
				Summary,
				SqlServerComplianceJsonContext.Default.ErasureSummary) ?? new ErasureSummary(),
				Verification = JsonSerializer.Deserialize(
				Verification,
				SqlServerComplianceJsonContext.Default.VerificationSummary) ?? CreateDefaultVerificationSummary(),
				LegalBasis = (ErasureLegalBasis)LegalBasisV2,
				Exceptions = JsonSerializer.Deserialize(
					Exceptions,
					SqlServerComplianceJsonContext.Default.IReadOnlyListErasureException) ?? [],

				// Null stays null. Coercing it to an empty list here is the same forgery bug as storing
				// "[]" above, arriving from the read side.
				UnreachedData = UnreachedData is null
					? null
					: JsonSerializer.Deserialize(
						UnreachedData,
						SqlServerComplianceJsonContext.Default.IReadOnlyListUnreachedDataLocation),
				GeneratedAt = GeneratedAt,
				Version = Version,
				RetainUntil = RetainUntil
			},
			Signature = Signature
		};
	}
}
