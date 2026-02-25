// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// In-memory implementation of <see cref="IErasureStore"/> for development and testing.
/// </summary>
/// <remarks>
/// This implementation stores all data in memory and is NOT suitable for production use.
/// Data is lost when the application restarts.
/// </remarks>
public sealed class InMemoryErasureStore : IErasureStore, IErasureCertificateStore, IErasureQueryStore
{
	private readonly ConcurrentDictionary<Guid, ErasureRequestData> _requests = new();
	private readonly ConcurrentDictionary<Guid, ErasureCertificate> _certificates = new();
	private readonly ConcurrentDictionary<Guid, Guid> _requestToCertificate = new();

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
		var data = new ErasureRequestData
		{
			RequestId = request.RequestId,
			DataSubjectIdHash = HashDataSubjectId(request.DataSubjectId),
			IdType = request.IdType,
			TenantId = request.TenantId,
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
		if (!_requests.TryGetValue(requestId, out var data))
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
		if (!_requests.TryGetValue(requestId, out var data))
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
		if (!_requests.TryGetValue(requestId, out var data))
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
		if (!_requests.TryGetValue(requestId, out var data))
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
		if (!_requestToCertificate.TryGetValue(requestId, out var certId))
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
		_ = _certificates.TryGetValue(certificateId, out var cert);
		return Task.FromResult(cert);
	}

	/// <inheritdoc />
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

	private static string HashDataSubjectId(string dataSubjectId) =>
		DataSubjectHasher.HashDataSubjectId(dataSubjectId);

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
