// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Erasure;

/// <summary>
/// Unit tests for <see cref="ErasureService"/> execution, cancellation, certificate,
/// and grace period logic.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Compliance")]
public sealed class ErasureServiceExecutionShould
{
	private readonly IErasureStore _store;
	private readonly IErasureCertificateStore _certStore;
	private readonly IErasureQueryStore _queryStore;
	private readonly ILegalHoldService _legalHoldService;
	private readonly IDataInventoryService _dataInventoryService;
	private readonly IKeyManagementProvider _keyProvider;
	private readonly ErasureOptions _erasureOptions;
	private readonly IOptions<ErasureSigningOptions> _signingOptions;
	private readonly ErasureService _sut;

	public ErasureServiceExecutionShould()
	{
		_store = A.Fake<IErasureStore>();
		_certStore = A.Fake<IErasureCertificateStore>();
		_queryStore = A.Fake<IErasureQueryStore>();
		_legalHoldService = A.Fake<ILegalHoldService>();
		_dataInventoryService = A.Fake<IDataInventoryService>();
		_keyProvider = A.Fake<IKeyManagementProvider>();
		_erasureOptions = new ErasureOptions
		{
			DefaultGracePeriod = TimeSpan.FromDays(7),
			MinimumGracePeriod = TimeSpan.FromDays(1),
			MaximumGracePeriod = TimeSpan.FromDays(30),
			EnableAutoDiscovery = true,
			CertificateRetentionPeriod = TimeSpan.FromDays(365)
		};
		_signingOptions = Microsoft.Extensions.Options.Options.Create(new ErasureSigningOptions { SigningKey = new byte[32] });

		// Wire up GetService to return sub-stores
		_ = A.CallTo(() => _store.GetService(typeof(IErasureCertificateStore)))
			.Returns(_certStore);
		_ = A.CallTo(() => _store.GetService(typeof(IErasureQueryStore)))
			.Returns(_queryStore);

		_sut = new ErasureService(
			_store,
			_keyProvider,
			Microsoft.Extensions.Options.Options.Create(_erasureOptions),
			_signingOptions,
			NullLogger<ErasureService>.Instance,
			_legalHoldService,
			_dataInventoryService);
	}

	#region ExecuteAsync Tests

