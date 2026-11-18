// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Excalibur.Dispatch;

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// In-memory implementation of <see cref="IErasureStore"/> for development and testing.
/// </summary>
/// <remarks>
/// This implementation stores all data in memory and is NOT suitable for production use.
/// Data is lost when the application restarts.
/// </remarks>
internal sealed class InMemoryErasureStore
	: IErasureStore, IErasureCertificateStore, IErasureQueryStore, IErasureBackgroundQueryStore, IKeyDestructionLedger, IKeyMintLedger
{
	private readonly ConcurrentDictionary<Guid, ErasureRequestData> _requests = new();

	/// <summary>
	/// The mint ledger: every handle this deployment has recorded an intent to mint, keyed by the handle.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Keyed by the HANDLE ALONE, and carrying no generation, because existence is a per-handle fact where a
	/// destruction is a per-generation one. A handle minted, destroyed and minted again has one entry
	/// throughout, and that entry stays true — so there is nothing to update and the entry is immutable once
	/// written.
	/// </para>
	/// <para>
	/// The owner and the subject token are carried as VALUES rather than being part of the key, because the
	/// handle is an opaque digest: nothing can be recovered from it, so an erasure that must enumerate one
	/// subject's handles has no other key to use. Ordinal throughout, matching the binary collation the SQL
	/// schemas declare, so this store answers exactly as they do.
	/// </para>
	/// </remarks>
	private readonly ConcurrentDictionary<string, MintedHandleRecord> _mintedHandles =
		new(StringComparer.Ordinal);

	/// <summary>
	/// The longest handle the shipped derivation produces: a 64-character digest, optionally composed with a
	/// separator and a 16-character obligation-class discriminator.
	/// </summary>
	/// <remarks>
	/// Enforced rather than assumed, and refused rather than truncated: a truncated handle is a row that
	/// matches no envelope, which is an omission from the set an erasure destroys from. The SQL schemas declare
	/// the same bound, so this store refuses exactly what they refuse.
	/// </remarks>
	private const int MaxKeyHandleLength = 81;

	/// <summary>
	/// The destruction ledger, keyed on the GENERATION alone and nothing else.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Store-level rather than per-request, because the read predicate is handed a generation and nothing else:
	/// a reader holds an envelope, and an envelope names a generation, not the erasure request that destroyed
	/// it. Keying this on the request or the handle would make the predicate unanswerable from what a reader
	/// actually has.
	/// </para>
	/// <para>
	/// A generation is minted once and never reused, so one generation is one destruction. An entry's existence
	/// IS the statement that it was destroyed — there is no flag to read and no clause to omit. Ordinal, because
	/// a generation identifier is an opaque backend token and never text to be compared culturally; a
	/// case-folding comparison would let one destroyed generation answer for a different, live one.
	/// </para>
	/// </remarks>
	// Keyed on the PAIR, matching the SQL stores' composite primary key. The generation alone would rest on
	// a generation identifying material uniquely across every handle, which no provider is obliged to satisfy
	// -- see the ledger contract. Ordinal and case-sensitive, matching the binary collation the SQL schemas
	// declare, so this store answers exactly as they do.
	// No explicit comparer: DestroyedKey is a record struct over two strings, and the generated equality
	// compares each with EqualityComparer<string>.Default, which is ordinal. The previous StringComparer
	// argument applied to a bare-string key and has nowhere to go now; the case-sensitivity it bought is a
	// property of the language here rather than of this line.
	private readonly ConcurrentDictionary<DestroyedKey, DestroyedGenerationRecord> _destroyedGenerations =
		new();

	private readonly ConcurrentDictionary<Guid, ErasureCertificate> _certificates = new();
	private readonly ConcurrentDictionary<Guid, Guid> _requestToCertificate = new();
	private readonly IDataSubjectHasher _dataSubjectHasher;
	private readonly ITenantContext _tenantContext;
	/// <summary>
	/// Gets the tenant scope this store runs under, resolved in one place so every statement it builds binds
	/// the same term. When the deployment is not multi-tenant the store
	/// binds the reserved untenanted partition. That fallback is stated here and nowhere else: a conversion
	/// cannot make it on the store's behalf without inventing a tenant decision the host never made.
	/// </summary>
	private TenantScope CurrentTenantScope =>
		TenantScope.FromContext(_tenantContext);

	private readonly bool _requireTenant;

	/// <summary>
	/// Gets the tenant scope applied to every tenant-facing operation, for both the write and the match.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the single place the tenant term is derived. Every tenant-facing operation in this class reads
	/// it; none compares a tenant value by hand. That is what makes the leak inexpressible: the defect was
	/// that each read <em>branched on a caller-supplied nullable</em>, so a caller who passed nothing got no
	/// filter at all and a caller who passed another tenant's identifier got that tenant's rows. With the term
	/// derived here instead of from the argument, there is no per-call-site opportunity to omit it, and a
	/// caller-supplied identifier can only ever be <em>added</em> to this one — narrowing the result, never
	/// widening it.
	/// </para>
	/// <para>
	/// Deployment mode decides the shape, and it is read from the store's own configuration rather than
	/// inferred from a missing tenant term. A deployment that has not opted into multi-tenancy applies no
	/// filter, and rows keep whatever tenant value the caller supplied — byte-identical to the single-tenant
	/// behaviour, so no stored row becomes unreachable. A
	/// multi-tenant deployment resolves a scoped term that rides every tenant-facing path. Mode is "did the
	/// consumer opt in", read from <see cref="TenantContextOptions.RequireTenant"/>, and deliberately not "is
	/// an <see cref="ITenantContext"/> present" — the framework always registers a single-tenant default, so
	/// presence would make every deployment look multi-tenant.
	/// </para>
	/// <para>
	/// Multi-tenancy active with no resolved tenant fails closed: it throws rather than reaching an unfiltered
	/// read. A missing context is the same failure and is stated as such, because degrading it to an
	/// unfiltered read is the exact cross-tenant read this property exists to remove.
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
	/// Initializes a new instance of the <see cref="InMemoryErasureStore"/> class.
	/// </summary>
	/// <param name="dataSubjectHasher">The keyed hasher used to pseudonymize data-subject identifiers.</param>
	/// <param name="tenantContext">
	/// Ambient tenant context. Under multi-tenancy every tenant-facing operation matches on the resolved
	/// tenant, and the write path stamps it rather than the value on the incoming request, so one tenant
	/// cannot file a request into another tenant's partition. The estate-wide background surfaces
	/// (<c>GetScheduledRequestsAsync</c>, <c>CleanupExpiredCertificatesAsync</c>) are deliberately unscoped
	/// and documented as such at their call sites. It is required: the deployment mode is selected by
	/// <paramref name="tenantContextOptions"/>, not by whether a context was supplied.
	/// </param>
	/// <param name="tenantContextOptions">
	/// <param name="timeProvider">
	/// The clock a claim lapse is decided against. Injected rather than read from the ambient clock so
	/// that no caller can supply the instant -- which is the obligation the SQL providers meet with the
	/// database's own clock inside the claiming statement.
	/// </param>
	/// The tenant-context options. Its <see cref="TenantContextOptions.RequireTenant"/> (set by
	/// <c>AddMultiTenancy()</c>) selects the deployment mode.
	/// </param>
	public InMemoryErasureStore(
		IDataSubjectHasher dataSubjectHasher,
		ITenantContext tenantContext,
		IOptions<TenantContextOptions> tenantContextOptions,
		TimeProvider timeProvider)
	{
		_dataSubjectHasher = dataSubjectHasher ?? throw new ArgumentNullException(nameof(dataSubjectHasher));
		_tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
		ArgumentNullException.ThrowIfNull(tenantContextOptions);
		_requireTenant = tenantContextOptions.Value.RequireTenant;

		// The instant a claim lapse is decided against must come from a clock NO CALLER CAN SUPPLY. The
		// SQL providers satisfy that with the database's own clock inside the claiming statement; this
		// store satisfies it with an injected TimeProvider. A caller-computed instant would make the
		// lapse a comparison between two machines' clocks, which is the one comparison that may never
		// decide anything: a fast clock hands its holder a claim that has already expired, and a slow one
		// holds a claim nobody else can take.
		_timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
	}

	private readonly TimeProvider _timeProvider;

	/// <summary>
	/// Gets the count of requests in the store.
	/// </summary>
	public int RequestCount => _requests.Count;

	/// <summary>
	/// Gets the count of certificates in the store.
	/// </summary>
	public int CertificateCount => _certificates.Count;

	/// <inheritdoc />
	public Task SaveRequestAsync(
		ErasureRequest request,
		DateTimeOffset scheduledExecutionTime,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		var tenant = AmbientScope;
		var owner = ErasureRequestOwner.Resolve(_requireTenant, tenant, request, _tenantContext);

		// The ambient term is authoritative on the write. Stamping the request's own TenantId would let a
		// caller file a request into another tenant's partition — and, because every scoped read matches on
		// the ambient term, that row would then be readable only by the tenant it was planted on.

		// THE PARTITION IS THE SECOND FACT, decided HERE and persisted, from whether multi-tenancy is
		// configured rather than from the shape of the tenant term. That discriminator is what makes a
		// tenant named like the single-tenant default harmless: a multi-tenant host yields a scoped
		// partition whatever its tenants are called. Deciding it at filing time also keeps a certificate
		// reproducible if the deployment is reconfigured before the request executes. Same derivation as
		// InMemoryDataInventoryStore:58, deliberately — one rule, written once per store.
		var partition = _requireTenant
			? KeyedTenantPartition.FromContext(_tenantContext)
			: KeyedTenantPartition.Untenanted;


		var data = new ErasureRequestData
		{
			RequestId = request.RequestId,
			DataSubjectIdHash = HashDataSubjectId(request.DataSubjectId),
			IdType = request.IdType,
			TenantId = owner,
			TenantPartition = partition.TenantId,
			Scope = request.Scope,
			LegalBasis = request.LegalBasis,
			ExternalReference = request.ExternalReference,
			RequestedBy = request.RequestedBy,
			RequestedAt = request.RequestedAt,
			ScheduledExecutionAt = scheduledExecutionTime,
			Status = ErasureRequestStatus.Scheduled,
			CreatedAt = DateTimeOffset.UtcNow,
			UpdatedAt = DateTimeOffset.UtcNow
		};

		if (!_requests.TryAdd(request.RequestId, data))
		{
			// The specific type, not the base: a caller reading a bare InvalidOperationException as
			// "already on file" would do the same for a fault that stored nothing, and drop the request.
			throw DuplicateErasureRequestException.ForRequestId(request.RequestId);
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<ErasureStatus?> GetStatusAsync(
		Guid requestId,
		CancellationToken cancellationToken)
	{
		// Resolved before the lookup so that an unresolved tenant fails closed whether or not the row exists.
		var tenant = AmbientScope;

		// A row belonging to another tenant is reported exactly as a row that is not there. Distinguishing the
		// two would leak the existence of another tenant's request through the difference.
		if (!_requests.TryGetValue(requestId, out var data) || !MatchesAmbientTenant(tenant, data.TenantId))
		{
			return Task.FromResult<ErasureStatus?>(null);
		}

		return Task.FromResult<ErasureStatus?>(ToStatus(data));
	}

	/// <inheritdoc />
	public Task<ErasureWriteOutcome> UpdateStatusAsync(
		Guid requestId,
		ErasureRequestStatus status,
		ErasureRequestStatus? expectedStatus,
		string? errorMessage,
		string? claimIdentity,
		CancellationToken cancellationToken)
	{
		// Taking custody has its own door. Refused here rather than quietly handled, because a second
		// door would advance a request into the claimed status for a caller holding no identity, and
		// every fenced write it then made would be comparing against nothing.
		if (status == ErasureRequestStatus.InProgress)
		{
			throw new ArgumentException(
				"The in-progress status records CUSTODY and may only be written by ClaimForExecutionAsync, "
				+ "which is the only member that mints the identity a fenced write presents.",
				nameof(status));
		}

		// Resolved before the lookup so that an unresolved tenant fails closed whether or not the row exists.
		var tenant = AmbientScope;

		// Another tenant's row is treated as absent, so a cross-tenant update reports the same "no such
		// request" result as a missing one rather than mutating a row the caller cannot see.
		if (!_requests.TryGetValue(requestId, out var data) || !MatchesAmbientTenant(tenant, data.TenantId))
		{
			return Task.FromResult(ErasureWriteOutcome.NotFound);
		}

		lock (data.Transitions)
		{
			// THE CLAIM TERM, and null is the STRICTER argument. A presented identity must be the one the
			// request carries. No identity asserts that nobody holds the request at all -- which is what a
			// supervisor means when it re-offers an abandoned request or marks one terminally failed.
			//
			// The expiry is consulted HERE and only here, never for a presented identity. A lapse does not
			// revoke: a holder whose run outlasted its own lease, but whom nobody superseded, still holds
			// the identity the row carries and its writes still apply. Were it otherwise, an executor that
			// had already destroyed a subject's keys could be refused its own completion, leaving the
			// erasure done and no certificate able to attest it.
			// A LEGAL HOLD IS OUTSIDE THE FENCE, and for the same reason the destruction ledger is: refusing
			// it loses something the law requires rather than preventing a false claim. A hold recorded
			// while an executor holds the claim MUST land -- otherwise the hold is silently dropped and
			// the run completes over data the controller has asserted a legal basis to retain, which is
			// the one outcome this subsystem may never produce.
			//
			// The asymmetry is this subsystem's own stated principle: withholding an erasure is
			// recoverable by releasing or narrowing the hold, and destroying held records is not. The
			// worst a stale actor can do through this door is DENY an erasure another instance owns,
			// which is visible in the status and reversible; the worst a fence here can do is destroy
			// retained data and certify it.
			if (status != ErasureRequestStatus.BlockedByLegalHold)
			{
				if (claimIdentity is null)
				{
					if (data.ClaimIdentity is not null && data.ClaimExpiresAt > _timeProvider.GetUtcNow())
					{
						return Task.FromResult(ErasureWriteOutcome.ClaimLost);
					}
				}
				else if (!string.Equals(data.ClaimIdentity, claimIdentity, StringComparison.Ordinal))
				{
					return Task.FromResult(ErasureWriteOutcome.ClaimLost);
				}
			}

			var current = data.Status;

			// expectedStatus TIGHTENS the store's own partition and never replaces it, so a caller cannot
			// use it to authorise a transition the partition forbids.
			if (expectedStatus is { } required && current != required)
			{
				return Task.FromResult(ErasureWriteOutcome.StatusRefused);
			}

			if (IsConditionalTarget(status) && !IsLegalSourceFor(status, current))
			{
				return Task.FromResult(ErasureWriteOutcome.StatusRefused);
			}

			data.Status = status;
			data.ErrorMessage = errorMessage;
			data.UpdatedAt = DateTimeOffset.UtcNow;

			// EVERY status write through this member ends custody. Taking custody has exactly ONE door, so
			// a write here always means the run is finished with the request -- it reached an outcome, it
			// was refused, or it handed the request to the next stage. An earlier version listed the
			// statuses that release, and the list was wrong for the awaiting-key-destruction handoff: the
			// executor kept its claim after handing over, so the confirmation pass could not claim until
			// the lease lapsed and nothing completed in the meantime.
			//
			// Both halves clear together, holding the pairing invariant the SQL schemas enforce with a
			// check constraint.
			data.ClaimIdentity = null;
			data.ClaimExpiresAt = null;

			return Task.FromResult(ErasureWriteOutcome.Applied);
		}
	}

	/// <inheritdoc />
	public Task<ErasureClaimResult> ClaimForExecutionAsync(
		Guid requestId,
		ErasureRequestStatus expectedStatus,
		TimeSpan claimDuration,
		CancellationToken cancellationToken)
	{
		// Zero is legal and yields an already-lapsed claim. Negative is not: it would be a claim that
		// expired before it was granted, which no caller can mean.
		ArgumentOutOfRangeException.ThrowIfLessThan(claimDuration, TimeSpan.Zero);

		var tenant = AmbientScope;

		if (!_requests.TryGetValue(requestId, out var data) || !MatchesAmbientTenant(tenant, data.TenantId))
		{
			return Task.FromResult(ErasureClaimResult.NotFound());
		}

		lock (data.Transitions)
		{
			// CUSTODY IS TESTED BEFORE STATUS, and the order is load-bearing rather than stylistic.
			// Granting a claim from Scheduled ADVANCES the status, so a second contender arriving a moment
			// later finds the row in the claimed status -- and a status test first would answer
			// StatusMismatch, which obliges the caller to STOP OFFERING the request. That is wrong and it
			// is wrong in the direction this whole mechanism exists to prevent: the request IS coming back
			// when the holder's claim lapses, and a caller told otherwise strands it. AlreadyClaimed is
			// the honest answer whenever somebody holds it, whatever the status says.
			if (data.ClaimIdentity is not null && data.ClaimExpiresAt > _timeProvider.GetUtcNow())
			{
				return Task.FromResult(ErasureClaimResult.AlreadyClaimed());
			}

			if (data.Status != expectedStatus)
			{
				return Task.FromResult(ErasureClaimResult.StatusMismatch());
			}

			// Opaque by construction: a GUID carries no order anyone could compare, which is the property
			// the contract requires. It is minted here and nowhere else.
			var identity = Guid.NewGuid().ToString("N");

			data.ClaimIdentity = identity;
			data.ClaimExpiresAt = _timeProvider.GetUtcNow().Add(claimDuration);

			// Claiming from Scheduled ADVANCES the status; claiming from AwaitingKeyDestruction leaves it
			// alone, because that status is a STAGE of the work rather than a statement about who is doing
			// it. One SET for both would silently un-stage a request mid-confirmation.
			if (expectedStatus == ErasureRequestStatus.Scheduled)
			{
				data.Status = ErasureRequestStatus.InProgress;
				data.ExecutedAt = DateTimeOffset.UtcNow;
			}

			data.UpdatedAt = DateTimeOffset.UtcNow;

			return Task.FromResult(ErasureClaimResult.Granted(identity));
		}
	}

	/// <summary>
	/// Whether a write PRODUCING this status must name a legal prior state rather than land
	/// unconditionally.
	/// </summary>
	/// <remarks>
	/// A status is conditional when some other operation's precondition succeeds BECAUSE the row is in
	/// it -- writing such a status is a grant of permission, not the recording of an outcome. That is
	/// true of <c>Scheduled</c> (the execution claim reads it) and of <c>AwaitingKeyDestruction</c> (the
	/// completion pass discovers by it, and <c>RecordCompletionAsync</c> is enabled by it).
	/// <c>InProgress</c> is conditional too and keeps its own compare-and-swap above, which is stricter
	/// than this and predates it.
	/// </remarks>
	private static bool IsConditionalTarget(ErasureRequestStatus status) =>
		status is ErasureRequestStatus.Scheduled or ErasureRequestStatus.AwaitingKeyDestruction;

	/// <summary>
	/// Whether <paramref name="target" /> may be written over a request currently in
	/// <paramref name="source" />.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <c>Scheduled</c> re-offers the request for execution, so it must not land on one that is owned or
	/// finished.
	/// </para>
	/// <para>
	/// <c>AwaitingKeyDestruction</c> puts the request into the completion pass's discovery set, and that
	/// pass asks the key provider to confirm destruction. Writing it over a CANCELLED request would feed
	/// a cancelled erasure's keys into the destruction pipeline, which that status's own documentation
	/// says cannot happen. Both legitimate writers are already inside the permitted set: the execution
	/// path records it from the claimed window, and the completion pass REWRITES it with a fresh detail
	/// message when it cannot yet complete -- which is why the source set includes the target itself and
	/// a stricter "from InProgress only" rule would break that pass.
	/// </para>
	/// </remarks>
	private static bool IsLegalSourceFor(ErasureRequestStatus target, ErasureRequestStatus source) =>
		target switch
		{
			ErasureRequestStatus.Scheduled => !IsOwnedOrTerminal(source),
			ErasureRequestStatus.AwaitingKeyDestruction =>
				source is ErasureRequestStatus.InProgress or ErasureRequestStatus.AwaitingKeyDestruction,

			// Every other target records an outcome and is unconditional; IsConditionalTarget gates the
			// call, so this arm is never reached for one.
			_ => true,
		};

	/// <summary>
	/// Whether a request in this status is owned by an executor or has reached a terminal outcome, and so
	/// must not be put back to <see cref="ErasureRequestStatus.Scheduled" />.
	/// </summary>
	/// <remarks>
	/// <c>InProgress</c> and <c>AwaitingKeyDestruction</c> are OWNED -- an executor is mid-run, or the
	/// key-management provider holds keys it has irreversibly scheduled for destruction. <c>Completed</c>
	/// and <c>Cancelled</c> are TERMINAL. Every other status (<c>Pending</c>, <c>Scheduled</c>,
	/// <c>BlockedByLegalHold</c>, <c>Failed</c>, <c>PartiallyCompleted</c>) describes a request nobody is
	/// running, so offering it again is legitimate and is how a transient failure is ever retried.
	/// </remarks>
	private static bool IsOwnedOrTerminal(ErasureRequestStatus status) =>
		status is ErasureRequestStatus.InProgress
			or ErasureRequestStatus.AwaitingKeyDestruction
			or ErasureRequestStatus.Completed
			or ErasureRequestStatus.Cancelled;

	/// <inheritdoc />
	public Task<ErasureWriteOutcome> RecordCompletionAsync(
		Guid requestId,
		int keysDeleted,
		int recordsAffected,
		Guid certificateId,
		string claimIdentity,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(claimIdentity);

		// Resolved before the lookup so that an unresolved tenant fails closed whether or not the row exists.
		var tenant = AmbientScope;

		// Another tenant's row is treated as absent, so completion against it raises the same not-found
		// failure as a missing request instead of writing a completion into another tenant's partition.
		// A completion may only be recorded over a request that is still IN the run. A legal hold or a
		// cancellation recorded while the contributors were working moves it out of the run deliberately,
		// and holds are re-checked exactly once, before a contributor pass the design calls long -- so
		// without this the winner is whoever writes last, and a signed Completed silently overwrites an
		// Article 17(3) hold. The permitted set is stated positively: a status this store does not
		// recognise must refuse rather than inherit "fine".
		if (!_requests.TryGetValue(requestId, out var data)
			|| !MatchesAmbientTenant(tenant, data.TenantId)
			|| data.Status is not (ErasureRequestStatus.Scheduled or ErasureRequestStatus.InProgress
				or ErasureRequestStatus.AwaitingKeyDestruction))
		{
			throw new KeyNotFoundException(
				$"No erasure request with id '{requestId}' is in a state from which a completion can be "
				+ "recorded: it does not exist, it belongs to another tenant, or its status changed while "
				+ "the run was executing. A legal hold or a cancellation recorded mid-run takes the request "
				+ "out of the run deliberately, and a completion must not overwrite it.");
		}

		lock (data.Transitions)
		{
			// THE FENCE, and it compares the identity and ONLY the identity. The expiry is not consulted:
			// a lapse does not revoke, so a holder nobody superseded still completes its own run. If this
			// also tested the expiry, an executor that had destroyed a subject's keys and overrun its
			// lease would be refused the certificate attesting what it had already done.
			if (!string.Equals(data.ClaimIdentity, claimIdentity, StringComparison.Ordinal))
			{
				return Task.FromResult(ErasureWriteOutcome.ClaimLost);
			}

			data.Status = ErasureRequestStatus.Completed;
			data.KeysDeleted = keysDeleted;
			data.RecordsAffected = recordsAffected;
			data.CertificateId = certificateId;
			data.CompletedAt = DateTimeOffset.UtcNow;
			data.UpdatedAt = DateTimeOffset.UtcNow;

			// Custody ends with the run, and both halves clear together to hold the pairing invariant.
			data.ClaimIdentity = null;
			data.ClaimExpiresAt = null;

			_requestToCertificate[requestId] = certificateId;

			return Task.FromResult(ErasureWriteOutcome.Applied);
		}
	}

	/// <inheritdoc />
	public Task StageKeyDestructionAsync(
		Guid requestId,
		string keyHandle,
		string keyGeneration,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(keyHandle);
		ArgumentException.ThrowIfNullOrEmpty(keyGeneration);

		var data = ResolveRequestForKeyRecord(
			requestId,
			"a destruction cannot be staged against it. This throws rather than returning quietly: the staged "
			+ "intent is the only copy of the generation that survives the destruction, so losing it silently "
			+ "would leave a crashed pass with nothing to recover from.");

		// Kept APART from the ledger, in a collection no predicate reads. Between this write and the
		// destruction the generation names LIVE material, so a store that held it where the read predicate
		// could reach it would report a live key as destroyed.
		_ = data.StagedDestructions.GetOrAdd(keyHandle, static _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal))
			.TryAdd(keyGeneration, 0);
		data.UpdatedAt = DateTimeOffset.UtcNow;

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<string>> GetStagedKeyGenerationsAsync(
		Guid requestId,
		string keyHandle,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(keyHandle);

		// Resolved before the lookup so that an unresolved tenant fails closed whether or not the row exists.
		var tenant = AmbientScope;

		if (!_requests.TryGetValue(requestId, out var data)
			|| !MatchesAmbientTenant(tenant, data.TenantId)
			|| !data.StagedDestructions.TryGetValue(keyHandle, out var staged))
		{
			return Task.FromResult<IReadOnlyList<string>>([]);
		}

		return Task.FromResult<IReadOnlyList<string>>([.. staged.Keys]);
	}

	/// <inheritdoc />
	public Task RecordKeyDestroyedAsync(
		Guid requestId,
		string keyHandle,
		string keyGeneration,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(keyHandle);
		ArgumentException.ThrowIfNullOrEmpty(keyGeneration);

		var data = ResolveRequestForKeyRecord(
			requestId,
			"a destroyed key cannot be recorded against it. This throws rather than returning quietly: the "
			+ "record is what lets a retry attest a destruction an earlier pass performed and what lets a read "
			+ "report the subject's erasure, so losing it silently would make that erasure permanently "
			+ "uncertifiable with nothing reporting why.");

		// The ledger. Keyed on (handle, generation): one pair is one destruction, so a second write for the
		// same pair is absorbed and the first instant stands. Two DIFFERENT generations destroyed at one handle
		// are two destructions and get two entries, which is what keying on the handle ALONE would have
		// silently dropped -- the pair keeps that property while removing the generation-alone key's
		// dependence on a uniqueness no provider is obliged to deliver.
		_ = _destroyedGenerations.TryAdd(
			new DestroyedKey(keyHandle, keyGeneration),
			new DestroyedGenerationRecord(requestId, keyHandle, DateTimeOffset.UtcNow, RecordedBy.FrameworkErasure));

		// The handle set a retry reads is DERIVED from these entries rather than tracked beside them -- see
		// DestroyedHandlesFor. One source means this store answers exactly as the SQL stores do, which read
		// the same question off the same ledger table; two sources would let the two diverge on an input the
		// conformance kit can reach.

		// ONLY AFTER the ledger entry exists, never before. Once it does, the staged intent carries nothing
		// the ledger does not, so dropping it loses no recovery information -- whereas dropping it first
		// would recreate the unrepairable window the staging collection was introduced to close. Other
		// generations staged at the same handle stay, because they are still unrecorded.
		if (data.StagedDestructions.TryGetValue(keyHandle, out var stagedForHandle))
		{
			_ = stagedForHandle.TryRemove(keyGeneration, out _);
		}

		data.UpdatedAt = DateTimeOffset.UtcNow;

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task RecordDestroyedGenerationAsync(
		string keyHandle,
		string keyGeneration,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(keyHandle);
		ArgumentException.ThrowIfNullOrEmpty(keyGeneration);

		// NO request, because the caller genuinely has none -- but the HANDLE is now required and the caller
		// does have it: you cannot destroy a key without naming it. The provenance value records that this
		// entry rests on the caller's assertion rather than on anything this framework observed; it is stated
		// rather than inferred from the absent request.
		//
		// TryAdd, so re-asserting a pair already on file is a no-op and the FIRST instant stands.
		_ = _destroyedGenerations.TryAdd(
			new DestroyedKey(keyHandle, keyGeneration),
			new DestroyedGenerationRecord(null, keyHandle, DateTimeOffset.UtcNow, RecordedBy.CallerAssertion));

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public ValueTask<bool> IsGenerationDestroyedAsync(
		string keyHandle,
		string keyGeneration,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(keyHandle);
		ArgumentException.ThrowIfNullOrEmpty(keyGeneration);

		// NO tenant term, deliberately, and NOT an oversight. The lookup is addressed by the ledger's whole key,
		// so it already selects at most one entry; a tenant term on such a lookup cannot admit a foreign entry
		// and its only reachable effect is turning the correct entry into none. Here that effect is the
		// catastrophic one in reverse: a read of a genuinely erased subject would stop reporting their erasure
		// and start failing, permanently. The HANDLE is what makes "one tenant cannot name another's entry"
		// true by construction rather than by assumption: this sentence used to rest on a generation being
		// minted by the backend and not derivable from a subject, which is true of every shipped provider and
		// is not something a consumer-supplied one is obliged to satisfy.
		//
		// Staged intents are NOT consulted. A staged generation names material that may still be live.
		return new ValueTask<bool>(_destroyedGenerations.ContainsKey(new DestroyedKey(keyHandle, keyGeneration)));
	}

	/// <inheritdoc />
	/// <inheritdoc />
	/// <remarks>
	/// <b>FALSE, and it is the honest answer rather than a limitation to work around.</b> Rows live in a
	/// <c>ConcurrentDictionary</c> and are gone when the process is. An erasure run after a restart would
	/// enumerate nothing, destroy only the handles it can derive from declared retentions, and certify --
	/// so this store must not supply the mint ledger in any deployment whose erasures are relied upon.
	/// Startup validation refuses that composition unless the consumer states the choice by name.
	/// </remarks>
	public bool IsDurable => false;

	public Task RecordMintIntentAsync(
		string keyHandle,
		TenantId tenantId,
		string dataSubjectIdHash,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(keyHandle);
		ArgumentNullException.ThrowIfNull(tenantId);
		ArgumentException.ThrowIfNullOrWhiteSpace(dataSubjectIdHash);

		if (keyHandle.Length > MaxKeyHandleLength)
		{
			throw new ArgumentException(
				$"A key handle is at most {MaxKeyHandleLength} characters and this one is {keyHandle.Length}. "
				+ "Refusing rather than storing it: a truncated handle is a row that matches no envelope, "
				+ "which is an omission from the enumeration an erasure destroys from.",
				nameof(keyHandle));
		}

		// MintedAt is left NULL: the key does not exist yet, and this row must not claim otherwise.
		var entry = new MintedHandleRecord(
			tenantId.Value, dataSubjectIdHash, DateTimeOffset.UtcNow, MintedAt: null);

		// TryAdd, so recording a handle that already has an entry is a no-op and the FIRST instant stands.
		// A lost race is a SUCCESS, not an error: the first write per subject and obligation class races with
		// every other, and failing here would fail a perfectly good personal-data write on a path that is
		// deliberately fail-closed.
		if (_mintedHandles.TryAdd(keyHandle, entry))
		{
			return Task.CompletedTask;
		}

		// THE ENUMERATION KEY MUST AGREE, and a disagreement is refused rather than silently keeping the
		// first spelling. A handle is derived from the owner and the subject, so a collision between two of
		// them is either a derivation defect or a hash collision. Keeping the first silently would leave the
		// handle recorded while the CORRECT owner's enumeration returned nothing -- worse than no row at all,
		// because the erasure then destroys what it found and certifies.
		var existing = _mintedHandles[keyHandle];

		if (!string.Equals(existing.TenantId, tenantId.Value, StringComparison.Ordinal)
			|| !string.Equals(existing.DataSubjectIdHash, dataSubjectIdHash, StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				"This key handle is already recorded for a different owner or data subject. A handle is "
				+ "derived from both, so a collision is either a derivation defect or a hash collision, and "
				+ "silently keeping the first spelling would leave the handle unenumerable for the owner "
				+ "whose data it actually holds.");
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<MintLedgerSnapshot> ListHandlesAsync(
		TenantId tenantId,
		string dataSubjectIdHash,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(tenantId);
		ArgumentException.ThrowIfNullOrWhiteSpace(dataSubjectIdHash);

		// ORDINAL on both terms, and this is the one place in this store where a tenant term is load-bearing
		// rather than redundant: the enumeration is addressed by (owner, subject) and NOT by the ledger's key,
		// so without the term it would return every subject's handles. A folding comparison would let one
		// owner's handles be enumerated for another -- and this set is what an erasure DESTROYS.
		//
		// Complete, with no paging and no limit: a truncated answer here is a handle the erasure does not
		// destroy, which is a subject reported erased whose data is still readable.
		// ONE ENUMERATION, SPLIT ONCE. Taking the rows in a single pass and partitioning them here is what
		// makes the two sets a coherent snapshot; two passes over a live dictionary could observe a row
		// moving from pending to minted and report it in neither set.
		var rows = _mintedHandles
			.Where(entry =>
				string.Equals(entry.Value.TenantId, tenantId.Value, StringComparison.Ordinal)
				&& string.Equals(entry.Value.DataSubjectIdHash, dataSubjectIdHash, StringComparison.Ordinal))
			.Select(entry => (entry.Key, entry.Value.MintedAt))
			.ToList();

		IReadOnlyList<string> minted = [.. rows.Where(r => r.MintedAt is not null).Select(r => r.Key)];
		IReadOnlyList<string> pending = [.. rows.Where(r => r.MintedAt is null).Select(r => r.Key)];

		return Task.FromResult(new MintLedgerSnapshot(minted, pending));
	}

	/// <inheritdoc />
	public Task MarkMintedAsync(string keyHandle, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(keyHandle);
		cancellationToken.ThrowIfCancellationRequested();

		// REFUSED when no row exists, rather than inserting one. The intent row is always written first, so
		// its absence here means the caller skipped that step -- and a key may then exist at a handle no
		// erasure will enumerate, which is the failure this ledger exists to remove. Inserting a row now
		// would paper over it and lose the owner and subject the row has to carry.
		if (!_mintedHandles.TryGetValue(keyHandle, out var existing))
		{
			throw new InvalidOperationException(
				$"No mint-ledger row exists for key handle '{keyHandle}', so its key creation cannot be "
				+ "confirmed. The row is written before the key is created, always; its absence means the "
				+ "intent step was skipped and a key may exist at a handle no erasure can enumerate.");
		}

		// IDEMPOTENT, and the FIRST confirmation stands. A later confirmation must not move the instant: the
		// row records when the key came into existence, and that is not a fact a retry may restate.
		if (existing.MintedAt is not null)
		{
			return Task.CompletedTask;
		}

		// A lost race against another confirmation is a SUCCESS -- both callers want the row confirmed, and
		// it is. Nothing else mutates this row, so no other update can be clobbered here.
		_ = _mintedHandles.TryUpdate(
			keyHandle,
			existing with { MintedAt = DateTimeOffset.UtcNow },
			existing);

		return Task.CompletedTask;
	}

	/// <summary>
	/// Resolves the request a key record is about to be written against, failing closed on an unresolved tenant
	/// and treating another tenant's request as absent.
	/// </summary>
	/// <remarks>
	/// Shared by the stage and the record so the tenant check cannot drift between them: a record written into
	/// the wrong partition would let the wrong request attest coverage it never achieved.
	/// </remarks>
	private ErasureRequestData ResolveRequestForKeyRecord(Guid requestId, string consequence)
	{
		// Resolved before the lookup so that an unresolved tenant fails closed whether or not the row exists.
		var tenant = AmbientScope;

		if (!_requests.TryGetValue(requestId, out var data) || !MatchesAmbientTenant(tenant, data.TenantId))
		{
			throw new KeyNotFoundException(
				$"No erasure request with id '{requestId}' exists in this tenant, so {consequence}");
		}

		return data;
	}

	/// <summary>
	/// The handles this request has destroyed, derived from the ledger entries it wrote.
	/// </summary>
	/// <remarks>
	/// A retry has to know WHICH handles an earlier pass destroyed, because the key store reports one it
	/// destroyed exactly as it reports one it never held. Derived rather than tracked separately so this store
	/// answers the question from the same records the SQL stores read it from; a second source of truth here
	/// would diverge from them the first time two records shared a generation. Ordinal throughout -- a handle is
	/// an opaque identifier, never text to be compared culturally.
	/// </remarks>
	/// <!-- ponytail: a scan of the ledger per status read. This store is for development and testing; if it
	///      ever needs to answer at scale, index by request id. -->
	private IReadOnlyList<string> DestroyedHandlesFor(Guid requestId) =>
	[
		.. _destroyedGenerations.Values
			// A caller-asserted entry has no request, so it never matches and never appears in a request's
			// destroyed-handle set -- correct, because this framework did not destroy it.
			.Where(r => r.RequestId == requestId)
			.Select(static r => r.KeyHandle!)
			.Distinct(StringComparer.Ordinal),
	];

	/// <summary>
	/// The ledger's provenance values, matching the SQL stores' so an auditor reading rows from any provider
	/// sees the same two strings.
	/// </summary>
	/// <remarks>
	/// Named constants rather than inline literals because the value is a STORED CONTRACT: an auditor reads it
	/// to tell an intent-backed service record from a caller's unverified assertion, so the two doors must
	/// never converge on one string. It is an audit attribute only — the read predicate never names it, so a
	/// row is a row whichever writer produced it.
	/// </remarks>
	private static class RecordedBy
	{
		/// <summary>An erasure this framework performed: staged before the destroy, recorded after it.</summary>
		public const string FrameworkErasure = "framework-erasure";

		/// <summary>A destruction the CALLER performed and asserted. Nothing here re-verified it.</summary>
		public const string CallerAssertion = "caller-assertion";
	}

	/// <summary>The ledger's whole key: one handle's one generation.</summary>
	/// <param name="KeyHandle">The handle whose material was destroyed.</param>
	/// <param name="KeyGeneration">The generation of the material destroyed at that handle.</param>
	/// <remarks>
	/// A record struct rather than a composed string, so no separator can be forged into a second key and no
	/// caller can assemble one by hand. Equality is ordinal and case-sensitive on both components, matching
	/// the binary collation the SQL schemas declare.
	/// </remarks>
	private readonly record struct DestroyedKey(string KeyHandle, string KeyGeneration);

	/// <summary>
	/// A ledger entry: this handle's generation was destroyed at this instant, and this is who says so.
	/// </summary>
	/// <remarks>
	/// The REQUEST is nullable because a destruction asserted through the ledger's public write has none. The
	/// HANDLE is not: it is part of the ledger's key, so every entry has one, and it was nullable only while
	/// the key was the generation alone. <paramref name="RecordedBy"/> states the provenance explicitly rather
	/// than leaving it to be inferred from the null request.
	/// </remarks>
	private sealed record DestroyedGenerationRecord(
		Guid? RequestId,
		string KeyHandle,
		DateTimeOffset DestroyedAt,
		string RecordedBy);

	/// <summary>
	/// A mint-ledger entry: this handle was recorded for this subject under this owner, at this instant.
	/// </summary>
	/// <remarks>
	/// <para>
	/// It carries NO obligation class. The handle is derived from the class, so storing the class as well
	/// would duplicate derivable state and create an axis on which the entry could disagree with the handle
	/// it names. An erasure decides an entry's obligation by testing its handle against the handles the
	/// currently-declared retentions produce — equality on an opaque value, with nothing to interpret.
	/// </para>
	/// <para>
	/// <b>MintedAt is NULLABLE, and the null is the whole point.</b> The row is written BEFORE the key is
	/// created, so at that moment nothing can say whether the key exists. Null means exactly that and
	/// nothing more -- it is not a default standing in for a value nobody measured. An erasure that finds a
	/// null cannot tell a write in flight from a write abandoned, which is why it must report a concurrent
	/// write rather than certify; see <see cref="MintLedgerSnapshot"/>.
	/// </para>
	/// </remarks>
	private sealed record MintedHandleRecord(
		string TenantId,
		string DataSubjectIdHash,
		DateTimeOffset IntentAt,
		DateTimeOffset? MintedAt);

	/// <inheritdoc />
	public Task<bool> RecordCancellationAsync(
		Guid requestId,
		string reason,
		string cancelledBy,
		CancellationToken cancellationToken)
	{
		// Resolved before the lookup so that an unresolved tenant fails closed whether or not the row exists.
		var tenant = AmbientScope;

		// Another tenant's row is treated as absent, so one tenant cannot cancel another tenant's erasure.
		if (!_requests.TryGetValue(requestId, out var data) || !MatchesAmbientTenant(tenant, data.TenantId))
		{
			return Task.FromResult(false);
		}

		// UNDER THE ROW'S TRANSITION LOCK, like every other writer of this field. This used to be two
		// bare compare-and-swaps, and being atomic per-field is not the same as being atomic with
		// respect to the other writers: the status write and the completion write both decide under the
		// lock and then store, so a cancellation landing between one of those decisions and its store
		// was overwritten and the caller was still told TRUE.
		//
		// The interleaving, with no claim involved and the request merely Scheduled:
		//   T1 UpdateStatusAsync(Failed) takes the lock, reads Scheduled, the partition admits Failed;
		//   T2 this method's CAS Scheduled -> Cancelled succeeds and returns TRUE;
		//   T1 stores Failed over Cancelled.
		// The store then reports an erasure the caller was told was cancelled, with the cancellation
		// reason and actor stamped on a request whose status says it failed. Neither SQL provider can
		// produce it -- a single conditional UPDATE holds the row -- so this was the reference store
		// failing to be a subtype of its own contract, which is the direction that matters least for
		// safety and most for a consumer writing against the in-memory store first.
		lock (data.Transitions)
		{
			var current = (ErasureRequestStatus)Volatile.Read(ref data.StatusValue);

			if (current is not (ErasureRequestStatus.Pending or ErasureRequestStatus.Scheduled))
			{
				return Task.FromResult(false);
			}

			data.Status = ErasureRequestStatus.Cancelled;
			data.CancelledAt = DateTimeOffset.UtcNow;
			data.CancellationReason = reason;
			data.CancelledBy = cancelledBy;
			data.UpdatedAt = DateTimeOffset.UtcNow;

			return Task.FromResult(true);
		}
	}

	/// <inheritdoc />
	/// <remarks>
	/// ESTATE-WIDE BY DESIGN — deliberately not tenant-scoped, and the asymmetry is load-bearing. The erasure
	/// scheduler drains every tenant's due requests in one background pass with no ambient tenant
	/// established; scoping it would resolve the tenant as absent, return the empty set, and stall erasure
	/// permanently while still satisfying a safety-only test. Each row carries its own tenant, so the
	/// scheduler establishes a per-request scope as it drains. This surface is reachable only through
	/// <see cref="IErasureQueryStore"/>, which a per-tenant caller does not take a dependency on.
	/// </remarks>
	public Task<IReadOnlyList<ErasureStatus>> GetScheduledRequestsAsync(
		int maxResults,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		// THE SECOND DISJUNCT IS WHAT MAKES AN INTERRUPTED ERASURE COME BACK. A request whose executor
		// died stays in the claimed status with nothing to advance it, and this query is the only thing
		// that offers work -- so without the lapse term it is excluded forever, correctly and silently,
		// and the subject's data is never erased.
		var scheduled = _requests.Values
			.Where(r => (r.Status == ErasureRequestStatus.Scheduled && r.ScheduledExecutionAt <= now)
						|| (r.Status == ErasureRequestStatus.InProgress && HasLapsedClaim(r)))
			.OrderBy(r => r.ScheduledExecutionAt)
			.Take(maxResults)
			.Select(ToStatus)
			.ToList();

		return Task.FromResult<IReadOnlyList<ErasureStatus>>(scheduled);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<ErasureStatus>> GetAwaitingKeyDestructionRequestsAsync(
		int pageNumber, int pageSize, CancellationToken cancellationToken)
	{
		// NOT DiscoverByStatus, and the divergence is deliberate: this status is the only one a
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
		cancellationToken.ThrowIfCancellationRequested();

		// ESTATE-WIDE AND DELIBERATELY UNSCOPED, like the other background discovery surfaces: this runs
		// from a host that has no tenant, so a tenant predicate here returns nothing and the whole
		// confirmation pass silently stops. The claim exclusion below is the only new term.
		var offset = ((long)pageNumber - 1) * pageSize;
		var matching = _requests.Values
			.Where(r => r.Status == ErasureRequestStatus.AwaitingKeyDestruction && !HasLiveClaim(r))
			.OrderBy(r => r.RequestedAt).ThenBy(r => r.RequestId).ToArray();

		return Task.FromResult<IReadOnlyList<ErasureStatus>>(offset >= matching.Length
			? []
			: matching.Skip((int)offset).Take(pageSize).Select(ToStatus).ToList());
	}

	/// <summary>Whether this request is held by a claim that has not lapsed.</summary>
	/// <remarks>
	/// Read WITHOUT the row's transition lock on purpose: a discovery pass that observed a claim granted
	/// or released a moment later is simply offered, or not offered, one poll early or late. The lock
	/// protects the ATOMICITY of taking custody, and the claim itself is what refuses a stale offer, so
	/// taking it here would serialise every poll against every executor for no safety.
	/// </remarks>
	private bool HasLiveClaim(ErasureRequestData data) =>
		data.ClaimIdentity is not null && data.ClaimExpiresAt > _timeProvider.GetUtcNow();

	/// <summary>Whether this request carries a claim that has elapsed, so it may be recovered.</summary>
	private bool HasLapsedClaim(ErasureRequestData data) =>
		data.ClaimIdentity is not null && data.ClaimExpiresAt <= _timeProvider.GetUtcNow();

	/// <inheritdoc />
	public Task<IReadOnlyList<ErasureStatus>> GetHoldDeferredRequestsAsync(
		int pageNumber, int pageSize, CancellationToken cancellationToken) =>
		DiscoverByStatus(ErasureRequestStatus.BlockedByLegalHold, pageNumber, pageSize, cancellationToken);

	/// <summary>
	/// The estate-wide discovery both background members are served by.
	/// </summary>
	/// <remarks>
	/// Shared deliberately: the two members differ only in the status they look for, and what drifts
	/// between hand-copied discovery paths is the paging, the ordering and the tenancy rather than the
	/// constant. No ambient-tenant match is applied, which the nearest sibling reads in this file DO --
	/// a background worker runs under no tenant scope. This reference store cannot catch an ambient
	/// term wrongly added by a provider, because its own match short-circuits to true under single
	/// tenancy, which is how most callers exercise it; the shared conformance arm is what catches that.
	/// </remarks>
	private Task<IReadOnlyList<ErasureStatus>> DiscoverByStatus(
		ErasureRequestStatus status, int pageNumber, int pageSize, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
		ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 1000);
		cancellationToken.ThrowIfCancellationRequested();
		var offset = ((long)pageNumber - 1) * pageSize;
		var matching = _requests.Values
			.Where(row => row.Status == status)
			.OrderBy(row => row.RequestedAt).ThenBy(row => row.RequestId).ToArray();
		return Task.FromResult<IReadOnlyList<ErasureStatus>>(offset >= matching.Length
			? []
			: matching.Skip((int)offset).Take(pageSize).Select(ToStatus).ToList());
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<ErasureStatus>> ListRequestsAsync(
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

		var query = _requests.Values.AsEnumerable();

		// The ambient term is applied FIRST and unconditionally under multi-tenancy, so it is a floor rather
		// than an alternative. The caller's own tenantId is then applied on top: two equality terms can only
		// intersect, so asking for another tenant yields the empty set instead of that tenant's rows, and
		// omitting the argument no longer removes the filter. Widening is not expressible here. The scope is
		// resolved eagerly so an unresolved tenant fails closed rather than yielding an empty page.
		var tenant = AmbientScope;
		query = query.Where(r => MatchesAmbientTenant(tenant, r.TenantId));

		if (status.HasValue)
		{
			query = query.Where(r => r.Status == status.Value);
		}

		if (!string.IsNullOrEmpty(tenantId))
		{
			query = query.Where(r => r.TenantId == tenantId);
		}

		if (fromDate.HasValue)
		{
			query = query.Where(r => r.RequestedAt >= fromDate.Value);
		}

		if (toDate.HasValue)
		{
			query = query.Where(r => r.RequestedAt <= toDate.Value);
		}

		var results = query
			.OrderByDescending(r => r.RequestedAt)
			.Skip((pageNumber - 1) * pageSize)
			.Take(pageSize)
			.Select(ToStatus)
			.ToList();

		return Task.FromResult<IReadOnlyList<ErasureStatus>>(results);
	}

	/// <inheritdoc />
	public Task SaveCertificateAsync(
		ErasureCertificate certificate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(certificate);

		if (!_certificates.TryAdd(certificate.Payload.CertificateId, certificate))
		{
			throw DuplicateErasureCertificateException.ForCertificateId(certificate.Payload.CertificateId);
		}

		// KEEP THE NEWEST, rather than the last written. This was an unconditional overwrite, which made the
		// per-request answer depend on INSERTION ORDER while both SQL providers resolve it by
		// "ORDER BY generated_at DESC, certificate_id DESC". A request can legitimately hold two certificates
		// -- the partial branch issues one and the completion branch another -- so the three stores have to
		// agree on WHICH one the lookup returns, and this is the conformance reference the other two are
		// measured against. The tiebreak on id is what makes the order total, exactly as in the SQL clause.
		_ = _requestToCertificate.AddOrUpdate(
			certificate.Payload.RequestId,
			certificate.Payload.CertificateId,
			(_, existingId) =>
				_certificates.TryGetValue(existingId, out var existing)
				&& (existing.Payload.GeneratedAt > certificate.Payload.GeneratedAt
					|| (existing.Payload.GeneratedAt == certificate.Payload.GeneratedAt
						&& existing.Payload.CertificateId.CompareTo(certificate.Payload.CertificateId) > 0))
					? existingId
					: certificate.Payload.CertificateId);

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<ErasureCertificate?> GetCertificateAsync(
		Guid requestId,
		CancellationToken cancellationToken)
	{
		// A certificate carries no tenant of its own: it belongs to the request it certifies, so its tenant is
		// that request's. Scoping through the request is what keeps the certificate and the row it certifies
		// in agreement — giving the certificate a second tenant value would let the two disagree.
		var tenant = AmbientScope;

		if (!_requestToCertificate.TryGetValue(requestId, out var certId)
			|| !OwningRequestMatchesAmbientTenant(tenant, requestId))
		{
			return Task.FromResult<ErasureCertificate?>(null);
		}

		_ = _certificates.TryGetValue(certId, out var cert);
		return Task.FromResult(cert);
	}

	/// <inheritdoc />
	public Task<ErasureCertificate?> GetCertificateByIdAsync(
		Guid certificateId,
		CancellationToken cancellationToken)
	{
		// Scoped through the certified request, for the same reason as the by-request lookup above.
		var tenant = AmbientScope;

		if (!_certificates.TryGetValue(certificateId, out var cert)
			|| !OwningRequestMatchesAmbientTenant(tenant, cert.Payload.RequestId))
		{
			return Task.FromResult<ErasureCertificate?>(null);
		}

		return Task.FromResult<ErasureCertificate?>(cert);
	}

	/// <inheritdoc />
	/// <remarks>
	/// ESTATE-WIDE BY DESIGN, like <c>GetScheduledRequestsAsync</c>: a retention sweep that runs from a
	/// background service with no ambient tenant and must remove every tenant's expired certificates in one
	/// pass. Scoping it would silently stop honouring the retention limit for every tenant but one. It is
	/// reachable only through <see cref="IErasureCertificateStore"/>, not the per-tenant request path.
	/// </remarks>
	public Task<int> CleanupExpiredCertificatesAsync(
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;
		var expired = _certificates.Values
			.Where(c => c.Payload.RetainUntil < now)
			.Select(c => c.Payload.CertificateId)
			.ToList();

		var count = 0;
		foreach (var id in expired)
		{
			if (_certificates.TryRemove(id, out var cert))
			{
				_ = _requestToCertificate.TryRemove(cert.Payload.RequestId, out _);
				count++;
			}
		}

		return Task.FromResult(count);
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

	/// <summary>
	/// Clears all data from the store.
	/// </summary>
	public void Clear()
	{
		_requests.Clear();
		_certificates.Clear();
		_requestToCertificate.Clear();

		// The ledger and the staged intents go too. The ledger is keyed on the generation alone and is NOT
		// scoped by request, so leaving it behind would let a cleared store keep reporting generations as
		// destroyed -- an arm that then asserted "not destroyed" could never fail.
		_destroyedGenerations.Clear();

		// THE MINT LEDGER IS DELIBERATELY *NOT* CLEARED, and the superseded reasoning is quoted below because
		// it was wrong in a way worth recognising rather than deleting.
		//
		// Clearing the DESTRUCTION ledger above moves toward the SAFE answer: no record, no tombstone, the
		// read proceeds. Clearing the MINT ledger moves toward the CATASTROPHIC one: an erasure then
		// enumerates fewer handles than exist, destroys what it found, and certifies. The contract states
		// that rows are insert-only and permanent -- "no sweep, no expiry, no administrative reset" -- for
		// exactly that reason, so a member that removes one makes this type a non-subtype of the contract it
		// declares: the abstraction forbids the transition row-present to row-absent and this permitted it.
		//
		// SUPERSEDED, and the flaw is in its last step rather than its premises: "It is cleared here anyway,
		// and that is coherent only because this store is DEVELOPMENT-ONLY and already loses every row on a
		// process restart. Clear() is the same loss on demand, not a new one, so the contract's durability
		// requirement is what rules this store out of a deployment whose erasures are relied upon -- not this
		// line." Every clause of that is true of the STORE and none of it establishes anything about the
		// TYPE. It justifies one contract violation by appealing to a second one, which is an argument that
		// can license anything.
		//
		// Nothing needed it. A fresh instance is how a caller gets a fresh ledger, and the conformance kit
		// already builds one per arm.
	}

	// Not static: the destroyed-handle set is derived from the instance's ledger rather than carried on the
	// request, so that this store and the SQL stores answer the question from one source each.
	private ErasureStatus ToStatus(ErasureRequestData data) =>
		new()
		{
			RequestId = data.RequestId,
			DataSubjectIdHash = data.DataSubjectIdHash,
			IdType = data.IdType,
			TenantId = data.TenantId,
			TenantPartition = KeyedTenantPartition.FromStoredValue(data.TenantPartition),
			Scope = data.Scope,
			LegalBasis = data.LegalBasis,
			Status = data.Status,
			ExternalReference = data.ExternalReference,
			RequestedBy = data.RequestedBy,
			RequestedAt = data.RequestedAt,
			ScheduledExecutionAt = data.ScheduledExecutionAt,
			ExecutedAt = data.ExecutedAt,
			CompletedAt = data.CompletedAt,
			CancelledAt = data.CancelledAt,
			CancellationReason = data.CancellationReason,
			CancelledBy = data.CancelledBy,
			KeysDeleted = data.KeysDeleted,
			DestroyedKeyHandles = DestroyedHandlesFor(data.RequestId),
			RecordsAffected = data.RecordsAffected,
			CertificateId = data.CertificateId,
			ErrorMessage = data.ErrorMessage,
			UpdatedAt = data.UpdatedAt
		};

	/// <summary>
	/// Decides whether a stored row's tenant satisfies the ambient tenant term.
	/// </summary>
	/// <param name="tenant">The scope resolved once at the start of the operation.</param>
	/// <param name="rowTenantId">The tenant value stored on the row.</param>
	/// <returns>
	/// <see langword="true"/> when multi-tenancy is not active, or when the row belongs to the ambient
	/// tenant; otherwise <see langword="false"/>.
	/// </returns>
	/// <remarks>
	/// <para>
	/// The single comparison site for this store: every tenant-facing operation routes through it rather than
	/// comparing a tenant value inline, so the match cannot be omitted at one call site and applied at
	/// another. The comparison is ordinal because a tenant identifier is case-sensitive throughout the
	/// framework — matching case-insensitively here would let two distinct tenants read each other's rows.
	/// An erasure request is matched on plain equality: unlike a legal hold, an unowned request is not a
	/// control that anything else depends on, so there is no null-is-global case to admit.
	/// </para>
	/// <para>
	/// The scope is taken as a parameter rather than read here, because a caller that read it lazily — only
	/// once it had a row in hand — would make failing closed depend on whether the store happened to hold
	/// data: an unresolved tenant would throw against a populated store and quietly return "not found"
	/// against an empty one. Each operation resolves the scope up front and passes it in, so the fail-closed
	/// throw is a property of the deployment rather than of the data.
	/// </para>
	/// </remarks>
	private bool MatchesAmbientTenant(TenantScope tenant, string? rowTenantId) =>
		!_requireTenant || string.Equals(rowTenantId, tenant.TenantId, StringComparison.Ordinal);

	/// <summary>
	/// Decides whether the request a certificate certifies belongs to the ambient tenant.
	/// </summary>
	/// <param name="tenant">The scope resolved once at the start of the operation.</param>
	/// <param name="requestId">The identifier of the certified request.</param>
	/// <returns>
	/// <see langword="true"/> when multi-tenancy is not active, or when the certified request exists and
	/// belongs to the ambient tenant; otherwise <see langword="false"/>.
	/// </returns>
	/// <remarks>
	/// Under multi-tenancy the certified request must still exist for the certificate to be readable: a
	/// certificate whose request has gone has no tenant to be checked against, and returning it would hand
	/// out a document whose ownership can no longer be established. Without multi-tenancy the certificate is
	/// returned regardless, which is the single-tenant behaviour unchanged.
	/// </remarks>
	private bool OwningRequestMatchesAmbientTenant(TenantScope tenant, Guid requestId)
	{
		if (!_requireTenant)
		{
			return true;
		}

		return _requests.TryGetValue(requestId, out var request) && MatchesAmbientTenant(tenant, request.TenantId);
	}

	private string HashDataSubjectId(string dataSubjectId) =>
		_dataSubjectHasher.HashDataSubjectId(dataSubjectId);

	private sealed class ErasureRequestData
	{
		/// <summary>The partition a retention declaration must name to apply to this request.</summary>
		/// <remarks>
		/// Stored separately from <c>TenantId</c> because they answer different questions: TenantId is the
		/// OWNER the data was encrypted under and what key handles derive from, this is the PARTITION the
		/// retention join compares. One field could not be both, and the collision destroyed retained keys.
		/// </remarks>
		public required string TenantPartition { get; init; }

		public Guid RequestId { get; init; }
		public required string DataSubjectIdHash { get; init; }
		public DataSubjectIdType IdType { get; init; }
		public string? TenantId { get; init; }
		public ErasureScope Scope { get; init; }
		public ErasureLegalBasis LegalBasis { get; init; }
		public string? ExternalReference { get; init; }
		public required string RequestedBy { get; init; }
		public DateTimeOffset RequestedAt { get; init; }
		public DateTimeOffset? ScheduledExecutionAt { get; set; }
		public DateTimeOffset? ExecutedAt { get; set; }
		public DateTimeOffset? CompletedAt { get; set; }
		public DateTimeOffset? CancelledAt { get; set; }
		public string? CancellationReason { get; set; }
		public string? CancelledBy { get; set; }
		public int StatusValue;

		/// <summary>
		/// Serialises every transition of this request's STATUS and its CUSTODY together.
		/// </summary>
		/// <remarks>
		/// <para>
		/// This replaced three separate compare-and-swaps on <see cref="StatusValue"/>, and the reason is
		/// that the atomic step is no longer one integer wide. Granting a claim writes the status, the
		/// identity and the expiry, and those three must become visible together or not at all: two
		/// interlocked operations would let a second caller observe the status it needs between them, be
		/// granted as well, and then write past a fence that is comparing against its own identity. That
		/// is a weakened postcondition NO SEQUENTIAL TEST CAN OBSERVE, which is why it is structural here
		/// rather than argued in a comment.
		/// </para>
		/// <para>
		/// A lock rather than a wider interlocked value because the correctness is then obvious instead of
		/// derived, and because this store is a development and test substrate where per-request
		/// contention costs nothing. The SQL providers get the same indivisibility from a single
		/// conditional UPDATE.
		/// </para>
		/// </remarks>
		public readonly object Transitions = new();

		/// <summary>The identity of the claim currently held, or null when nobody holds this request.</summary>
		/// <remarks>
		/// Only ever read or written under <see cref="Transitions"/>. Paired with
		/// <see cref="ClaimExpiresAt"/>: both null or both set, never one, which is the same invariant the
		/// SQL schemas enforce with a check constraint.
		/// </remarks>
		public string? ClaimIdentity;

		/// <summary>When the current claim stops being live. Null exactly when <see cref="ClaimIdentity"/> is.</summary>
		public DateTimeOffset? ClaimExpiresAt;

		public ErasureRequestStatus Status
		{
			get => (ErasureRequestStatus)Volatile.Read(ref StatusValue);
			set => Volatile.Write(ref StatusValue, (int)value);
		}
		public int? KeysDeleted { get; set; }


		// Generations this request staged for destruction, by handle. Held here rather than in the ledger
		// because between the stage and the destruction a staged generation names LIVE material -- nothing that
		// resolves the read predicate may be able to reach it.
		public ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> StagedDestructions { get; } =
			new(StringComparer.Ordinal);

		public int? RecordsAffected { get; set; }
		public Guid? CertificateId { get; set; }
		public string? ErrorMessage { get; set; }
		public DateTimeOffset CreatedAt { get; init; }
		public DateTimeOffset UpdatedAt { get; set; }
	}
}
