// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
internal sealed class InMemoryErasureStore : IErasureStore, IErasureCertificateStore, IErasureQueryStore
{
	private readonly ConcurrentDictionary<Guid, ErasureRequestData> _requests = new();
	private readonly ConcurrentDictionary<Guid, ErasureCertificate> _certificates = new();
	private readonly ConcurrentDictionary<Guid, Guid> _requestToCertificate = new();
	private readonly IDataSubjectHasher _dataSubjectHasher;
	private readonly ITenantContext? _tenantContext;
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
	/// Deployment mode decides the shape. A deployment that has not opted into multi-tenancy resolves
	/// <see cref="TenantScope.None"/>: no filter is applied, and rows keep whatever tenant value the caller
	/// supplied — byte-identical to the single-tenant behaviour, so no stored row becomes unreachable. A
	/// multi-tenant deployment resolves a scoped term that rides every tenant-facing path. Mode is "did the
	/// consumer opt in", read from <see cref="TenantContextOptions.RequireTenant"/>, and deliberately not "is
	/// an <see cref="ITenantContext"/> present" — the framework always registers a single-tenant default, so
	/// presence would make every deployment look multi-tenant.
	/// </para>
	/// <para>
	/// Multi-tenancy active with no resolved tenant fails closed: it throws rather than reaching an unfiltered
	/// read. A missing context is the same failure and is stated as such, because degrading it to
	/// <see cref="TenantScope.None"/> would apply no filter at all — the exact cross-tenant read this property
	/// exists to remove.
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
	/// Initializes a new instance of the <see cref="InMemoryErasureStore"/> class.
	/// </summary>
	/// <param name="dataSubjectHasher">The keyed hasher used to pseudonymize data-subject identifiers.</param>
	/// <param name="tenantContext">
	/// Ambient tenant context. Under multi-tenancy every tenant-facing operation matches on the resolved
	/// tenant, and the write path stamps it rather than the value on the incoming request, so one tenant
	/// cannot file a request into another tenant's partition. The estate-wide background surfaces
	/// (<c>GetScheduledRequestsAsync</c>, <c>CleanupExpiredCertificatesAsync</c>) are deliberately unscoped
	/// and documented as such at their call sites. Omitting it — the default — is the single-tenant
	/// deployment shape, in which the store resolves <see cref="TenantScope.None"/> and applies no filter.
	/// </param>
	/// <param name="tenantContextOptions">
	/// The tenant-context options. Its <see cref="TenantContextOptions.RequireTenant"/> (set by
	/// <c>AddMultiTenancy()</c>) selects the deployment mode.
	/// </param>
	public InMemoryErasureStore(
		IDataSubjectHasher dataSubjectHasher,
		ITenantContext? tenantContext = null,
		IOptions<TenantContextOptions>? tenantContextOptions = null)
	{
		_dataSubjectHasher = dataSubjectHasher ?? throw new ArgumentNullException(nameof(dataSubjectHasher));
		_tenantContext = tenantContext;
		_requireTenant = tenantContextOptions?.Value.RequireTenant ?? false;
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
		// The ambient term is authoritative on the write. Stamping the request's own TenantId would let a
		// caller file a request into another tenant's partition — and, because every scoped read matches on
		// the ambient term, that row would then be readable only by the tenant it was planted on.
		var tenant = AmbientScope;

		var data = new ErasureRequestData
		{
			RequestId = request.RequestId,
			DataSubjectIdHash = HashDataSubjectId(request.DataSubjectId),
			IdType = request.IdType,
			TenantId = tenant.IsScoped ? tenant.TenantId : request.TenantId,
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
			throw new InvalidOperationException($"Request {request.RequestId} already exists");
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
		if (!_requests.TryGetValue(requestId, out var data) || !MatchesAmbientTenant(tenant, data.TenantId))
		{
			throw new KeyNotFoundException($"Request {requestId} not found");
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
		if (!_certificates.TryAdd(certificate.CertificateId, certificate))
		{
			throw new InvalidOperationException($"Certificate {certificate.CertificateId} already exists");
		}

		_requestToCertificate[certificate.RequestId] = certificate.CertificateId;

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
			|| !OwningRequestMatchesAmbientTenant(tenant, cert.RequestId))
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
			.Where(c => c.RetainUntil < now)
			.Select(c => c.CertificateId)
			.ToList();

		var count = 0;
		foreach (var id in expired)
		{
			if (_certificates.TryRemove(id, out var cert))
			{
				_ = _requestToCertificate.TryRemove(cert.RequestId, out _);
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
	}

	private static ErasureStatus ToStatus(ErasureRequestData data) =>
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
	private static bool MatchesAmbientTenant(TenantScope tenant, string? rowTenantId) =>
		!tenant.IsScoped || string.Equals(rowTenantId, tenant.TenantId, StringComparison.Ordinal);

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
		if (!tenant.IsScoped)
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
		public int? RecordsAffected { get; set; }
		public Guid? CertificateId { get; set; }
		public string? ErrorMessage { get; set; }
		public DateTimeOffset CreatedAt { get; init; }
		public DateTimeOffset UpdatedAt { get; set; }
	}
}
