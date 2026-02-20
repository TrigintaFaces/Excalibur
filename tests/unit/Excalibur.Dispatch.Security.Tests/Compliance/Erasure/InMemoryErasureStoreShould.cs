// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Erasure;

/// <summary>
/// Unit tests for <see cref="InMemoryErasureStore"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Compliance")]
public sealed class InMemoryErasureStoreShould
{
	private readonly InMemoryErasureStore _sut = new();

	#region SaveRequestAsync Tests

	[Fact]
	public async Task SaveRequestAsync_StoresRequest()
	{
		// Arrange
		var request = CreateRequest();

		// Act
		await _sut.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None).ConfigureAwait(false);

		// Assert
		_sut.RequestCount.ShouldBe(1);
	}

	[Fact]
	public async Task SaveRequestAsync_ThrowsInvalidOperationException_WhenRequestAlreadyExists()
	{
		// Arrange
		var request = CreateRequest();
		await _sut.SaveRequestAsync(request, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.SaveRequestAsync(request, DateTimeOffset.UtcNow, CancellationToken.None)).ConfigureAwait(false);
	}

	#endregion

	#region GetStatusAsync Tests

	[Fact]
	public async Task GetStatusAsync_ReturnsNull_WhenNotFound()
	{
		var result = await _sut.GetStatusAsync(Guid.NewGuid(), CancellationToken.None).ConfigureAwait(false);
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetStatusAsync_ReturnsStatus_WhenRequestExists()
	{
		// Arrange
		var request = CreateRequest();
		await _sut.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.GetStatusAsync(request.RequestId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldNotBeNull();
		result.RequestId.ShouldBe(request.RequestId);
		result.Status.ShouldBe(ErasureRequestStatus.Scheduled);
	}

	#endregion

	#region UpdateStatusAsync Tests

	[Fact]
	public async Task UpdateStatusAsync_ReturnsFalse_WhenNotFound()
	{
		var result = await _sut.UpdateStatusAsync(Guid.NewGuid(), ErasureRequestStatus.InProgress, null, CancellationToken.None).ConfigureAwait(false);
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task UpdateStatusAsync_UpdatesStatus()
	{
		// Arrange
		var request = CreateRequest();
		await _sut.SaveRequestAsync(request, DateTimeOffset.UtcNow, cancellationToken: CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.UpdateStatusAsync(request.RequestId, ErasureRequestStatus.InProgress, null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();
		var status = await _sut.GetStatusAsync(request.RequestId, cancellationToken: CancellationToken.None).ConfigureAwait(false);
		status.Status.ShouldBe(ErasureRequestStatus.InProgress);
	}

	[Fact]
	public async Task UpdateStatusAsync_SetsExecutedAt_WhenStatusIsInProgress()
	{
		// Arrange
		var request = CreateRequest();
		await _sut.SaveRequestAsync(request, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);

		// Act
		await _sut.UpdateStatusAsync(request.RequestId, ErasureRequestStatus.InProgress, null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		var status = await _sut.GetStatusAsync(request.RequestId, cancellationToken: CancellationToken.None).ConfigureAwait(false);
		status.ExecutedAt.ShouldNotBeNull();
	}

	#endregion

	#region RecordCompletionAsync Tests

	[Fact]
	public async Task RecordCompletionAsync_ThrowsKeyNotFoundException_WhenNotFound()
	{
		await Should.ThrowAsync<KeyNotFoundException>(
			() => _sut.RecordCompletionAsync(Guid.NewGuid(), 5, 10, Guid.NewGuid(), CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task RecordCompletionAsync_UpdatesRequestWithCompletionData()
	{
		// Arrange
		var request = CreateRequest();
		await _sut.SaveRequestAsync(request, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);
		var certId = Guid.NewGuid();

		// Act
		await _sut.RecordCompletionAsync(request.RequestId, 3, 100, certId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		var status = await _sut.GetStatusAsync(request.RequestId, CancellationToken.None).ConfigureAwait(false);
		status.Status.ShouldBe(ErasureRequestStatus.Completed);
		status.KeysDeleted.ShouldBe(3);
		status.RecordsAffected.ShouldBe(100);
		status.CertificateId.ShouldBe(certId);
		status.CompletedAt.ShouldNotBeNull();
	}

	#endregion

	#region RecordCancellationAsync Tests

	[Fact]
	public async Task RecordCancellationAsync_ReturnsFalse_WhenNotFound()
	{
		var result = await _sut.RecordCancellationAsync(Guid.NewGuid(), "reason", "admin", CancellationToken.None).ConfigureAwait(false);
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task RecordCancellationAsync_ReturnsFalse_WhenStatusIsNotCancellable()
	{
		// Arrange
		var request = CreateRequest();
		await _sut.SaveRequestAsync(request, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);
		await _sut.RecordCompletionAsync(request.RequestId, 0, 0, Guid.NewGuid(), CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.RecordCancellationAsync(request.RequestId, "reason", "admin", CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task RecordCancellationAsync_ReturnsTrue_WhenScheduled()
	{
		// Arrange
		var request = CreateRequest();
		await _sut.SaveRequestAsync(request, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.RecordCancellationAsync(request.RequestId, "reason", "admin", CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();
		var status = await _sut.GetStatusAsync(request.RequestId, CancellationToken.None).ConfigureAwait(false);
		status.Status.ShouldBe(ErasureRequestStatus.Cancelled);
		status.CancellationReason.ShouldBe("reason");
		status.CancelledBy.ShouldBe("admin");
	}

	#endregion

	#region ListRequestsAsync Tests

	[Fact]
	public async Task ListRequestsAsync_ReturnsEmpty_WhenNoRequests()
	{
		var results = await _sut.ListRequestsAsync(null, null, null, null, 1, 100, CancellationToken.None).ConfigureAwait(false);
		results.ShouldBeEmpty();
	}

	[Fact]
	public async Task ListRequestsAsync_FiltersByStatus()
	{
		// Arrange
		var request1 = CreateRequest();
		var request2 = CreateRequest();
		await _sut.SaveRequestAsync(request1, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveRequestAsync(request2, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);
		await _sut.RecordCompletionAsync(request1.RequestId, 0, 0, Guid.NewGuid(), CancellationToken.None).ConfigureAwait(false);

		// Act
		var scheduled = await _sut.ListRequestsAsync(ErasureRequestStatus.Scheduled, null, null, null, 1, 100, CancellationToken.None).ConfigureAwait(false);
		var completed = await _sut.ListRequestsAsync(ErasureRequestStatus.Completed, null, null, null, 1, 100, CancellationToken.None).ConfigureAwait(false);

		// Assert
		scheduled.Count.ShouldBe(1);
		completed.Count.ShouldBe(1);
	}

	[Fact]
	public async Task ListRequestsAsync_FiltersByTenantId()
	{
		// Arrange
		var request1 = CreateRequest(tenantId: "tenant-a");
		var request2 = CreateRequest(tenantId: "tenant-b");
		await _sut.SaveRequestAsync(request1, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveRequestAsync(request2, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);

		// Act
		var results = await _sut.ListRequestsAsync(null, "tenant-a", null, null, 1, 100, CancellationToken.None).ConfigureAwait(false);

		// Assert
		results.Count.ShouldBe(1);
		results[0].TenantId.ShouldBe("tenant-a");
	}

	#endregion

	#region Certificate Tests

	[Fact]
	public async Task SaveCertificateAsync_ThrowsInvalidOperationException_WhenAlreadyExists()
	{
		// Arrange
		var cert = CreateCertificate();
		await _sut.SaveCertificateAsync(cert, CancellationToken.None).ConfigureAwait(false);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.SaveCertificateAsync(cert, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task GetCertificateAsync_ReturnsNull_WhenNotFound()
	{
		var result = await _sut.GetCertificateAsync(Guid.NewGuid(), CancellationToken.None).ConfigureAwait(false);
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetCertificateAsync_ReturnsCertificate_WhenMappingExists()
	{
		// Arrange
		var cert = CreateCertificate();
		await _sut.SaveCertificateAsync(cert, CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.GetCertificateAsync(cert.RequestId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldNotBeNull();
		result.CertificateId.ShouldBe(cert.CertificateId);
	}

	[Fact]
	public async Task GetCertificateByIdAsync_ReturnsNull_WhenNotFound()
	{
		var result = await _sut.GetCertificateByIdAsync(Guid.NewGuid(), CancellationToken.None).ConfigureAwait(false);
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetCertificateByIdAsync_ReturnsCertificate()
	{
		// Arrange
		var cert = CreateCertificate();
		await _sut.SaveCertificateAsync(cert, CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.GetCertificateByIdAsync(cert.CertificateId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldNotBeNull();
		result.CertificateId.ShouldBe(cert.CertificateId);
	}

	#endregion

	#region CleanupExpiredCertificatesAsync Tests

	[Fact]
	public async Task CleanupExpiredCertificatesAsync_RemovesExpiredCertificates()
	{
		// Arrange
		var expiredCert = CreateCertificate(retainUntil: DateTimeOffset.UtcNow.AddDays(-1));
		var validCert = CreateCertificate(retainUntil: DateTimeOffset.UtcNow.AddDays(30));
		await _sut.SaveCertificateAsync(expiredCert, CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveCertificateAsync(validCert, CancellationToken.None).ConfigureAwait(false);

		// Act
		var count = await _sut.CleanupExpiredCertificatesAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		count.ShouldBe(1);
		_sut.CertificateCount.ShouldBe(1);
	}

	#endregion

	#region GetScheduledRequestsAsync Tests

	[Fact]
	public async Task GetScheduledRequestsAsync_ReturnsOnlyDueScheduledRequests()
	{
		// Arrange
		var request1 = CreateRequest();
		var request2 = CreateRequest();
		await _sut.SaveRequestAsync(request1, DateTimeOffset.UtcNow.AddDays(-1), CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveRequestAsync(request2, DateTimeOffset.UtcNow.AddDays(1), CancellationToken.None).ConfigureAwait(false);

		// Act
		var results = await _sut.GetScheduledRequestsAsync(100, CancellationToken.None).ConfigureAwait(false);

		// Assert
		results.Count.ShouldBe(1);
		results[0].RequestId.ShouldBe(request1.RequestId);
	}

	#endregion

	#region Clear Tests

	[Fact]
	public async Task Clear_RemovesAllData()
	{
		// Arrange
		var request = CreateRequest();
		await _sut.SaveRequestAsync(request, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveCertificateAsync(CreateCertificate(), CancellationToken.None).ConfigureAwait(false);

		// Act
		_sut.Clear();

		// Assert
		_sut.RequestCount.ShouldBe(0);
		_sut.CertificateCount.ShouldBe(0);
	}

	#endregion

	#region Helpers

	private static ErasureRequest CreateRequest(string? tenantId = null) =>
		new()
		{
			RequestId = Guid.NewGuid(),
			DataSubjectId = $"user-{Guid.NewGuid():N}",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			TenantId = tenantId,
			RequestedBy = "admin",
			RequestedAt = DateTimeOffset.UtcNow
		};

	private static ErasureCertificate CreateCertificate(DateTimeOffset? retainUntil = null) =>
		new()
		{
			CertificateId = Guid.NewGuid(),
			RequestId = Guid.NewGuid(),
			DataSubjectReference = "hash-value",
			RequestReceivedAt = DateTimeOffset.UtcNow.AddDays(-1),
			CompletedAt = DateTimeOffset.UtcNow,
			Method = ErasureMethod.CryptographicErasure,
			Summary = new ErasureSummary(),
			Verification = new VerificationSummary
			{
				Verified = true,
				Methods = VerificationMethod.KeyManagementSystem,
				VerifiedAt = DateTimeOffset.UtcNow
			},
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			Signature = "test-sig",
			RetainUntil = retainUntil ?? DateTimeOffset.UtcNow.AddDays(365)
		};

	#endregion
}
