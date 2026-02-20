// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Erasure;

/// <summary>
/// Unit tests for <see cref="ErasureService"/>.
/// Tests GDPR Article 17 erasure request processing per ADR-054.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class ErasureServiceShould
{
	private readonly IErasureStore _store;
	private readonly IErasureCertificateStore _certStore;
	private readonly IErasureQueryStore _queryStore;
	private readonly ILegalHoldService _legalHoldService;
	private readonly IDataInventoryService _dataInventoryService;
	private readonly IKeyManagementProvider _keyProvider;
	private readonly IOptions<ErasureOptions> _options;
	private readonly IOptions<ErasureSigningOptions> _signingOptions;
	private readonly ErasureService _sut;

	public ErasureServiceShould()
	{
		_store = A.Fake<IErasureStore>();
		_certStore = A.Fake<IErasureCertificateStore>();
		_queryStore = A.Fake<IErasureQueryStore>();
		_legalHoldService = A.Fake<ILegalHoldService>();
		_dataInventoryService = A.Fake<IDataInventoryService>();
		_keyProvider = A.Fake<IKeyManagementProvider>();
		_options = Microsoft.Extensions.Options.Options.Create(new ErasureOptions());
		_signingOptions = Microsoft.Extensions.Options.Options.Create(new ErasureSigningOptions { SigningKey = new byte[32] });

		// Wire up GetService to return sub-stores
		_ = A.CallTo(() => _store.GetService(typeof(IErasureCertificateStore)))
			.Returns(_certStore);
		_ = A.CallTo(() => _store.GetService(typeof(IErasureQueryStore)))
			.Returns(_queryStore);

		_sut = new ErasureService(
			_store,
			_keyProvider,
			_options,
			_signingOptions,
			NullLogger<ErasureService>.Instance,
			_legalHoldService,
			_dataInventoryService);
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenStoreIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new ErasureService(
			null!,
			_keyProvider,
			_options,
			_signingOptions,
			NullLogger<ErasureService>.Instance,
			null,
			null));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenKeyProviderIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new ErasureService(
			_store,
			null!,
			_options,
			_signingOptions,
			NullLogger<ErasureService>.Instance,
			null,
			null));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new ErasureService(
			_store,
			_keyProvider,
			null!,
			_signingOptions,
			NullLogger<ErasureService>.Instance,
			null,
			null));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new ErasureService(
			_store,
			_keyProvider,
			_options,
			_signingOptions,
			null!,
			null,
			null));
	}

	[Fact]
	public void Constructor_AcceptsNullLegalHoldService()
	{
		// Act & Assert - should not throw
		var service = new ErasureService(
			_store,
			_keyProvider,
			_options,
			_signingOptions,
			NullLogger<ErasureService>.Instance,
			legalHoldService: null,
			dataInventoryService: null);

		_ = service.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_AcceptsNullDataInventoryService()
	{
		// Act & Assert - should not throw
		var service = new ErasureService(
			_store,
			_keyProvider,
			_options,
			_signingOptions,
			NullLogger<ErasureService>.Instance,
			legalHoldService: null,
			dataInventoryService: null);

		_ = service.ShouldNotBeNull();
	}

	#endregion Constructor Tests

	#region RequestErasureAsync Tests

	[Fact]
	public async Task RequestErasureAsync_ThrowsArgumentNullException_WhenRequestIsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.RequestErasureAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task RequestErasureAsync_ThrowsValidationException_WhenDataSubjectIdIsEmpty()
	{
		// Arrange
		var request = new ErasureRequest
		{
			DataSubjectId = string.Empty,
			IdType = DataSubjectIdType.UserId,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			RequestedBy = "operator@test.com"
		};

		// Act & Assert
		_ = await Should.ThrowAsync<ErasureOperationException>(() =>
			_sut.RequestErasureAsync(request, CancellationToken.None));
	}

	[Fact]
	public async Task RequestErasureAsync_ThrowsValidationException_WhenRequestedByIsEmpty()
	{
		// Arrange
		var request = new ErasureRequest
		{
			DataSubjectId = "user-123",
			IdType = DataSubjectIdType.UserId,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			RequestedBy = string.Empty
		};

		// Act & Assert
		_ = await Should.ThrowAsync<ErasureOperationException>(() =>
			_sut.RequestErasureAsync(request, CancellationToken.None));
	}

	[Fact]
	public async Task RequestErasureAsync_ThrowsValidationException_WhenTenantScopeWithoutTenantId()
	{
		// Arrange
		var request = new ErasureRequest
		{
			DataSubjectId = "user-123",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.Tenant,
			TenantId = null,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			RequestedBy = "operator@test.com"
		};

		// Act & Assert
		_ = await Should.ThrowAsync<ErasureOperationException>(() =>
			_sut.RequestErasureAsync(request, CancellationToken.None));
	}

	[Fact]
	public async Task RequestErasureAsync_ThrowsValidationException_WhenSelectiveScopeWithoutCategories()
	{
		// Arrange
		var request = new ErasureRequest
		{
			DataSubjectId = "user-123",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.Selective,
			DataCategories = null,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			RequestedBy = "operator@test.com"
		};

		// Act & Assert
		_ = await Should.ThrowAsync<ErasureOperationException>(() =>
			_sut.RequestErasureAsync(request, CancellationToken.None));
	}

	[Fact]
	public async Task RequestErasureAsync_ReturnsScheduledResult_WhenValid()
	{
		// Arrange
		var request = CreateValidRequest();

		_ = A.CallTo(() => _legalHoldService.CheckHoldsAsync(
			A<string>._,
			A<DataSubjectIdType>._,
			A<string?>._,
			A<CancellationToken>._))
			.Returns(LegalHoldCheckResult.NoHolds);

		// Act
		var result = await _sut.RequestErasureAsync(request, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Status.ShouldBe(ErasureRequestStatus.Scheduled);
		result.RequestId.ShouldBe(request.RequestId);
		_ = result.ScheduledExecutionTime.ShouldNotBeNull();
	}

	[Fact]
	public async Task RequestErasureAsync_SavesRequest_WhenValid()
	{
		// Arrange
		var request = CreateValidRequest();

		_ = A.CallTo(() => _legalHoldService.CheckHoldsAsync(
			A<string>._,
			A<DataSubjectIdType>._,
			A<string?>._,
			A<CancellationToken>._))
			.Returns(LegalHoldCheckResult.NoHolds);

		// Act
		_ = await _sut.RequestErasureAsync(request, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _store.SaveRequestAsync(
			request,
			A<DateTimeOffset>._,
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
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

		_ = A.CallTo(() => _legalHoldService.CheckHoldsAsync(
			A<string>._,
			A<DataSubjectIdType>._,
			A<string?>._,
			A<CancellationToken>._))
			.Returns(LegalHoldCheckResult.WithHolds([holdInfo]));

		// Act
		var result = await _sut.RequestErasureAsync(request, CancellationToken.None);

		// Assert
		result.Status.ShouldBe(ErasureRequestStatus.BlockedByLegalHold);
		_ = result.BlockingHold.ShouldNotBeNull();
		result.BlockingHold.HoldId.ShouldBe(holdInfo.HoldId);
	}

	[Fact]
	public async Task RequestErasureAsync_DoesNotSaveRequest_WhenBlocked()
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

		_ = A.CallTo(() => _legalHoldService.CheckHoldsAsync(
			A<string>._,
			A<DataSubjectIdType>._,
			A<string?>._,
			A<CancellationToken>._))
			.Returns(LegalHoldCheckResult.WithHolds([holdInfo]));

		// Act
		_ = await _sut.RequestErasureAsync(request, CancellationToken.None);

		// Assert
		A.CallTo(() => _store.SaveRequestAsync(
			A<ErasureRequest>._,
			A<DateTimeOffset>._,
			A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task RequestErasureAsync_SkipsLegalHoldCheck_WhenServiceNotConfigured()
	{
		// Arrange
		var service = new ErasureService(
			_store,
			_keyProvider,
			_options,
			_signingOptions,
			NullLogger<ErasureService>.Instance,
			legalHoldService: null,
			dataInventoryService: null);

		var request = CreateValidRequest();

		// Act
		var result = await service.RequestErasureAsync(request, CancellationToken.None);

		// Assert
		result.Status.ShouldBe(ErasureRequestStatus.Scheduled);
	}

	[Fact]
	public async Task RequestErasureAsync_IncludesInventorySummary_WhenAutoDiscoveryEnabled()
	{
		// Arrange
		var request = CreateValidRequest();

		_ = A.CallTo(() => _legalHoldService.CheckHoldsAsync(
			A<string>._,
			A<DataSubjectIdType>._,
			A<string?>._,
			A<CancellationToken>._))
			.Returns(LegalHoldCheckResult.NoHolds);

		var inventory = new DataInventory
		{
			DataSubjectId = request.DataSubjectId,
			Locations =
			[
				new DataLocation
				{
					TableName = "Users",
					FieldName = "Email",
					DataCategory = "ContactInfo",
					RecordId = "1",
					KeyId = "key-1"
				}
			],
			AssociatedKeys =
			[
				new KeyReference { KeyId = "key-1", KeyScope = EncryptionKeyScope.User, RecordCount = 1 }
			]
		};

		_ = A.CallTo(() => _dataInventoryService.DiscoverAsync(
			A<string>._,
			A<DataSubjectIdType>._,
			A<string?>._,
			A<CancellationToken>._))
			.Returns(inventory);

		// Act
		var result = await _sut.RequestErasureAsync(request, CancellationToken.None);

		// Assert
		_ = result.InventorySummary.ShouldNotBeNull();
		result.InventorySummary.EncryptedFieldCount.ShouldBe(1);
		result.InventorySummary.KeyCount.ShouldBe(1);
	}

	[Fact]
	public async Task RequestErasureAsync_UsesDefaultGracePeriod_WhenNoOverride()
	{
		// Arrange
		var request = CreateValidRequest();
		var options = Microsoft.Extensions.Options.Options.Create(new ErasureOptions { DefaultGracePeriod = TimeSpan.FromHours(48) });
		var service = new ErasureService(
			_store,
			_keyProvider,
			options,
			_signingOptions,
			NullLogger<ErasureService>.Instance,
			null,
			null);

		DateTimeOffset? capturedScheduledTime = null;
		_ = A.CallTo(() => _store.SaveRequestAsync(
			A<ErasureRequest>._,
			A<DateTimeOffset>._,
			A<CancellationToken>._))
			.Invokes(call => capturedScheduledTime = call.GetArgument<DateTimeOffset>(1));

		// Act
		var result = await service.RequestErasureAsync(request, CancellationToken.None);

		// Assert
		_ = capturedScheduledTime.ShouldNotBeNull();
		var expectedTime = DateTimeOffset.UtcNow.AddHours(48);
		(capturedScheduledTime.Value - expectedTime).Duration().ShouldBeLessThan(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public async Task RequestErasureAsync_UsesOverrideGracePeriod_WhenProvided()
	{
		// Arrange
		var request = CreateValidRequest() with { GracePeriodOverride = TimeSpan.FromHours(24) };

		DateTimeOffset? capturedScheduledTime = null;
		_ = A.CallTo(() => _store.SaveRequestAsync(
			A<ErasureRequest>._,
			A<DateTimeOffset>._,
			A<CancellationToken>._))
			.Invokes(call => capturedScheduledTime = call.GetArgument<DateTimeOffset>(1));

		// Act
		_ = await _sut.RequestErasureAsync(request, CancellationToken.None);

		// Assert
		_ = capturedScheduledTime.ShouldNotBeNull();
		var expectedTime = DateTimeOffset.UtcNow.AddHours(24);
		(capturedScheduledTime.Value - expectedTime).Duration().ShouldBeLessThan(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public async Task RequestErasureAsync_ClampsGracePeriod_WhenBelowMinimum()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new ErasureOptions { MinimumGracePeriod = TimeSpan.FromHours(2) });
		var service = new ErasureService(
			_store,
			_keyProvider,
			options,
			_signingOptions,
			NullLogger<ErasureService>.Instance,
			null,
			null);

		var request = CreateValidRequest() with { GracePeriodOverride = TimeSpan.FromMinutes(30) };

		DateTimeOffset? capturedScheduledTime = null;
		_ = A.CallTo(() => _store.SaveRequestAsync(
			A<ErasureRequest>._,
			A<DateTimeOffset>._,
			A<CancellationToken>._))
			.Invokes(call => capturedScheduledTime = call.GetArgument<DateTimeOffset>(1));

		// Act
		_ = await service.RequestErasureAsync(request, CancellationToken.None);

		// Assert
		_ = capturedScheduledTime.ShouldNotBeNull();
		var expectedTime = DateTimeOffset.UtcNow.AddHours(2); // Clamped to minimum
		(capturedScheduledTime.Value - expectedTime).Duration().ShouldBeLessThan(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public async Task RequestErasureAsync_ClampsGracePeriod_WhenAboveMaximum()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new ErasureOptions { MaximumGracePeriod = TimeSpan.FromDays(7) });
		var service = new ErasureService(
			_store,
			_keyProvider,
			options,
			_signingOptions,
			NullLogger<ErasureService>.Instance,
			null,
			null);

		var request = CreateValidRequest() with { GracePeriodOverride = TimeSpan.FromDays(15) };

		DateTimeOffset? capturedScheduledTime = null;
		_ = A.CallTo(() => _store.SaveRequestAsync(
			A<ErasureRequest>._,
			A<DateTimeOffset>._,
			A<CancellationToken>._))
			.Invokes(call => capturedScheduledTime = call.GetArgument<DateTimeOffset>(1));

		// Act
		_ = await service.RequestErasureAsync(request, CancellationToken.None);

		// Assert
		_ = capturedScheduledTime.ShouldNotBeNull();
		var expectedTime = DateTimeOffset.UtcNow.AddDays(7); // Clamped to maximum
		(capturedScheduledTime.Value - expectedTime).Duration().ShouldBeLessThan(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public async Task RequestErasureAsync_ThrowsException_WhenStoreThrows()
	{
		// Arrange
		var request = CreateValidRequest();

		_ = A.CallTo(() => _store.SaveRequestAsync(
			A<ErasureRequest>._,
			A<DateTimeOffset>._,
			A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Store error"));

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.RequestErasureAsync(request, CancellationToken.None));
	}

	#endregion RequestErasureAsync Tests

	#region GetStatusAsync Tests

	[Fact]
	public async Task GetStatusAsync_ReturnsNull_WhenNotFound()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		_ = A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns((ErasureStatus?)null);

		// Act
		var result = await _sut.GetStatusAsync(requestId, CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetStatusAsync_ReturnsStatus_WhenFound()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = new ErasureStatus
		{
			RequestId = requestId,
			Status = ErasureRequestStatus.Scheduled,
			DataSubjectIdHash = "hash",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			RequestedAt = DateTimeOffset.UtcNow,
			RequestedBy = "operator",
			UpdatedAt = DateTimeOffset.UtcNow
		};

		_ = A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);

		// Act
		var result = await _sut.GetStatusAsync(requestId, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.RequestId.ShouldBe(requestId);
		result.Status.ShouldBe(ErasureRequestStatus.Scheduled);
	}

	#endregion GetStatusAsync Tests

	#region CancelErasureAsync Tests

	[Fact]
	public async Task CancelErasureAsync_ThrowsArgumentException_WhenReasonIsEmpty()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.CancelErasureAsync(Guid.NewGuid(), string.Empty, "admin", CancellationToken.None));
	}

	[Fact]
	public async Task CancelErasureAsync_ThrowsArgumentException_WhenCancelledByIsEmpty()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.CancelErasureAsync(Guid.NewGuid(), "reason", string.Empty, CancellationToken.None));
	}

	[Fact]
	public async Task CancelErasureAsync_ReturnsFalse_WhenRequestNotFound()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		_ = A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns((ErasureStatus?)null);

		// Act
		var result = await _sut.CancelErasureAsync(requestId, "reason", "admin", CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task CancelErasureAsync_ThrowsInvalidOperation_WhenAlreadyExecuted()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = new ErasureStatus
		{
			RequestId = requestId,
			Status = ErasureRequestStatus.Completed,
			DataSubjectIdHash = "hash",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			RequestedAt = DateTimeOffset.UtcNow,
			RequestedBy = "operator",
			UpdatedAt = DateTimeOffset.UtcNow
		};

		_ = A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.CancelErasureAsync(requestId, "reason", "admin", CancellationToken.None));
	}

	[Fact]
	public async Task CancelErasureAsync_ReturnsTrue_WhenCancelled()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = new ErasureStatus
		{
			RequestId = requestId,
			Status = ErasureRequestStatus.Scheduled,
			DataSubjectIdHash = "hash",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			RequestedAt = DateTimeOffset.UtcNow,
			RequestedBy = "operator",
			UpdatedAt = DateTimeOffset.UtcNow
		};

		_ = A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);

		_ = A.CallTo(() => _store.RecordCancellationAsync(
			requestId,
			A<string>._,
			A<string>._,
			A<CancellationToken>._))
			.Returns(true);

		// Act
		var result = await _sut.CancelErasureAsync(requestId, "User requested", "admin", CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task CancelErasureAsync_RecordsCancellation_WhenCancelled()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = new ErasureStatus
		{
			RequestId = requestId,
			Status = ErasureRequestStatus.Scheduled,
			DataSubjectIdHash = "hash",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			RequestedAt = DateTimeOffset.UtcNow,
			RequestedBy = "operator",
			UpdatedAt = DateTimeOffset.UtcNow
		};

		_ = A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);

		_ = A.CallTo(() => _store.RecordCancellationAsync(
			A<Guid>._,
			A<string>._,
			A<string>._,
			A<CancellationToken>._))
			.Returns(true);

		// Act
		_ = await _sut.CancelErasureAsync(requestId, "User requested", "admin@test.com", CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _store.RecordCancellationAsync(
			requestId,
			"User requested",
			"admin@test.com",
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion CancelErasureAsync Tests

	#region GenerateCertificateAsync Tests

	[Fact]
	public async Task GenerateCertificateAsync_ThrowsKeyNotFoundException_WhenRequestNotFound()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		_ = A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns((ErasureStatus?)null);

		// Act & Assert
		_ = await Should.ThrowAsync<KeyNotFoundException>(() =>
			_sut.GenerateCertificateAsync(requestId, CancellationToken.None));
	}

	[Fact]
	public async Task GenerateCertificateAsync_ThrowsInvalidOperation_WhenNotExecuted()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = new ErasureStatus
		{
			RequestId = requestId,
			Status = ErasureRequestStatus.Scheduled,
			DataSubjectIdHash = "hash",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			RequestedAt = DateTimeOffset.UtcNow,
			RequestedBy = "operator",
			UpdatedAt = DateTimeOffset.UtcNow
		};

		_ = A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.GenerateCertificateAsync(requestId, CancellationToken.None));
	}

	[Fact]
	public async Task GenerateCertificateAsync_ReturnsExistingCertificate_WhenAlreadyExists()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var certificateId = Guid.NewGuid();
		var status = new ErasureStatus
		{
			RequestId = requestId,
			Status = ErasureRequestStatus.Completed,
			DataSubjectIdHash = "hash",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			RequestedAt = DateTimeOffset.UtcNow,
			RequestedBy = "operator",
			CompletedAt = DateTimeOffset.UtcNow,
			KeysDeleted = 5,
			UpdatedAt = DateTimeOffset.UtcNow
		};

		var existingCertificate = new ErasureCertificate
		{
			CertificateId = certificateId,
			RequestId = requestId,
			DataSubjectReference = "hash",
			RequestReceivedAt = status.RequestedAt,
			CompletedAt = status.CompletedAt.Value,
			Method = ErasureMethod.CryptographicErasure,
			Summary = new ErasureSummary { KeysDeleted = 5, RecordsAffected = 10 },
			Verification = new VerificationSummary { Verified = true, Methods = VerificationMethod.KeyManagementSystem, VerifiedAt = DateTimeOffset.UtcNow },
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			Signature = "test-signature",
			RetainUntil = DateTimeOffset.UtcNow.AddYears(7)
		};

		_ = A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);

		_ = A.CallTo(() => _certStore.GetCertificateAsync(requestId, A<CancellationToken>._))
			.Returns(existingCertificate);

		// Act
		var result = await _sut.GenerateCertificateAsync(requestId, CancellationToken.None);

		// Assert
		result.ShouldBe(existingCertificate);
		A.CallTo(() => _certStore.SaveCertificateAsync(
			A<ErasureCertificate>._,
			A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task GenerateCertificateAsync_CreatesNewCertificate_WhenNotExists()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = new ErasureStatus
		{
			RequestId = requestId,
			Status = ErasureRequestStatus.Completed,
			DataSubjectIdHash = "hash",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			RequestedAt = DateTimeOffset.UtcNow.AddDays(-1),
			RequestedBy = "operator",
			CompletedAt = DateTimeOffset.UtcNow,
			KeysDeleted = 5,
			RecordsAffected = 10,
			UpdatedAt = DateTimeOffset.UtcNow
		};

		_ = A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);

		_ = A.CallTo(() => _certStore.GetCertificateAsync(requestId, A<CancellationToken>._))
			.Returns((ErasureCertificate?)null);

		// Act
		var result = await _sut.GenerateCertificateAsync(requestId, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.RequestId.ShouldBe(requestId);
		result.DataSubjectReference.ShouldBe("hash");
		result.Method.ShouldBe(ErasureMethod.CryptographicErasure);
		result.Summary.KeysDeleted.ShouldBe(5);
		result.Signature.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public async Task GenerateCertificateAsync_SavesNewCertificate()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = new ErasureStatus
		{
			RequestId = requestId,
			Status = ErasureRequestStatus.Completed,
			DataSubjectIdHash = "hash",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			RequestedAt = DateTimeOffset.UtcNow.AddDays(-1),
			RequestedBy = "operator",
			CompletedAt = DateTimeOffset.UtcNow,
			KeysDeleted = 5,
			UpdatedAt = DateTimeOffset.UtcNow
		};

		_ = A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);

		_ = A.CallTo(() => _certStore.GetCertificateAsync(requestId, A<CancellationToken>._))
			.Returns((ErasureCertificate?)null);

		// Act
		_ = await _sut.GenerateCertificateAsync(requestId, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _certStore.SaveCertificateAsync(
			A<ErasureCertificate>.That.Matches(c => c.RequestId == requestId),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion GenerateCertificateAsync Tests

	#region ListRequestsAsync Tests (via IErasureQueryStore)

	[Fact]
	public async Task ListRequestsAsync_ReturnsEmptyList_WhenNoRequests()
	{
		// Arrange - ListRequestsAsync is now on IErasureQueryStore (ISP split)
		_ = A.CallTo(() => _queryStore.ListRequestsAsync(
			A<ErasureRequestStatus?>._,
			A<string?>._,
			A<DateTimeOffset?>._,
			A<DateTimeOffset?>._,
			A<int>._,
			A<int>._,
			A<CancellationToken>._))
			.Returns(Array.Empty<ErasureStatus>());

		// Act
		var result = await _queryStore.ListRequestsAsync(null, null, null, null, 1, 100, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task ListRequestsAsync_PassesFilterParameters()
	{
		// Arrange - ListRequestsAsync is now on IErasureQueryStore (ISP split)
		var status = ErasureRequestStatus.Scheduled;
		var tenantId = "tenant-1";
		var fromDate = DateTimeOffset.UtcNow.AddDays(-7);
		var toDate = DateTimeOffset.UtcNow;

		_ = A.CallTo(() => _queryStore.ListRequestsAsync(
			A<ErasureRequestStatus?>._,
			A<string?>._,
			A<DateTimeOffset?>._,
			A<DateTimeOffset?>._,
			A<int>._,
			A<int>._,
			A<CancellationToken>._))
			.Returns(Array.Empty<ErasureStatus>());

		// Act
		_ = await _queryStore.ListRequestsAsync(status, tenantId, fromDate, toDate, 1, 100, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _queryStore.ListRequestsAsync(
			status,
			tenantId,
			fromDate,
			toDate,
			1,
			100,
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion ListRequestsAsync Tests (via IErasureQueryStore)

	#region ExecuteAsync Tests

	[Fact]
	public async Task ExecuteAsync_ReturnsNotFound_WhenRequestNotFound()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		_ = A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns((ErasureStatus?)null);

		// Act
		var result = await _sut.ExecuteAsync(requestId, CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("not found");
	}

	[Fact]
	public async Task ExecuteAsync_ReturnsInvalidStatus_WhenNotScheduled()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = new ErasureStatus
		{
			RequestId = requestId,
			Status = ErasureRequestStatus.Completed,
			DataSubjectIdHash = "hash",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			RequestedAt = DateTimeOffset.UtcNow,
			RequestedBy = "operator",
			UpdatedAt = DateTimeOffset.UtcNow
		};

		_ = A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);

		// Act
		var result = await _sut.ExecuteAsync(requestId, CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("Invalid status");
	}

	[Fact]
	public async Task ExecuteAsync_UpdatesStatusToInProgress()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = new ErasureStatus
		{
			RequestId = requestId,
			Status = ErasureRequestStatus.Scheduled,
			DataSubjectIdHash = "hash",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			RequestedAt = DateTimeOffset.UtcNow,
			RequestedBy = "operator",
			UpdatedAt = DateTimeOffset.UtcNow
		};

		_ = A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);

		// Act
		_ = await _sut.ExecuteAsync(requestId, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _store.UpdateStatusAsync(
			requestId,
			ErasureRequestStatus.InProgress,
			A<string?>._,
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ExecuteAsync_RecordsCompletion_WhenSuccessful()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = new ErasureStatus
		{
			RequestId = requestId,
			Status = ErasureRequestStatus.Scheduled,
			DataSubjectIdHash = "hash",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			RequestedAt = DateTimeOffset.UtcNow,
			RequestedBy = "operator",
			UpdatedAt = DateTimeOffset.UtcNow
		};

		_ = A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);

		_ = A.CallTo(() => _store.UpdateStatusAsync(
			requestId,
			ErasureRequestStatus.InProgress,
			A<string?>._,
			A<CancellationToken>._))
			.Returns(true);

		// Act
		var result = await _sut.ExecuteAsync(requestId, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		_ = A.CallTo(() => _store.RecordCompletionAsync(
			requestId,
			A<int>._,
			A<int>._,
			A<Guid>._,
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ExecuteAsync_UpdatesStatusToFailed_WhenException()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = new ErasureStatus
		{
			RequestId = requestId,
			Status = ErasureRequestStatus.Scheduled,
			DataSubjectIdHash = "hash",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			RequestedAt = DateTimeOffset.UtcNow,
			RequestedBy = "operator",
			UpdatedAt = DateTimeOffset.UtcNow
		};

		_ = A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);

		_ = A.CallTo(() => _store.UpdateStatusAsync(
			requestId,
			ErasureRequestStatus.InProgress,
			A<string?>._,
			A<CancellationToken>._))
			.Returns(true);

		_ = A.CallTo(() => _store.RecordCompletionAsync(
			A<Guid>._,
			A<int>._,
			A<int>._,
			A<Guid>._,
			A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Database error"));

		// Act
		var result = await _sut.ExecuteAsync(requestId, CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		_ = A.CallTo(() => _store.UpdateStatusAsync(
			requestId,
			ErasureRequestStatus.Failed,
			A<string>.That.Contains("Database error"),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion ExecuteAsync Tests

	#region Helper Methods

	private static ErasureRequest CreateValidRequest()
	{
		return new ErasureRequest
		{
			DataSubjectId = "user-123",
			IdType = DataSubjectIdType.UserId,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			RequestedBy = "operator@test.com"
		};
	}

	#endregion Helper Methods
}