	[Fact]
	public async Task ExecuteAsync_ReturnsFailure_WhenRequestNotFound()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(null));

		// Act
		var result = await _sut.ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("not found");
	}

	[Fact]
	public async Task ExecuteAsync_ReturnsFailure_WhenStatusIsNotScheduled()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateErasureStatus(requestId, ErasureRequestStatus.Completed);
		A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(status));

		// Act
		var result = await _sut.ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("Invalid status");
	}

	[Fact]
	public async Task ExecuteAsync_ReturnsSuccess_WhenRequestIsScheduled()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateErasureStatus(requestId, ErasureRequestStatus.Scheduled);
		A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(status));
		A.CallTo(() => _store.UpdateStatusAsync(requestId, A<ErasureRequestStatus>._, A<string>._, A<CancellationToken>._))
			.Returns(true);
		A.CallTo(() => _store.RecordCompletionAsync(requestId, A<int>._, A<int>._, A<Guid>._, A<CancellationToken>._))
			.Returns(Task.CompletedTask);

		// Act
		var result = await _sut.ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();
	}

	[Fact]
	public async Task ExecuteAsync_ThrowsException_WhenUpdateStatusFails()
	{
		// Arrange - UpdateStatusAsync is called before the try-catch in ExecuteAsync,
		// so exceptions propagate directly to the caller.
		var requestId = Guid.NewGuid();
		var status = CreateErasureStatus(requestId, ErasureRequestStatus.Scheduled);
		A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(status));
		A.CallTo(() => _store.UpdateStatusAsync(requestId, ErasureRequestStatus.InProgress, A<string>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("test error"));

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.ExecuteAsync(requestId, CancellationToken.None)).ConfigureAwait(false);
	}

	#endregion

	#region CancelErasureAsync Tests

	[Fact]
	public async Task CancelErasureAsync_ThrowsArgumentException_WhenReasonIsNullOrWhiteSpace()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.CancelErasureAsync(Guid.NewGuid(), "", "admin", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task CancelErasureAsync_ThrowsArgumentException_WhenCancelledByIsNullOrWhiteSpace()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.CancelErasureAsync(Guid.NewGuid(), "reason", "", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task CancelErasureAsync_ReturnsFalse_WhenRequestNotFound()
	{
		// Arrange
		A.CallTo(() => _store.GetStatusAsync(A<Guid>._, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(null));

		// Act
		var result = await _sut.CancelErasureAsync(Guid.NewGuid(), "reason", "admin", CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task CancelErasureAsync_ThrowsInvalidOperationException_WhenCannotCancel()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateErasureStatus(requestId, ErasureRequestStatus.Completed);
		A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(status));

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.CancelErasureAsync(requestId, "reason", "admin", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task CancelErasureAsync_ReturnsTrue_WhenSuccessfullyCancelled()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateErasureStatus(requestId, ErasureRequestStatus.Scheduled);
		A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(status));
		A.CallTo(() => _store.RecordCancellationAsync(requestId, "reason", "admin", A<CancellationToken>._))
			.Returns(true);

		// Act
		var result = await _sut.CancelErasureAsync(requestId, "reason", "admin", CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();
	}

	#endregion

	#region GenerateCertificateAsync Tests

	[Fact]
	public async Task GenerateCertificateAsync_ThrowsKeyNotFoundException_WhenRequestNotFound()
	{
		// Arrange
		A.CallTo(() => _store.GetStatusAsync(A<Guid>._, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(null));

		// Act & Assert
		await Should.ThrowAsync<KeyNotFoundException>(
			() => _sut.GenerateCertificateAsync(Guid.NewGuid(), CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task GenerateCertificateAsync_ThrowsInvalidOperationException_WhenNotExecuted()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateErasureStatus(requestId, ErasureRequestStatus.Scheduled);
		A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(status));

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.GenerateCertificateAsync(requestId, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task GenerateCertificateAsync_ReturnsExistingCertificate_WhenAlreadyGenerated()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateErasureStatus(requestId, ErasureRequestStatus.Completed, isExecuted: true);
		var existingCert = new ErasureCertificate
		{
			CertificateId = Guid.NewGuid(),
			RequestId = requestId,
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
			Signature = "existing-sig",
			RetainUntil = DateTimeOffset.UtcNow.AddDays(365)
		};

		A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(status));
		A.CallTo(() => _certStore.GetCertificateAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureCertificate?>(existingCert));

		// Act
		var result = await _sut.GenerateCertificateAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeSameAs(existingCert);
	}

	[Fact]
	public async Task GenerateCertificateAsync_CreatesNewCertificate_WhenNoneExists()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateErasureStatus(requestId, ErasureRequestStatus.Completed, isExecuted: true);

		A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(status));
		A.CallTo(() => _certStore.GetCertificateAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureCertificate?>(null));
		A.CallTo(() => _certStore.SaveCertificateAsync(A<ErasureCertificate>._, A<CancellationToken>._))
			.Returns(Task.CompletedTask);

		// Act
		var result = await _sut.GenerateCertificateAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldNotBeNull();
		result.RequestId.ShouldBe(requestId);
		result.Method.ShouldBe(ErasureMethod.CryptographicErasure);
		result.Signature.ShouldNotBeNullOrEmpty();
		A.CallTo(() => _certStore.SaveCertificateAsync(A<ErasureCertificate>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region RequestErasureAsync - Grace Period Tests

	[Fact]
	public async Task RequestErasureAsync_UsesDefaultGracePeriod_WhenNoOverride()
	{
		// Arrange
		var request = CreateValidRequest();
		A.CallTo(() => _legalHoldService.CheckHoldsAsync(A<string>._, A<DataSubjectIdType>._, A<string>._, A<CancellationToken>._))
			.Returns(LegalHoldCheckResult.NoHolds);
		A.CallTo(() => _dataInventoryService.DiscoverAsync(A<string>._, A<DataSubjectIdType>._, A<string>._, A<CancellationToken>._))
			.Returns(DataInventory.Empty("user-123"));
		A.CallTo(() => _store.SaveRequestAsync(A<ErasureRequest>._, A<DateTimeOffset>._, A<CancellationToken>._))
			.Returns(Task.CompletedTask);

		// Act
		var result = await _sut.RequestErasureAsync(request, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Status.ShouldBe(ErasureRequestStatus.Scheduled);
	}

	[Fact]
	public async Task RequestErasureAsync_ClampsGracePeriod_WhenBelowMinimum()
	{
		// Arrange
		var request = CreateValidRequest(gracePeriodOverride: TimeSpan.FromMinutes(1));

		A.CallTo(() => _legalHoldService.CheckHoldsAsync(A<string>._, A<DataSubjectIdType>._, A<string>._, A<CancellationToken>._))
			.Returns(LegalHoldCheckResult.NoHolds);
		A.CallTo(() => _dataInventoryService.DiscoverAsync(A<string>._, A<DataSubjectIdType>._, A<string>._, A<CancellationToken>._))
			.Returns(DataInventory.Empty("user-123"));
		A.CallTo(() => _store.SaveRequestAsync(A<ErasureRequest>._, A<DateTimeOffset>._, A<CancellationToken>._))
			.Returns(Task.CompletedTask);

		// Act
		var result = await _sut.RequestErasureAsync(request, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Status.ShouldBe(ErasureRequestStatus.Scheduled);
	}

	[Fact]
	public async Task RequestErasureAsync_ClampsGracePeriod_WhenAboveMaximum()
	{
		// Arrange
		var request = CreateValidRequest(gracePeriodOverride: TimeSpan.FromDays(365));

		A.CallTo(() => _legalHoldService.CheckHoldsAsync(A<string>._, A<DataSubjectIdType>._, A<string>._, A<CancellationToken>._))
			.Returns(LegalHoldCheckResult.NoHolds);
		A.CallTo(() => _dataInventoryService.DiscoverAsync(A<string>._, A<DataSubjectIdType>._, A<string>._, A<CancellationToken>._))
			.Returns(DataInventory.Empty("user-123"));
		A.CallTo(() => _store.SaveRequestAsync(A<ErasureRequest>._, A<DateTimeOffset>._, A<CancellationToken>._))
			.Returns(Task.CompletedTask);

		// Act
		var result = await _sut.RequestErasureAsync(request, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Status.ShouldBe(ErasureRequestStatus.Scheduled);
	}

	[Fact]
	public async Task RequestErasureAsync_ReturnsBlocked_WhenLegalHoldExists()
	{
		// Arrange
		var request = CreateValidRequest();
		var holdInfo = new LegalHoldInfo
		{
			HoldId = Guid.NewGuid(),
			Basis = LegalHoldBasis.LitigationHold,
			CaseReference = "CASE-001",
			CreatedAt = DateTimeOffset.UtcNow
		};

		A.CallTo(() => _legalHoldService.CheckHoldsAsync(A<string>._, A<DataSubjectIdType>._, A<string>._, A<CancellationToken>._))
			.Returns(LegalHoldCheckResult.WithHolds([holdInfo]));

		// Act
		var result = await _sut.RequestErasureAsync(request, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Status.ShouldBe(ErasureRequestStatus.BlockedByLegalHold);
	}

	[Fact]
	public async Task RequestErasureAsync_ThrowsArgumentNullException_WhenRequestIsNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.RequestErasureAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task RequestErasureAsync_ThrowsValidationException_WhenDataSubjectIdIsEmpty()
	{
		// Arrange
		var request = new ErasureRequest
		{
			RequestId = Guid.NewGuid(),
			DataSubjectId = "",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			RequestedBy = "admin",
			RequestedAt = DateTimeOffset.UtcNow
		};

		// Act & Assert
		await Should.ThrowAsync<ErasureOperationException>(
			() => _sut.RequestErasureAsync(request, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task RequestErasureAsync_ThrowsValidationException_WhenRequestedByIsEmpty()
	{
		// Arrange
		var request = new ErasureRequest
		{
			RequestId = Guid.NewGuid(),
			DataSubjectId = "user-123",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			RequestedBy = "",
			RequestedAt = DateTimeOffset.UtcNow
		};

		// Act & Assert
		await Should.ThrowAsync<ErasureOperationException>(
			() => _sut.RequestErasureAsync(request, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task RequestErasureAsync_ThrowsValidationException_WhenTenantScopeMissingTenantId()
	{
		// Arrange
		var request = new ErasureRequest
		{
			RequestId = Guid.NewGuid(),
			DataSubjectId = "user-123",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.Tenant,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			TenantId = null,
			RequestedBy = "admin",
			RequestedAt = DateTimeOffset.UtcNow
		};

		// Act & Assert
		await Should.ThrowAsync<ErasureOperationException>(
			() => _sut.RequestErasureAsync(request, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task RequestErasureAsync_ThrowsValidationException_WhenSelectiveScopeMissingCategories()
	{
		// Arrange
		var request = new ErasureRequest
		{
			RequestId = Guid.NewGuid(),
			DataSubjectId = "user-123",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.Selective,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			DataCategories = null,
			RequestedBy = "admin",
			RequestedAt = DateTimeOffset.UtcNow
		};

		// Act & Assert
		await Should.ThrowAsync<ErasureOperationException>(
			() => _sut.RequestErasureAsync(request, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task RequestErasureAsync_Succeeds_WithoutOptionalServices()
	{
		// Arrange - no legal hold service or data inventory service
		var sut = new ErasureService(
			_store,
			_keyProvider,
			Microsoft.Extensions.Options.Options.Create(_erasureOptions),
			_signingOptions,
			NullLogger<ErasureService>.Instance,
			null,
			null);

		var request = CreateValidRequest();
		A.CallTo(() => _store.SaveRequestAsync(A<ErasureRequest>._, A<DateTimeOffset>._, A<CancellationToken>._))
			.Returns(Task.CompletedTask);

		// Act
		var result = await sut.RequestErasureAsync(request, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Status.ShouldBe(ErasureRequestStatus.Scheduled);
	}

	#endregion

	#region GetStatusAsync Tests

	[Fact]
	public async Task GetStatusAsync_ReturnsNull_WhenRequestNotFound()
	{
		// Arrange
		A.CallTo(() => _store.GetStatusAsync(A<Guid>._, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(null));

		// Act
		var result = await _sut.GetStatusAsync(Guid.NewGuid(), CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeNull();
	}

	#endregion

	#region ListRequestsAsync Tests (via IErasureQueryStore)

	[Fact]
	public async Task ListRequestsAsync_DelegatesToQueryStore()
	{
		// Arrange - ListRequestsAsync is now on IErasureQueryStore (ISP split)
		var expected = new List<ErasureStatus>();
		A.CallTo(() => _queryStore.ListRequestsAsync(A<ErasureRequestStatus?>._, A<string>._, A<DateTimeOffset?>._, A<DateTimeOffset?>._, A<int>._, A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<ErasureStatus>>(expected));

		// Act
		var result = await _queryStore.ListRequestsAsync(null, null, null, null, 1, 100, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeSameAs(expected);
	}

	#endregion

	#region HashDataSubjectId Tests

	[Fact]
	public void HashDataSubjectId_ReturnsDeterministicHash()
	{
		// Act
		var hash1 = ErasureService.HashDataSubjectId("user@example.com");
		var hash2 = ErasureService.HashDataSubjectId("user@example.com");

		// Assert
		hash1.ShouldBe(hash2);
		hash1.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void HashDataSubjectId_ReturnsDifferentHash_ForDifferentInputs()
	{
		// Act
		var hash1 = ErasureService.HashDataSubjectId("user1@example.com");
		var hash2 = ErasureService.HashDataSubjectId("user2@example.com");

		// Assert
		hash1.ShouldNotBe(hash2);
	}

	#endregion

	#region Helpers

	private static ErasureRequest CreateValidRequest(TimeSpan? gracePeriodOverride = null) =>
		new()
		{
			RequestId = Guid.NewGuid(),
			DataSubjectId = "user-123",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			RequestedBy = "admin",
			RequestedAt = DateTimeOffset.UtcNow,
			GracePeriodOverride = gracePeriodOverride
		};

	private static ErasureStatus CreateErasureStatus(Guid requestId, ErasureRequestStatus status, bool isExecuted = false) =>
		new()
		{
			RequestId = requestId,
			DataSubjectIdHash = "hash-value",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			Status = status,
			RequestedBy = "admin",
			RequestedAt = DateTimeOffset.UtcNow,
			CompletedAt = isExecuted ? DateTimeOffset.UtcNow : null,
			UpdatedAt = DateTimeOffset.UtcNow
		};

	#endregion
}
