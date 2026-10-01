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
	: IErasureStore, IErasureCertificateStore, IErasureQueryStore, IKeyDestructionLedger
{
	private readonly ConcurrentDictionary<Guid, ErasureRequestData> _requests = new();

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
	private readonly ConcurrentDictionary<string, DestroyedGenerationRecord> _destroyedGenerations =
		new(StringComparer.Ordinal);

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
	/// The tenant-context options. Its <see cref="TenantContextOptions.RequireTenant"/> (set by
	/// <c>AddMultiTenancy()</c>) selects the deployment mode.
	/// </param>
	public InMemoryErasureStore(
		IDataSubjectHasher dataSubjectHasher,
		ITenantContext tenantContext,
		IOptions<TenantContextOptions> tenantContextOptions)
	{
		_dataSubjectHasher = dataSubjectHasher ?? throw new ArgumentNullException(nameof(dataSubjectHasher));
		_tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
		ArgumentNullException.ThrowIfNull(tenantContextOptions);
		_requireTenant = tenantContextOptions.Value.RequireTenant;
	}

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

		// The ambient term is authoritative on the write. Stamping the request's own TenantId would let a
		// caller file a request into another tenant's partition — and, because every scoped read matches on
		// the ambient term, that row would then be readable only by the tenant it was planted on.
		var tenant = AmbientScope;

		var data = new ErasureRequestData
		{
			RequestId = request.RequestId,
			DataSubjectIdHash = HashDataSubjectId(request.DataSubjectId),
			IdType = request.IdType,
			TenantId = KeyedTenantPartition.FromStoredValue(
				_requireTenant ? tenant.TenantId : request.TenantId).TenantId,
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
	public Task<bool> UpdateStatusAsync(
		Guid requestId,
		ErasureRequestStatus status,
		string? errorMessage,
		CancellationToken cancellationToken)
	{
		// Resolved before the lookup so that an unresolved tenant fails closed whether or not the row exists.
		var tenant = AmbientScope;

		// Another tenant's row is treated as absent, so a cross-tenant update reports the same "no such
		// request" result as a missing one rather than mutating a row the caller cannot see.
		if (!_requests.TryGetValue(requestId, out var data) || !MatchesAmbientTenant(tenant, data.TenantId))
		{
			return Task.FromResult(false);
		}

		// Atomic compare-and-swap for InProgress transition to prevent TOCTOU
		if (status == ErasureRequestStatus.InProgress)
		{
			var previous = Interlocked.CompareExchange(ref data.StatusValue, (int)ErasureRequestStatus.InProgress, (int)ErasureRequestStatus.Scheduled);
			if (previous != (int)ErasureRequestStatus.Scheduled)
			{
				return Task.FromResult(false);
			}

			data.ExecutedAt = DateTimeOffset.UtcNow;
			data.ErrorMessage = errorMessage;
			data.UpdatedAt = DateTimeOffset.UtcNow;
			return Task.FromResult(true);
		}

		data.Status = status;
		data.ErrorMessage = errorMessage;
		data.UpdatedAt = DateTimeOffset.UtcNow;

		return Task.FromResult(true);
	}

	/// <inheritdoc />
	public Task RecordCompletionAsync(
		Guid requestId,
		int keysDeleted,
		int recordsAffected,
		Guid certificateId,
		CancellationToken cancellationToken)
	{
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

		data.Status = ErasureRequestStatus.Completed;
		data.KeysDeleted = keysDeleted;
		data.RecordsAffected = recordsAffected;
		data.CertificateId = certificateId;
		data.CompletedAt = DateTimeOffset.UtcNow;
		data.UpdatedAt = DateTimeOffset.UtcNow;

		_requestToCertificate[requestId] = certificateId;

		return Task.CompletedTask;
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

		// The ledger. Keyed on the generation alone: one generation is one destruction, so a second write for
		// the same generation is absorbed and the first instant stands. Two DIFFERENT generations destroyed at
		// one handle are two destructions and get two entries -- keying this on the handle would silently drop
		// the second.
		_ = _destroyedGenerations.TryAdd(
			keyGeneration,
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
	public Task RecordDestroyedGenerationAsync(string keyGeneration, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(keyGeneration);

		// NO request and NO handle, because the caller genuinely has neither. The provenance value records
		// that this entry rests on the caller's assertion rather than on anything this framework observed; it
		// is stated rather than inferred from the absent request.
		//
		// TryAdd, so re-asserting a generation already on file is a no-op and the FIRST instant stands.
		_ = _destroyedGenerations.TryAdd(
			keyGeneration,
			new DestroyedGenerationRecord(null, null, DateTimeOffset.UtcNow, RecordedBy.CallerAssertion));

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public ValueTask<bool> IsGenerationDestroyedAsync(string keyGeneration, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(keyGeneration);

		// NO tenant term, deliberately, and NOT an oversight. The lookup is addressed by the ledger's whole key,
		// so it already selects at most one entry; a tenant term on such a lookup cannot admit a foreign entry
		// and its only reachable effect is turning the correct entry into none. Here that effect is the
		// catastrophic one in reverse: a read of a genuinely erased subject would stop reporting their erasure
		// and start failing, permanently. A generation is minted by the key backend and is not derivable from a
		// subject, so one tenant cannot name another's.
		//
		// Staged intents are NOT consulted. A staged generation names material that may still be live.
		return new ValueTask<bool>(_destroyedGenerations.ContainsKey(keyGeneration));
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

	/// <summary>
	/// A ledger entry: this generation was destroyed at this instant, and this is who says so.
	/// </summary>
	/// <remarks>
	/// The request and the handle are nullable because a destruction asserted through the ledger's public write
	/// has neither. <paramref name="RecordedBy"/> states the provenance explicitly rather than leaving it to be
	/// inferred from those nulls.
	/// </remarks>
	private sealed record DestroyedGenerationRecord(
		Guid? RequestId,
		string? KeyHandle,
		DateTimeOffset DestroyedAt,
		string RecordedBy);

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

		// Atomic compare-and-swap: only cancel if currently Pending or Scheduled
		var previous = Interlocked.CompareExchange(
			ref data.StatusValue,
			(int)ErasureRequestStatus.Cancelled,
			(int)ErasureRequestStatus.Pending);

		if (previous != (int)ErasureRequestStatus.Pending)
		{
			// Try Scheduled → Cancelled
			previous = Interlocked.CompareExchange(
				ref data.StatusValue,
				(int)ErasureRequestStatus.Cancelled,
				(int)ErasureRequestStatus.Scheduled);

			if (previous != (int)ErasureRequestStatus.Scheduled)
			{
				return Task.FromResult(false);
			}
		}

		data.CancelledAt = DateTimeOffset.UtcNow;
		data.CancellationReason = reason;
		data.CancelledBy = cancelledBy;
		data.UpdatedAt = DateTimeOffset.UtcNow;

		return Task.FromResult(true);
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

		var scheduled = _requests.Values
			.Where(r => r.Status == ErasureRequestStatus.Scheduled &&
						r.ScheduledExecutionAt <= now)
			.OrderBy(r => r.ScheduledExecutionAt)
			.Take(maxResults)
			.Select(ToStatus)
			.ToList();

		return Task.FromResult<IReadOnlyList<ErasureStatus>>(scheduled);
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

		if (serviceType == typeof(IErasureQueryStore))
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
