// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

// ddl-migration: provisioning-only {_options.FullRequestsTableName} - Earlier alpha tenant identity and partition schemas require fresh provisioning; preserve the earlier schema separately.
// ddl-migration: provisioning-only {_options.FullDestroyedKeysTableName} - Earlier alpha ledger identities require a new schema; no record is deleted or backfilled.
// ddl-migration: provisioning-only {_options.FullMintedKeysTableName} - Earlier prerelease mint-ledger identity and intent schemas require a new dedicated schema; retain the earlier schema and destruction evidence, with no in-place backfill.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Transactions;

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
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Maintainability",
	"CA1506:AvoidExcessiveClassCoupling",
	Justification =
		"This store implements eight contracts, and the claim fence added the three custody result types "
		+ "plus two verdict readers, taking it past the threshold of 96. The threshold is a smell and the "
		+ "fence is a correctness invariant: without it two executors each write a signed erasure "
		+ "certificate over coverage neither established, which a regulated consumer hands to an auditor. "
		+ "Naming the trade rather than hiding it -- the coupling is real and is tracked as debt. Splitting "
		+ "the store along its eight interfaces is the fix, and it is not a change to make in the same "
		+ "commit as a fence.")]
public sealed partial class SqlServerErasureStore
	: IErasureStore, IErasureCertificateStore, IErasureQueryStore, IErasureBackgroundQueryStore, IErasureSchemaValidator,
		IDataSubjectTokenFingerprintLedger,
		IKeyDestructionLedger, IKeyMintLedger, IDisposable
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
		var tenant = AmbientScope;
		var owner = ErasureRequestOwner.Resolve(_requireTenant, tenant, request, _tenantContext);

		// THE PARTITION IS THE SECOND FACT, decided here and persisted, from whether multi-tenancy is
		// configured rather than from the shape of the tenant term -- so a tenant named like the
		// single-tenant default is harmless. Deciding it at filing time keeps a certificate reproducible
		// if the deployment is reconfigured before the request executes.
		var partition = _requireTenant
			? KeyedTenantPartition.FromContext(_tenantContext)
			: KeyedTenantPartition.Untenanted;
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			INSERT INTO {_options.FullRequestsTableName}
				(RequestId, DataSubjectIdHash, IdType, TenantId, TenantPartition, Scope, LegalBasisV2,
				 ExternalReference, RequestedBy, RequestedAt, ScheduledExecutionAt,
				 Status, DataCategories, CreatedAt, UpdatedAt)
			VALUES
				(@RequestId, @DataSubjectIdHash, @IdType, @TenantId, @TenantPartition, @Scope, @LegalBasisV2,
				 @ExternalReference, @RequestedBy, @RequestedAt, @ScheduledExecutionAt,
				 @Status, @DataCategories, @CreatedAt, @UpdatedAt)";

		// The ambient term is authoritative on the write. Stamping the request's own TenantId would let a
		// caller file a request into another tenant's partition — and, because every scoped read matches on
		// the ambient term, that row would then be readable only by the tenant it was planted on.


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
			TenantId = owner,
			TenantPartition = partition.TenantId,
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
		var tenantPredicate = _requireTenant ? " AND TenantId = @AmbientTenantId AND DATALENGTH(TenantId) = DATALENGTH(@AmbientTenantId)" : string.Empty;

		var sql = $@"
			SELECT RequestId, DataSubjectIdHash, IdType, TenantId, TenantPartition, Scope, LegalBasisV2,
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
	public async Task<ErasureWriteOutcome> UpdateStatusAsync(
		Guid requestId,
		ErasureRequestStatus status,
		ErasureRequestStatus? expectedStatus,
		string? errorMessage,
		string? claimIdentity,
		CancellationToken cancellationToken)
	{
		// Custody has its own door. Refused rather than quietly handled: a second door would advance a
		// request into the claimed status for a caller holding no identity, and every fenced write it
		// then made would compare against nothing.
		if (status == ErasureRequestStatus.InProgress)
		{
			throw new ArgumentException(
				"The in-progress status records CUSTODY and may only be written by ClaimForExecutionAsync, "
				+ "which is the only member that mints the identity a fenced write presents.",
				nameof(status));
		}

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var tenant = AmbientScope;
		var tenantPredicate = _requireTenant ? " AND TenantId = @AmbientTenantId AND DATALENGTH(TenantId) = DATALENGTH(@AmbientTenantId)" : string.Empty;

		// THE WRITE FIRST, then the diagnostics, and only when nothing was written. A bare affected-row
		// count cannot tell "no such request" from "another claim holds it" from "that transition is
		// illegal", and those three oblige different things of the caller: report it, stand down
		// silently, report a defect. The UPDATE remains the single atomic act; the counts below only
		// explain a refusal that has already happened, so a concurrent change between them can change
		// the EXPLANATION and never the outcome.
		//
		// THE CLAIM TERM, and the expiry appears in ONE arm of it only. A presented identity is compared
		// for equality and nothing else, because A LAPSE DOES NOT REVOKE -- a holder whose run outlasted
		// its own lease but whom nobody superseded still owns the row and its writes still apply. The
		// expiry is consulted only when NO identity is presented, which is a supervisor asserting that
		// nobody is working on the request.
		var sql = $@"
			SET NOCOUNT ON;

			UPDATE {_options.FullRequestsTableName}
			SET Status = @Status,
				ErrorMessage = @ErrorMessage,
				UpdatedAt = @Now,
				ClaimIdentity = NULL,
				ClaimExpiresAt = NULL
			WHERE RequestId = @RequestId{tenantPredicate}
			  AND (@ExpectedStatus IS NULL OR Status = @ExpectedStatus)
			  -- A LEGAL HOLD IS OUTSIDE THE FENCE, and for the same reason the destruction ledger is:
			  -- refusing it loses something the law requires rather than preventing a false claim. A hold
			  -- recorded while an executor holds the claim MUST land, or the hold is silently dropped and
			  -- the run completes over data the controller asserted a basis to retain. The asymmetry is
			  -- this subsystem's own: withholding an erasure is recoverable by releasing the hold;
			  -- destroying held records is not.
			  AND (@Status = {(int)ErasureRequestStatus.BlockedByLegalHold} OR (CASE
			         WHEN @ClaimIdentity IS NULL
			           THEN CASE WHEN ClaimIdentity IS NULL OR ClaimExpiresAt <= SYSUTCDATETIME() THEN 1 ELSE 0 END
			         ELSE CASE WHEN ClaimIdentity = @ClaimIdentity AND DATALENGTH(ClaimIdentity) = DATALENGTH(@ClaimIdentity) THEN 1 ELSE 0 END
			       END) = 1)
			  AND (@Status <> {(int)ErasureRequestStatus.Scheduled}
			       OR Status NOT IN ({(int)ErasureRequestStatus.InProgress}, {(int)ErasureRequestStatus.AwaitingKeyDestruction}, {(int)ErasureRequestStatus.Completed}, {(int)ErasureRequestStatus.Cancelled}))
			  AND (@Status <> {(int)ErasureRequestStatus.AwaitingKeyDestruction}
			       OR Status IN ({(int)ErasureRequestStatus.InProgress}, {(int)ErasureRequestStatus.AwaitingKeyDestruction}));

			DECLARE @Written INT = @@ROWCOUNT;

			SELECT
				@Written AS WrittenCount,
				(SELECT COUNT(*) FROM {_options.FullRequestsTableName}
				 WHERE RequestId = @RequestId{tenantPredicate}) AS TargetCount,
				(SELECT COUNT(*) FROM {_options.FullRequestsTableName}
				 WHERE RequestId = @RequestId{tenantPredicate}
				   AND (CASE
				          WHEN @ClaimIdentity IS NULL
				            THEN CASE WHEN ClaimIdentity IS NULL OR ClaimExpiresAt <= SYSUTCDATETIME() THEN 1 ELSE 0 END
				          ELSE CASE WHEN ClaimIdentity = @ClaimIdentity AND DATALENGTH(ClaimIdentity) = DATALENGTH(@ClaimIdentity) THEN 1 ELSE 0 END
				        END) = 1) AS ClaimOk;";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var verdict = await connection.QuerySingleAsync<StatusWriteVerdict>(new CommandDefinition(sql,
			new
			{
				RequestId = requestId,
				Status = (int)status,
				ExpectedStatus = expectedStatus is null ? (int?)null : (int)expectedStatus,
				ErrorMessage = errorMessage,
				// The audit stamp, and ONLY the audit stamp. Every lapse comparison above reads
				// SYSUTCDATETIME() at the server: a process-side instant there would make a lapse a
				// comparison between two machines' clocks, which is the one comparison that may never
				// decide anything.
				Now = DateTimeOffset.UtcNow,
				// Plain string so the driver infers a wide text parameter rather than the column's own
				// width. A parameter typed to nvarchar(64) would silently truncate a 65-character
				// presented identity into a match against a 64-character stored one -- and the
				// DATALENGTH comparisons above are the second half of that guard, matching the tenant
				// predicate's own idiom.
				ClaimIdentity = claimIdentity,
				AmbientTenantId = tenant.TenantId
			},
			cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		return verdict.ToOutcome();
	}

	/// <inheritdoc />
	public async Task<ErasureClaimResult> ClaimForExecutionAsync(
		Guid requestId,
		ErasureRequestStatus expectedStatus,
		TimeSpan claimDuration,
		CancellationToken cancellationToken)
	{
		// Zero is legal and yields an already-lapsed claim, which is the only lever a provider-agnostic
		// conformance test has to construct one without controlling a clock. Negative is not: it would be
		// a claim that expired before it was granted, which no caller can mean.
		ArgumentOutOfRangeException.ThrowIfLessThan(claimDuration, TimeSpan.Zero);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var tenant = AmbientScope;
		var tenantPredicate = _requireTenant ? " AND TenantId = @AmbientTenantId AND DATALENGTH(TenantId) = DATALENGTH(@AmbientTenantId)" : string.Empty;

		// ONE conditional UPDATE, so the status test, the status write, the identity and the expiry all
		// become visible together or not at all. Two statements would let a second caller observe the
		// status it needs between them, be granted as well, and then write past a fence comparing
		// against its own identity -- a weakened postcondition no sequential test can observe.
		//
		// The expiry is DATEADD over SYSUTCDATETIME() AT THE SERVER, and every lapse test compares
		// against SYSUTCDATETIME() too. Nothing here takes an instant from the caller.
		//
		// Claiming from Scheduled ADVANCES the status; claiming from AwaitingKeyDestruction leaves it
		// alone, because that status is a STAGE of the work rather than a statement about who is doing
		// it. One unconditional SET would silently un-stage a request mid-confirmation.
		var sql = $@"
			SET NOCOUNT ON;

			UPDATE {_options.FullRequestsTableName}
			SET ClaimIdentity = @ClaimIdentity,
				ClaimExpiresAt = DATEADD(MILLISECOND, @ClaimMilliseconds, SYSUTCDATETIME()),
				Status = CASE WHEN @ExpectedStatus = {(int)ErasureRequestStatus.Scheduled} THEN {(int)ErasureRequestStatus.InProgress} ELSE Status END,
				ExecutedAt = CASE WHEN @ExpectedStatus = {(int)ErasureRequestStatus.Scheduled} THEN @Now ELSE ExecutedAt END,
				UpdatedAt = @Now
			WHERE RequestId = @RequestId{tenantPredicate}
			  AND Status = @ExpectedStatus
			  AND (ClaimIdentity IS NULL OR ClaimExpiresAt <= SYSUTCDATETIME());

			DECLARE @Granted INT = @@ROWCOUNT;

			SELECT
				@Granted AS GrantedCount,
				(SELECT COUNT(*) FROM {_options.FullRequestsTableName}
				 WHERE RequestId = @RequestId{tenantPredicate}) AS TargetCount,
				(SELECT COUNT(*) FROM {_options.FullRequestsTableName}
				 WHERE RequestId = @RequestId{tenantPredicate}
				   AND Status = @ExpectedStatus) AS StatusOk,
				(SELECT COUNT(*) FROM {_options.FullRequestsTableName}
				 WHERE RequestId = @RequestId{tenantPredicate}
				   AND ClaimIdentity IS NOT NULL AND ClaimExpiresAt > SYSUTCDATETIME()) AS LiveClaim;";

		// Minted here and nowhere else, and opaque by construction: a GUID carries no order anyone could
		// compare, which is what the contract requires of it.
		var identity = Guid.NewGuid().ToString("N");

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var verdict = await connection.QuerySingleAsync<ClaimVerdict>(new CommandDefinition(sql,
			new
			{
				RequestId = requestId,
				ExpectedStatus = (int)expectedStatus,
				ClaimIdentity = identity,
				// Milliseconds rather than binding the TimeSpan: DATEADD takes an integer, and an
				// explicit unit is one fewer driver conversion to reason about in a correctness path.
				ClaimMilliseconds = (int)Math.Min(claimDuration.TotalMilliseconds, int.MaxValue),
				Now = DateTimeOffset.UtcNow,
				AmbientTenantId = tenant.TenantId
			},
			cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		if (verdict.GrantedCount > 0)
		{
			return ErasureClaimResult.Granted(identity);
		}

		if (verdict.TargetCount == 0)
		{
			return ErasureClaimResult.NotFound();
		}

		// CUSTODY IS TESTED BEFORE STATUS, and the order is load-bearing rather than stylistic. Granting a
		// claim from Scheduled ADVANCES the status, so a second contender arriving a moment later finds the
		// row in the claimed status -- and answering StatusMismatch there obliges the caller to STOP
		// OFFERING the request. That is wrong in the direction this mechanism exists to prevent: the
		// request IS coming back when the holder's claim lapses, and a caller told otherwise strands it.
		if (verdict.LiveClaim > 0)
		{
			return ErasureClaimResult.AlreadyClaimed();
		}

		return ErasureClaimResult.StatusMismatch();
	}

	/// <summary>Whether the request exists at all, and whether this caller still holds its claim.</summary>
	/// <remarks>
	/// Two counts rather than one, because a single query filtering on both columns cannot tell an
	/// absent request from a superseded claim, and the caller must do opposite things about them.
	/// </remarks>
	private sealed class CompletionPresence
	{
		/// <summary>One when the request exists in this tenant, zero when it does not.</summary>
		public long RequestExists { get; init; }

		/// <summary>One when the request still carries the identity this caller presented.</summary>
		public long ClaimHeld { get; init; }
	}

	/// <summary>The three counts a status write reports.</summary>
	private sealed class StatusWriteVerdict
	{
		public int WrittenCount { get; init; }

		public int TargetCount { get; init; }

		public int ClaimOk { get; init; }

		/// <summary>Maps the counts onto the outcome, testing success first and by name.</summary>
		public ErasureWriteOutcome ToOutcome() =>
			WrittenCount > 0 ? ErasureWriteOutcome.Applied
			: TargetCount == 0 ? ErasureWriteOutcome.NotFound
			: ClaimOk == 0 ? ErasureWriteOutcome.ClaimLost
			: ErasureWriteOutcome.StatusRefused;
	}

	/// <summary>The three counts a claim attempt reports.</summary>
	private sealed class ClaimVerdict
	{
		public int GrantedCount { get; init; }

		public int TargetCount { get; init; }

		public int StatusOk { get; init; }

		public int LiveClaim { get; init; }
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
		var tenantPredicate = _requireTenant ? " AND r.TenantId = @AmbientTenantId AND DATALENGTH(r.TenantId) = DATALENGTH(@AmbientTenantId)" : string.Empty;

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
		var tenantPredicate = _requireTenant ? " AND r.TenantId = @AmbientTenantId AND DATALENGTH(r.TenantId) = DATALENGTH(@AmbientTenantId)" : string.Empty;

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
		var tenantPredicate = _requireTenant ? " AND r.TenantId = @AmbientTenantId AND DATALENGTH(r.TenantId) = DATALENGTH(@AmbientTenantId)" : string.Empty;

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
				  WHERE d.KeyHandle = @KeyHandle AND d.KeyGeneration = @KeyGeneration)";

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
				// exists is a proxy, and the wrong one: a pair on file under any other request would answer yes
				// and let a bad requestId pass as benign.
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
		// NOT EXISTS plus the duplicate-key filter below, so re-asserting a pair already on file is a no-op
		// and the FIRST instant stands. A later assertion must not move a recorded destruction's timestamp.
		var sql = $@"
			INSERT INTO {_options.FullDestroyedKeysTableName} (KeyGeneration, RequestId, KeyHandle, DestroyedAt, RecordedBy)
			SELECT @KeyGeneration, NULL, @KeyHandle, @Now, '{RecordedBy.CallerAssertion}'
			WHERE NOT EXISTS (
				SELECT 1 FROM {_options.FullDestroyedKeysTableName} d
				WHERE d.KeyHandle = @KeyHandle AND d.KeyGeneration = @KeyGeneration)";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			// Not caught beyond the race below, by contract: a caller that believed it recorded a destruction
			// it did not would see the subject's reads fail with no indication why.
			_ = await connection.ExecuteAsync(new CommandDefinition(
				sql,
				new { KeyHandle = keyHandle, KeyGeneration = keyGeneration, Now = DateTimeOffset.UtcNow },
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
	/// <inheritdoc />
	/// <remarks>
	/// TRUE: a committed row is on disk in the compliance schema and outlives the process. The write path
	/// awaits the commit before returning, so a caller that has returned from
	/// <see cref="RecordMintIntentAsync"/> holds a row an erasure will enumerate after any crash.
	/// </remarks>
	public bool IsDurable => true;

	/// <inheritdoc />
	public async Task RecordMintIntentAsync(
		string keyHandle,
		TenantId tenantId,
		string dataSubjectIdHash,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(keyHandle);
		ArgumentNullException.ThrowIfNull(tenantId);
		ArgumentException.ThrowIfNullOrWhiteSpace(dataSubjectIdHash);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// INSERT WHERE NOT EXISTS, so a handle already on file is a no-op and the FIRST instant stands. A
		// LOST RACE IS A SUCCESS by contract: two concurrent first writes for one subject and obligation
		// class both need the row to exist, and it does. The UPDLOCK/HOLDLOCK hint takes a range lock on the
		// probed key so a concurrent inserter cannot slip between the check and the insert; without it the
		// loser raises a duplicate-key error, which is a failure the caller would have to interpret.
		//
		// NOT CAUGHT, by contract, and this is the one place where the rule that cross-cutting
		// infrastructure fails OPEN is inapplicable. Returning normally here tells the caller the row is
		// durable, and the caller then mints a key. Swallowing a failure mints a key nothing will ever
		// enumerate, which is the catastrophic direction; a refused write is loud and recoverable.
		var inserted = await connection.ExecuteAsync(new CommandDefinition(
			// MintedAt is NOT written here. The key does not exist yet and this row must not claim it does.
			$@"INSERT INTO {_options.FullMintedKeysTableName}
					(KeyHandle, TenantId, DataSubjectIdHash, IntentAt, MintedAt)
				SELECT @KeyHandle, @TenantId, @SubjectHash, @Now, NULL
				WHERE NOT EXISTS (
					SELECT 1 FROM {_options.FullMintedKeysTableName} WITH (UPDLOCK, HOLDLOCK)
					WHERE KeyHandle = @KeyHandle)",
			new
			{
				KeyHandle = keyHandle,
				TenantId = tenantId.Value,
				SubjectHash = dataSubjectIdHash,
				Now = DateTimeOffset.UtcNow,
			},
			cancellationToken: cancellationToken,
			commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		if (inserted > 0)
		{
			return;
		}

		// A HANDLE ALREADY ON FILE UNDER A DIFFERENT OWNER OR SUBJECT IS REFUSED, not absorbed. The row is
		// what an erasure enumerates by, so letting a second owner write silently keep the first owner row
		// would make the handle enumerate for the wrong subject -- either erasing a subject who never asked
		// or withholding erasure from one who did. The handle is a digest over (owner, subject), so
		// disagreement here means a derivation collision or a caller passing terms the handle was not
		// derived from, and both are defects to report rather than record.
		var existing = await connection.QuerySingleOrDefaultAsync<MintedKeyOwnerRow>(new CommandDefinition(
			$@"SELECT TenantId, DataSubjectIdHash
				FROM {_options.FullMintedKeysTableName}
				WHERE KeyHandle = @KeyHandle",
			new { KeyHandle = keyHandle },
			cancellationToken: cancellationToken,
			commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false)
			// GONE between the two statements, which means something deleted it. The rows are insert-only
			// and permanent by contract, so this is not a state the framework can produce -- and it must
			// not be read as agreement: returning normally would promise durability for an absent row.
			?? throw new InvalidOperationException(
				$"The mint-ledger row for key handle '{keyHandle}' was not inserted and could not be read "
				+ "back. Mint rows are insert-only and permanent, so this indicates the row was deleted "
				+ "concurrently. The key was NOT minted: minting one whose handle no erasure can enumerate "
				+ "would leave this data subject's personal data readable after a completed erasure.");

		if (!string.Equals(existing.TenantId, tenantId.Value, StringComparison.Ordinal)
			|| !string.Equals(existing.DataSubjectIdHash, dataSubjectIdHash, StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				$"Key handle '{keyHandle}' is already recorded in the mint ledger for a different data "
				+ "subject or owner. A handle names the thing an erasure destroys, so recording it twice "
				+ "under different terms would make it enumerate for the wrong subject. The key was NOT "
				+ "minted.");
		}
	}

	/// <inheritdoc />
	public async Task<MintLedgerSnapshot> ListHandlesAsync(
		TenantId tenantId,
		string dataSubjectIdHash,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(tenantId);
		ArgumentException.ThrowIfNullOrWhiteSpace(dataSubjectIdHash);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// BOTH TERMS ARE PREDICATES, and neither is an ambient value. The owner is the one the erasure own
		// request recorded; the storage partition plays no part here. These two columns carry BIN2 precisely
		// so this comparison is bytewise, matching the service ordinal comparison -- the server default
		// collation is case-INsensitive and would let one owner rows answer for another.
		//
		// NO TOP and NO PAGING: the contract is COMPLETE ENUMERATION OR THROW. A truncated list is
		// indistinguishable from a subject with fewer handles, so the erasure would destroy the page it
		// received and certify. The set is bounded by the number of obligation classes a subject data
		// spans, which is small.
		//
		// NOT CAUGHT, by contract. An enumeration that cannot be completed must not degrade to a short
		// list; the exception leaves the request non-terminal and retryable instead of falsely attested.
		var rows = await connection.QueryAsync<(string KeyHandle, DateTimeOffset? MintedAt)>(new CommandDefinition(
			// THE DATALENGTH TERMS ARE LOAD-BEARING AND ARE NOT DEFENSIVE PADDING. SQL Server follows the
			// ANSI rule that TRAILING BLANKS ARE INSIGNIFICANT in a character comparison, so
			// `TenantId = @TenantId AND DATALENGTH(TenantId) = DATALENGTH(@TenantId)` matches an owner stored with a trailing space. That is a DIFFERENT
			// OWNER: this enumeration would return their handles, and the erasure adds every handle it
			// returns to the destruction set -- one tenant's erasure destroying another tenant's keys,
			// which is the cross-tenant direction this subsystem has already shipped once.
			//
			// A BINARY COLLATION ON THE COLUMN IS NOT SUFFICIENT HERE, which is why this is in the
			// statement rather than left to the DDL. The column is declared BIN2 by this store's own
			// CREATE TABLE, but that clause runs only when the table is ABSENT -- a database provisioned
			// out of band, or by an earlier schema, can present the right column NAMES with a padding
			// comparison, and the startup verification checks names and not collations. Putting the
			// predicate in the statement makes the comparison a property of the QUERY, so no table's
			// declaration can weaken it.
			//
			// The equality terms are kept so the (TenantId, DataSubjectIdHash) index still drives the
			// seek; DATALENGTH only discards the padded false positives the seek admits. Measured: the
			// conformance kit's MintLedger_ShouldMatchTheOwnerOrdinally arm fails on real SQL Server
			// without these two terms and passes with them, while Postgres passes either way -- its
			// equality is not blank-insensitive.
			$@"SELECT KeyHandle, MintedAt
				FROM {_options.FullMintedKeysTableName}
				WHERE TenantId = @TenantId AND DATALENGTH(TenantId) = DATALENGTH(@TenantId)
				  AND DATALENGTH(TenantId) = DATALENGTH(@TenantId)
				  AND DataSubjectIdHash = @SubjectHash
				  AND DATALENGTH(DataSubjectIdHash) = DATALENGTH(@SubjectHash)",
			new { TenantId = tenantId.Value, SubjectHash = dataSubjectIdHash },
			cancellationToken: cancellationToken,
			commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		var materialised = rows.ToList();

		IReadOnlyList<string> minted = [.. materialised.Where(r => r.MintedAt is not null).Select(r => r.KeyHandle)];
		IReadOnlyList<string> pending = [.. materialised.Where(r => r.MintedAt is null).Select(r => r.KeyHandle)];

		return new MintLedgerSnapshot(minted, pending);
	}

	/// <inheritdoc />
	public async Task MarkMintedAsync(string keyHandle, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(keyHandle);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// IS NULL IS PART OF THE PREDICATE, so a confirmation already on file is not moved. The row records
		// when the key came into existence and a retry may not restate that instant. DATALENGTH guards the
		// handle for the reason the enumeration does: SQL Server equality ignores trailing blanks, so a
		// padded handle would otherwise confirm a different row.
		var updated = await connection.ExecuteAsync(new CommandDefinition(
			$@"UPDATE {_options.FullMintedKeysTableName}
				SET MintedAt = @Now
				WHERE KeyHandle = @KeyHandle
				  AND DATALENGTH(KeyHandle) = DATALENGTH(@KeyHandle)
				  AND MintedAt IS NULL",
			new { KeyHandle = keyHandle, Now = DateTimeOffset.UtcNow },
			cancellationToken: cancellationToken,
			commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		if (updated > 0)
		{
			return;
		}

		// ZERO ROWS IS AMBIGUOUS AND THE TWO CAUSES ARE NOT INTERCHANGEABLE: either the row is already
		// confirmed (idempotent success) or there is no row at all (the caller skipped the intent step, and
		// a key may now exist at a handle no erasure enumerates). One cheap read separates them.
		var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
			$@"SELECT COUNT(1) FROM {_options.FullMintedKeysTableName}
				WHERE KeyHandle = @KeyHandle
				  AND DATALENGTH(KeyHandle) = DATALENGTH(@KeyHandle)",
			new { KeyHandle = keyHandle },
			cancellationToken: cancellationToken,
			commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		if (exists == 0)
		{
			throw new InvalidOperationException(
				$"No mint-ledger row exists for key handle '{keyHandle}', so its key creation cannot be "
				+ "confirmed. The row is written before the key is created, always; its absence means the "
				+ "intent step was skipped and a key may exist at a handle no erasure can enumerate.");
		}
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
			+ "WHERE KeyHandle = @KeyHandle AND KeyGeneration = @KeyGeneration";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var found = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
			sql,
			new { KeyHandle = keyHandle, KeyGeneration = keyGeneration },
			cancellationToken: cancellationToken,
			commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		return found > 0;
	}

	/// <inheritdoc />
	public async Task<ErasureWriteOutcome> RecordCompletionAsync(
		Guid requestId,
		int keysDeleted,
		int recordsAffected,
		Guid certificateId,
		string claimIdentity,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(claimIdentity);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var tenant = AmbientScope;
		var tenantPredicate = _requireTenant ? " AND TenantId = @AmbientTenantId AND DATALENGTH(TenantId) = DATALENGTH(@AmbientTenantId)" : string.Empty;

		var sql = $@"
			UPDATE {_options.FullRequestsTableName}
			SET Status = @Status,
				KeysDeleted = @KeysDeleted,
				RecordsAffected = @RecordsAffected,
				CertificateId = @CertificateId,
				CompletedAt = @Now,
				UpdatedAt = @Now
			WHERE RequestId = @RequestId{tenantPredicate}
			  AND Status IN ({(int)ErasureRequestStatus.Scheduled}, {(int)ErasureRequestStatus.InProgress}, {(int)ErasureRequestStatus.AwaitingKeyDestruction})
			  AND ClaimIdentity = @ClaimIdentity
			  AND DATALENGTH(ClaimIdentity) = DATALENGTH(@ClaimIdentity)";

		// THE FENCE, and it compares the identity and ONLY the identity. No expiry term: a lapse does not
		// revoke, so a holder nobody superseded still completes its own run. Were the expiry tested here,
		// an executor that had already destroyed a subject's keys and overrun its lease would be refused
		// the certificate attesting what it had done -- the erasure performed, and nothing able to say so.
		// The DATALENGTH term is the truncation guard, matching the tenant predicate's own idiom.

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
				ClaimIdentity = claimIdentity,
				AmbientTenantId = tenant.TenantId
			}, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		if (affected > 0)
		{
			return ErasureWriteOutcome.Applied;
		}

		// ABSENCE AND SUPERSESSION BOTH YIELD ZERO FROM A SINGLE QUERY THAT FILTERS ON BOTH COLUMNS, and
		// they oblige OPPOSITE things of the caller, so they are asked separately. A superseded claim is
		// an ordinary race the caller stands down from silently. An ABSENT request is the anomaly this
		// member has always thrown for, and its contract still states that. Asking one combined question
		// reported every absent request as a lost claim, so a caller swallowed a defect as a race.
		//
		// The DATALENGTH term stays on the claim half only: SQL Server compares char data ignoring
		// trailing spaces, so without it an identity differing only by padding compares equal.
		var presence = await connection.QuerySingleAsync<CompletionPresence>(new CommandDefinition(
			$@"SELECT
				(SELECT COUNT(*) FROM {_options.FullRequestsTableName}
				 WHERE RequestId = @RequestId{tenantPredicate}) AS RequestExists,
				(SELECT COUNT(*) FROM {_options.FullRequestsTableName}
				 WHERE RequestId = @RequestId{tenantPredicate}
				   AND ClaimIdentity = @ClaimIdentity
				   AND DATALENGTH(ClaimIdentity) = DATALENGTH(@ClaimIdentity)) AS ClaimHeld",
			new { RequestId = requestId, ClaimIdentity = claimIdentity, AmbientTenantId = tenant.TenantId },
			cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		if (presence.RequestExists > 0 && presence.ClaimHeld == 0)
		{
			return ErasureWriteOutcome.ClaimLost;
		}

		throw new KeyNotFoundException(
			$"No erasure request with id '{requestId}' is in a state from which a completion can be "
			+ "recorded: it does not exist, it belongs to another tenant, or its status changed while "
			+ "the run was executing. A legal hold or a cancellation recorded mid-run takes the request "
			+ "out of the run deliberately, and a completion must not overwrite it.");
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
		var tenantPredicate = _requireTenant ? " AND TenantId = @AmbientTenantId AND DATALENGTH(TenantId) = DATALENGTH(@AmbientTenantId)" : string.Empty;

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
				   RequestId, DataSubjectIdHash, IdType, TenantId, TenantPartition, Scope, LegalBasisV2,
				   ExternalReference, RequestedBy, RequestedAt, ScheduledExecutionAt,
				   ExecutedAt, CompletedAt, CancelledAt, CancellationReason, CancelledBy,
				   Status, KeysDeleted, RecordsAffected, CertificateId, ErrorMessage, UpdatedAt
			FROM {_options.FullRequestsTableName}
			WHERE (Status = {(int)ErasureRequestStatus.Scheduled} AND ScheduledExecutionAt <= @Now)
			   OR (Status = {(int)ErasureRequestStatus.InProgress} AND ClaimExpiresAt <= SYSUTCDATETIME())
			ORDER BY ScheduledExecutionAt";

		// THE SECOND DISJUNCT IS WHAT MAKES AN INTERRUPTED ERASURE COME BACK. A request whose executor
		// died stays in the claimed status with nothing to advance it, and this query is the only thing
		// that offers work -- so without the lapse term it is excluded forever, correctly and silently,
		// and the subject's data is never erased. Compared against SYSUTCDATETIME() AT THE SERVER, never
		// against the @Now the first disjunct binds: a lapse decided against a caller's clock is a
		// comparison between two machines, which is the one comparison that may never decide anything.

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<ErasureRequestRow>(
			new CommandDefinition(sql, new { MaxResults = maxResults, Now = DateTimeOffset.UtcNow },
				cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		return rows.Select(r => r.ToStatus()).ToList();
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<ErasureStatus>> GetAwaitingKeyDestructionRequestsAsync(
		int pageNumber, int pageSize, CancellationToken cancellationToken)
	{
		// NOT DiscoverByStatusAsync, and the divergence is deliberate: this status is the only one a
		// confirmation pass CLAIMS from, so a live claim means another confirmer already holds the
		// request. Offering it anyway makes the second confirmer take a claim it cannot get, be refused
		// at its first write, and log a defect on every poll for as long as the first one holds -- a
		// defect report that says nothing is wrong. The hold-deferred query keeps the shared helper
		// because nothing claims a deferred request.
		// THE PAGING GUARDS ARE THE SHARED HELPER'S, COPIED RATHER THAN PARAPHRASED. My first
		// hand-written version dropped the widened offset, so paging at int.MaxValue overflowed to a
		// wrapped negative, which LINQ and both engines read as "skip nothing" -- a page far past the
		// end returned the FIRST page instead of nothing. A tenant-scope arm caught it.
		ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
		ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 1000);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// ESTATE-WIDE AND DELIBERATELY UNSCOPED, like the other background discovery surfaces: this runs
		// from a host that has no tenant, so a tenant predicate here returns nothing and the whole
		// confirmation pass silently stops. The claim exclusion below is the only new term.
		var sql = $@"
			SELECT RequestId, DataSubjectIdHash, IdType, TenantId, TenantPartition, Scope, LegalBasisV2,
				   ExternalReference, RequestedBy, RequestedAt, ScheduledExecutionAt,
				   ExecutedAt, CompletedAt, CancelledAt, CancellationReason, CancelledBy,
				   Status, KeysDeleted, RecordsAffected, CertificateId, ErrorMessage, UpdatedAt
			FROM {_options.FullRequestsTableName}
			WHERE Status = {(int)ErasureRequestStatus.AwaitingKeyDestruction}
			  AND (ClaimIdentity IS NULL OR ClaimExpiresAt <= SYSUTCDATETIME())
			ORDER BY RequestedAt, RequestId
			OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<ErasureRequestRow>(
			new CommandDefinition(sql,
				new { PageSize = pageSize, Offset = ((long)pageNumber - 1) * pageSize },
				cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		return rows.Select(r => r.ToStatus()).ToList();
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<ErasureStatus>> GetHoldDeferredRequestsAsync(
		int pageNumber, int pageSize, CancellationToken cancellationToken) =>
		DiscoverByStatusAsync(ErasureRequestStatus.BlockedByLegalHold, pageNumber, pageSize, cancellationToken);

	/// <summary>
	/// The estate-wide discovery statement both background members are served by.
	/// </summary>
	/// <remarks>
	/// Shared deliberately: the two members differ only in the status they look for, and what drifts
	/// between hand-copied discovery statements is the paging, the ordering and the tenancy rather than
	/// the constant. In particular this statement carries NO ambient-tenant term, which the nearest
	/// sibling statements in this file DO -- a background worker runs under no tenant scope, so an
	/// ambient term here would return an empty set under multi-tenancy and the work would silently
	/// never be done. The unique tie-breaker is what makes the page boundary stable when requested
	/// times tie.
	/// </remarks>
	private async Task<IReadOnlyList<ErasureStatus>> DiscoverByStatusAsync(
		ErasureRequestStatus status, int pageNumber, int pageSize, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
		ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 1000);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
		var sql = $@"
			SELECT RequestId, DataSubjectIdHash, IdType, TenantId, TenantPartition, Scope, LegalBasisV2,
				   ExternalReference, RequestedBy, RequestedAt, ScheduledExecutionAt,
				   ExecutedAt, CompletedAt, CancelledAt, CancellationReason, CancelledBy,
				   Status, KeysDeleted, RecordsAffected, CertificateId, ErrorMessage, UpdatedAt
			FROM {_options.FullRequestsTableName}
			WHERE Status = @Status
			ORDER BY RequestedAt, RequestId
			OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		var rows = await connection.QueryAsync<ErasureRequestRow>(new CommandDefinition(sql,
			new { Status = (int)status, PageSize = pageSize, Offset = ((long)pageNumber - 1) * pageSize },
			cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);
		return rows.Select(row => row.ToStatus()).ToList();
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
			whereClauses.Add("TenantId = @AmbientTenantId AND DATALENGTH(TenantId) = DATALENGTH(@AmbientTenantId)");
			parameters.Add("AmbientTenantId", tenant.TenantId);
		}

		if (status.HasValue)
		{
			whereClauses.Add("Status = @Status");
			parameters.Add("Status", (int)status.Value);
		}

		if (!string.IsNullOrEmpty(tenantId))
		{
			whereClauses.Add("TenantId = @TenantId AND DATALENGTH(TenantId) = DATALENGTH(@TenantId)");
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
			SELECT RequestId, DataSubjectIdHash, IdType, TenantId, TenantPartition, Scope, LegalBasisV2,
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
				  WHERE r.RequestId = {_options.FullCertificatesTableName}.RequestId AND r.TenantId = @AmbientTenantId AND DATALENGTH(r.TenantId) = DATALENGTH(@AmbientTenantId))"
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
				  WHERE r.RequestId = {_options.FullCertificatesTableName}.RequestId AND r.TenantId = @AmbientTenantId AND DATALENGTH(r.TenantId) = DATALENGTH(@AmbientTenantId))"
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

		if (serviceType == typeof(IErasureQueryStore) || serviceType == typeof(IErasureBackgroundQueryStore))
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

	/// <summary>
	/// The one row this database's token fingerprint lives under. A constant, so the table holds at most
	/// one row by primary key rather than by convention.
	/// </summary>
	private const string TokenFingerprintRowId = "data-subject-token-function";

	/// <summary>SQL Server primary-key violation.</summary>
	private const int PrimaryKeyViolation = 2627;

	/// <summary>SQL Server unique-index violation.</summary>
	private const int UniqueIndexViolation = 2601;

	/// <inheritdoc />
	public async Task<string> ClaimTokenFingerprintAsync(string fingerprint, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		return await ClaimTokenFingerprintCoreAsync(fingerprint, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Records the fingerprint if none is recorded and returns what is recorded afterwards, without
	/// entering the initialisation funnel.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Separate from the public member because initialisation itself calls this while HOLDING the
	/// initialisation lock. A core that re-entered the funnel would deadlock the first operation of every
	/// process.
	/// </para>
	/// <para>
	/// <b>The duplicate is swallowed BY NUMBER, and only those two numbers.</b> Several processes may start
	/// together against a database with no fingerprint; exactly one insert wins and the rest collide. A lost
	/// race is a SUCCESS, so the collision must not surface — but a blanket <c>catch (SqlException)</c>
	/// would make the read-back the only evidence and turn a transient fault, a timeout or a permissions
	/// error into a silent pass. Only 2627 and 2601 mean "someone else already recorded it".
	/// </para>
	/// <para>
	/// <b>It runs on its own connection with no ambient transaction</b>, which is what makes the swallow
	/// effective: under <c>XACT_ABORT ON</c> inside an enclosing transaction the batch would be doomed and
	/// the subsequent read would fail too, so this must not be moved inside one.
	/// </para>
	/// <para>
	/// <b>No tenant term, deliberately, and not an oversight.</b> The token function is a property of the
	/// process, not of a tenant; the table has no tenant column at all, so the term is inexpressible rather
	/// than merely omitted.
	/// </para>
	/// </remarks>
	private async Task<string> ClaimTokenFingerprintCoreAsync(
		string fingerprint,
		CancellationToken cancellationToken)
	{
		// Public claims also reach this core outside initialization. Safety metadata is durable
		// independently of business work and must never enlist in the caller's transaction.
		using var claimScope = new TransactionScope(
			TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);

		var insertSql = $@"
			INSERT INTO {_options.FullTokenFingerprintTableName} ([Id], [Fingerprint], [RecordedAt])
			VALUES (@Id, @Fingerprint, @RecordedAt)";

		var selectSql = $@"
			SELECT [Fingerprint]
			FROM {_options.FullTokenFingerprintTableName}
			WHERE [Id] = @Id";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			_ = await connection.ExecuteAsync(
					new CommandDefinition(
						insertSql,
						new { Id = TokenFingerprintRowId, Fingerprint = fingerprint, RecordedAt = DateTimeOffset.UtcNow },
						cancellationToken: cancellationToken,
						commandTimeout: _options.CommandTimeoutSeconds))
				.ConfigureAwait(false);
		}
		catch (SqlException ex) when (ex.Number is PrimaryKeyViolation or UniqueIndexViolation)
		{
			// Another process recorded it first. That is the specified no-op, not a failure.
		}

		var recorded = await connection.ExecuteScalarAsync<string?>(
				new CommandDefinition(
					selectSql,
					new { Id = TokenFingerprintRowId },
					cancellationToken: cancellationToken,
					commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);

		// Reported rather than coalesced to the caller's own argument: returning the argument would make
		// every mismatch compare equal and the whole check vacuous.
		return recorded
			?? throw new InvalidOperationException(
				$"The data-subject token fingerprint could not be read back from {_options.FullTokenFingerprintTableName} " +
				"immediately after it was claimed. The record is write-once and must never be deleted; a " +
				"deployment whose fingerprint row can disappear cannot detect a changed hashing key.");
	}

	/// <summary>
	/// Refuses to finish initialising when this database's records were produced by a different
	/// data-subject token function than this process computes.
	/// </summary>
	/// <remarks>
	/// Runs as the last step of initialisation, inside the initialisation lock and after the schema is
	/// known to exist, so every one of this store's operations happens-after it.
	/// </remarks>
	private async Task VerifyTokenFingerprintAsync(CancellationToken cancellationToken)
	{
		var mine = DataSubjectPepperFingerprint.Derive(_dataSubjectHasher);
		var recorded = await ClaimTokenFingerprintCoreAsync(mine, cancellationToken).ConfigureAwait(false);

		if (string.Equals(recorded, mine, StringComparison.Ordinal))
		{
			return;
		}

		throw new InvalidOperationException(
			"The data-subject hashing key (or the registered hasher) has CHANGED since this database was " +
			"first used, so every pseudonymisation token it is keyed by would now be computed differently. " +
			"Startup is refused because continuing is silent and unrecoverable: an erasure would enumerate " +
			"no key handles and still sign a completion certificate, a legal-hold lookup would find nothing " +
			"so an erasure would proceed over data the law requires you to keep, and personal data already " +
			"encrypted would stop being decryptable. Restore the original " +
			$"{nameof(DataSubjectHashingOptions)}.{nameof(DataSubjectHashingOptions.Pepper)} and the original " +
			$"{nameof(IDataSubjectHasher)} registration, or migrate every token-keyed record to the new value " +
			"before starting. The hashing key is a permanent derivation parameter for the life of the data it " +
			"protects; it is not a credential to rotate.");
	}

	private async Task InitializeCoreAsync(CancellationToken cancellationToken)
	{
		// Initialization is cached, so its schema and fingerprint must survive caller rollback.
		// Ordinary data operations run after this scope restores the caller's ambient transaction.
		using var initializationScope = new TransactionScope(
			TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);

		if (_options.AutoCreateSchema)
		{
			await CreateSchemaIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
		}

		// Existing tables survive conditional creation unchanged. Verify their columns on both paths
		// so an older schema fails before any request is accepted.
		await VerifySchemaExistsAsync(cancellationToken).ConfigureAwait(false);

		// LAST, and after the schema is known to exist, because it writes a row. Placed here rather than at
		// each operation because every operation already awaits this funnel: the 21 call sites are what make
		// "every token-bound statement happens-after the check" true by construction instead of by review.
		await VerifyTokenFingerprintAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Verifies that the backing schema is one this store's statements can rely on, delegating to the
	/// type that owns that question.
	/// </summary>
	/// <remarks>
	/// Separate from this class on purpose. Compatibility is a different concern from data access: it
	/// reads the catalogue rather than the rows, it is answered once at startup rather than per call, and
	/// it is the half the provisioning scripts also answer -- so keeping the two definitions of
	/// "compatible" in one place is what stops them drifting apart.
	/// </remarks>
	/// <param name="cancellationToken">A token that cancels the catalogue reads.</param>
	/// <returns>A task that completes when the schema is verified.</returns>
	/// <exception cref="ErasureStoreNotProvisionedException">
	/// A required table is absent, or is present in a shape this store's statements cannot rely on.
	/// </exception>
	private Task VerifySchemaExistsAsync(CancellationToken cancellationToken) =>
		new SqlServerErasureSchemaVerifier(_options).VerifyAsync(cancellationToken);

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
	/// Indicates whether a SQL Server error is a primary-key or unique-index violation (2627 / 2601).
	/// </summary>
	/// <remarks>
	/// Used as an exception filter so only this one condition is translated. A broad catch would report
	/// an unrelated failure - a dropped connection, a timeout, a check constraint - as a duplicate, which
	/// is worse than not translating at all: the caller would be told the row exists when it does not.
	/// </remarks>
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
					TenantPartition NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL,
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
					-- CUSTODY, on its own axis. The Status column answers what HAPPENED to the erasure;
					-- these answer whether a process is currently responsible for it, which it could not
					-- express. Both nullable with no default, because an unclaimed row is the honest and
					-- correct reading of every row written before custody was tracked. Binary collation
					-- on the identity, matching this schema's tenant columns and for the same reason: the
					-- framework compares it ordinally, and a more permissive database collation would
					-- admit a write the fence was supposed to refuse. The expiry is written and compared
					-- by the SERVER. Added here AND in the packaged migration script, because this
					-- statement is a no-op against a table that already exists -- which is exactly why
					-- the schema verifier names both columns.
					ClaimIdentity NVARCHAR(64) COLLATE Latin1_General_BIN2 NULL,
					ClaimExpiresAt DATETIMEOFFSET NULL,
					CONSTRAINT CK_{_options.RequestsTableName}_ClaimPairing CHECK (
						(ClaimIdentity IS NULL AND ClaimExpiresAt IS NULL)
						OR (ClaimIdentity IS NOT NULL AND ClaimExpiresAt IS NOT NULL)
					),
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
		// IS the destruction statement -- and the KeyHandle joins it, which makes the assumption
		// unnecessary rather than documented. Keying on the handle ALONE would be wrong for the reason
		// the old paragraph feared: it would drop a second destruction at one handle. The pair admits
		// many generations per handle and refuses two rows for one pair.
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
					-- NULLABLE: a destruction asserted by the CONSUMER has no erasure request to name. The
					-- HANDLE is not optional -- it is half the primary key, and a caller asserting a
					-- destruction knows what it destroyed.
					RequestId UNIQUEIDENTIFIER NULL,
					KeyHandle NVARCHAR(256) COLLATE Latin1_General_BIN2 NOT NULL,
					DestroyedAt DATETIMEOFFSET NOT NULL,
					-- WHO asserted the destruction, stated explicitly rather than inferred from the nulls
					-- above. Deriving a fact from a missing value is the reasoning this table exists to replace.
					RecordedBy NVARCHAR(32) NOT NULL,
					-- THE PAIR, not the generation alone. The generation, because it is minted once and never
					-- reused, so a row's existence IS the destruction statement. The handle, because keying on
					-- the generation alone rests on an assumption nothing enforces -- that one generation
					-- identifies material uniquely across every handle -- which a consumer-supplied provider
					-- deriving its generations would break, giving two tenants' distinct keys one row.
					CONSTRAINT PK_{_options.DestroyedKeysTableName} PRIMARY KEY (KeyHandle, KeyGeneration)
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

		// THE MINT LEDGER: the handles the write path has intended to mint for a data subject, written
		// AHEAD of each mint. It is what makes an erasure's destruction set COMPLETE rather than DERIVED --
		// the declared retentions and the data inventory each answer which handles SHOULD exist, and a
		// handle neither implies is a key nothing destroys.
		var createMintedKeysTableSql = $@"
			IF NOT EXISTS (SELECT 1 FROM sys.tables t
				JOIN sys.schemas s ON t.schema_id = s.schema_id
				WHERE s.name = '{_options.SchemaName}' AND t.name = '{_options.MintedKeysTableName}')
			BEGIN
				CREATE TABLE {_options.FullMintedKeysTableName} (
					-- THE HANDLE ALONE IS THE KEY, the opposite choice from the destruction ledger, for a
					-- reason rather than by inconsistency. Existence is a per-HANDLE fact here; a
					-- destruction is a per-GENERATION one. A handle minted, destroyed and minted again has
					-- one row throughout and that row stays true, so the row is immutable.
					--
					-- BIN2 is not stylistic. An erasure enumerates by (TenantId, DataSubjectIdHash) and the
					-- service compares handles ordinally; the server default collation is case-INsensitive,
					-- which would let one owner rows answer for another -- the cross-tenant direction this
					-- subsystem has already shipped once.
					KeyHandle NVARCHAR(256) COLLATE Latin1_General_BIN2 NOT NULL,
					-- THE OWNER the data was encrypted under, stored verbatim, never an ambient value and
					-- never the storage partition. An erasure is filed by one actor and executed later by
					-- another, so the enumeration must join on the term the WRITE recorded.
					TenantId NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL,
					DataSubjectIdHash NVARCHAR(128) COLLATE Latin1_General_BIN2 NOT NULL,
					-- WHEN THE ROW WAS WRITTEN, which is BEFORE the key was created.
					IntentAt DATETIMEOFFSET NOT NULL,
					-- WHEN THE KEY WAS CONFIRMED TO EXIST, and NULL until it was. The null is load-bearing
					-- rather than a default: between the row and the key a write is invisible to both
					-- observables an erasure has, so that state is indistinguishable from a writer that
					-- crashed and is safely ignorable. This column separates them, and an erasure that
					-- finds a null must report a concurrent write rather than certify.
					MintedAt DATETIMEOFFSET NULL,
					CONSTRAINT PK_{_options.MintedKeysTableName} PRIMARY KEY (KeyHandle)
				)
				-- THE ENUMERATION INDEX, and the erasure cannot work without it: a handle is an opaque
				-- digest, so (TenantId, DataSubjectIdHash) is the only key a subject handles can be
				-- found by.
				CREATE INDEX IX_{_options.MintedKeysTableName}_Subject
					ON {_options.FullMintedKeysTableName} (TenantId, DataSubjectIdHash)
			END";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// NO TENANT COLUMN, deliberately. The token function belongs to the process, not to a tenant: a
		// per-tenant fingerprint would record a rotated hashing key as correct for a tenant onboarded
		// after the rotation while every earlier tenant's records were already orphaned, and the check
		// would then pass forever on exactly the deployment where the damage is worst. Omitting the column
		// makes a tenant predicate inexpressible rather than merely unused.
		//
		// This must stay equivalent in shape to the shipped provisioning script: the two are separate
		// provisioning surfaces for the same table, and a database created by one is verified against the
		// same contract as a database created by the other.
		var createTokenFingerprintTableSql = $@"
			IF OBJECT_ID(N'{_options.FullTokenFingerprintTableName}') IS NULL
			CREATE TABLE {_options.FullTokenFingerprintTableName} (
				[Id] NVARCHAR(128) COLLATE Latin1_General_BIN2 NOT NULL,
				[Fingerprint] NVARCHAR(128) COLLATE Latin1_General_BIN2 NOT NULL,
				[RecordedAt] DATETIMEOFFSET NOT NULL CONSTRAINT [DF_{_options.TokenFingerprintTableName}_RecordedAt] DEFAULT SYSDATETIMEOFFSET(),
				CONSTRAINT [PK_{_options.TokenFingerprintTableName}] PRIMARY KEY CLUSTERED ([Id]),
				CONSTRAINT [CK_{_options.TokenFingerprintTableName}_NotBlank] CHECK (LEN(LTRIM(RTRIM([Fingerprint]))) > 0)
			)";

		// WRITE-ONCE, ENFORCED AT THE DATABASE. The claim is an insert-if-absent followed by a read, which
		// is sound only while a recorded fingerprint can never change: otherwise a process with the WRONG
		// key can overwrite the value and then match it, while the process with the RIGHT key refuses.
		var createTokenFingerprintGuardSql = $@"
			IF OBJECT_ID(N'[{_options.SchemaName}].[TR_{_options.TokenFingerprintTableName}_WriteOnce]') IS NOT NULL
				DROP TRIGGER [{_options.SchemaName}].[TR_{_options.TokenFingerprintTableName}_WriteOnce];
			EXEC sys.sp_executesql N'
			CREATE TRIGGER [{_options.SchemaName}].[TR_{_options.TokenFingerprintTableName}_WriteOnce]
			ON {_options.FullTokenFingerprintTableName}
			AFTER UPDATE, DELETE
			AS
			BEGIN
				SET NOCOUNT ON;
				IF NOT EXISTS (SELECT 1 FROM deleted) RETURN;
				THROW 51004, ''The recorded data-subject token fingerprint is write-once and must never change or be removed. Changing it would let a process with the wrong hashing key proceed while a process with the right one is refused.'', 1;
			END'";

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
		_ = await connection.ExecuteAsync(new CommandDefinition(createMintedKeysTableSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);
		// ONE ROW PER DECLARED PAIR, insert-if-absent, NEVER updated on the advance path. A set-valued row
		// would make advancing a read-modify-write and therefore a lost update: two processes starting
		// together each read the set, each write their own union, and the first one's additions vanish.
		// Per-pair rows make the union form by construction.
		//
		// NO WRITE-ONCE TRIGGER HERE, and that is the difference from the fingerprint table above rather
		// than an omission. A declaration set legitimately GROWS, and a withdrawal must be able to
		// tombstone a row in place -- the tombstone OCCUPIES the key, which is what stops a later advance
		// resurrecting a withdrawn declaration.
		var createRetentionDeclarationsTableSql = $@"
			IF OBJECT_ID(N'{_options.FullRetentionDeclarationsTableName}') IS NULL
			CREATE TABLE {_options.FullRetentionDeclarationsTableName} (
				[TenantId] NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL,
				[AggregateType] NVARCHAR(512) COLLATE Latin1_General_BIN2 NOT NULL,
				[RecordedAt] DATETIMEOFFSET NOT NULL CONSTRAINT [DF_{_options.RetentionDeclarationsTableName}_RecordedAt] DEFAULT SYSDATETIMEOFFSET(),
				[WithdrawnAt] DATETIMEOFFSET NULL,
				[WithdrawnBy] NVARCHAR(256) NULL,
				[WithdrawalReason] NVARCHAR(1000) NULL,
				CONSTRAINT [PK_{_options.RetentionDeclarationsTableName}] PRIMARY KEY CLUSTERED ([TenantId], [AggregateType]),
				CONSTRAINT [CK_{_options.RetentionDeclarationsTableName}_TypeNotBlank]
					CHECK (LEN(LTRIM(RTRIM([AggregateType]))) > 0),
				CONSTRAINT [CK_{_options.RetentionDeclarationsTableName}_WithdrawalComplete]
					CHECK (([WithdrawnAt] IS NULL AND [WithdrawnBy] IS NULL AND [WithdrawalReason] IS NULL)
						OR ([WithdrawnAt] IS NOT NULL AND [WithdrawnBy] IS NOT NULL AND [WithdrawalReason] IS NOT NULL))
			)";

		// The read is by tenant, which the tenant's leading position in the clustered key already serves.
		// Declared anyway because the verifier asks which columns a read locates rows by, and a provider
		// that reordered the key would otherwise pass while every read scanned.
		var createRetentionDeclarationsIndexesSql = $@"
			IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_{_options.RetentionDeclarationsTableName}_Tenant')
			CREATE INDEX [IX_{_options.RetentionDeclarationsTableName}_Tenant]
				ON {_options.FullRetentionDeclarationsTableName} ([TenantId])";

		_ = await connection.ExecuteAsync(new CommandDefinition(createTokenFingerprintTableSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);
		// Replace the guard atomically, as the shipped provisioning script does. If CREATE fails after
		// DROP, rollback must restore the old guard rather than leaving an existing ledger writable.
		await using (var guardTransaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
		{
			_ = await connection.ExecuteAsync(new CommandDefinition(
				createTokenFingerprintGuardSql,
				transaction: guardTransaction,
				cancellationToken: cancellationToken,
				commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);
			await guardTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
		}

		_ = await connection.ExecuteAsync(new CommandDefinition(createRetentionDeclarationsTableSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);
		_ = await connection.ExecuteAsync(new CommandDefinition(createRetentionDeclarationsIndexesSql, cancellationToken: cancellationToken, commandTimeout: _options.CommandTimeoutSeconds))
			.ConfigureAwait(false);

		LogSchemaEnsured();
	}

	/// <summary>
	/// The owner terms of a mint-ledger row, read back to decide whether a conflicting insert agrees.
	/// </summary>
	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Dapper materializes this type.")]
	private sealed class MintedKeyOwnerRow
	{
		public string TenantId { get; init; } = string.Empty;

		public string DataSubjectIdHash { get; init; } = string.Empty;
	}

	// Internal row classes for Dapper mapping
	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Dapper materializes this type.")]
	private sealed class ErasureRequestRow
	{

		/// <summary>The stored partition term, read back from its own column.</summary>
		/// <remarks>
		/// Nullable and checked in <c>ToStatus</c> rather than defaulted. A missing column would otherwise
		/// surface as the untenanted partition, which is a WRONG ANSWER that looks like a valid one -- the
		/// same silence this pair of fields exists to remove.
		/// </remarks>
		public string? TenantPartition { get; init; }
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
			TenantPartition = TenantPartition is { Length: > 0 } storedPartition
				? KeyedTenantPartition.FromStoredValue(storedPartition)
				: throw new InvalidOperationException(
					"The erasure request row carries no TenantPartition. The column exists in the shipped "
					+ "schema and every query selects it, so an absent value means this row predates it or "
					+ "the query was narrowed. Refusing rather than assuming a partition: the wrong answer "
					+ "here either destroys a legally retained key or withholds an erasure."),
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
