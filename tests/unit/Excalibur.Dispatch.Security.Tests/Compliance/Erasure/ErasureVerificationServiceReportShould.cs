// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Erasure;

/// <summary>
/// Unit tests for <see cref="ErasureVerificationService"/>.GenerateReportAsync
/// and VerifyKeyDeletionAsync edge cases.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Compliance")]
public sealed class ErasureVerificationServiceReportShould
{
	private readonly IErasureStore _erasureStore;
	private readonly IErasureCertificateStore _certStore;
	private readonly IKeyManagementProvider _keyProvider;
	private readonly IDataInventoryService _inventoryService;
	private readonly IAuditStore _auditStore;
	private readonly IOptions<ErasureOptions> _options;
	private readonly ErasureVerificationService _sut;

	public ErasureVerificationServiceReportShould()
	{
		_erasureStore = A.Fake<IErasureStore>();
		_certStore = A.Fake<IErasureCertificateStore>();
		_keyProvider = A.Fake<IKeyManagementProvider>();
		_inventoryService = A.Fake<IDataInventoryService>();
		_auditStore = A.Fake<IAuditStore>();
		_options = Microsoft.Extensions.Options.Options.Create(new ErasureOptions
		{
			VerificationMethods = VerificationMethod.KeyManagementSystem | VerificationMethod.AuditLog
		});

		// Wire up GetService to return the certificate sub-store
		_ = A.CallTo(() => _erasureStore.GetService(typeof(IErasureCertificateStore)))
			.Returns(_certStore);

		_sut = new ErasureVerificationService(
			_erasureStore,
			_keyProvider,
			_inventoryService,
			_auditStore,
			_options,
			NullLogger<ErasureVerificationService>.Instance);
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenErasureStoreIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() => new ErasureVerificationService(
			null!, _keyProvider, _inventoryService, _auditStore, _options,
			NullLogger<ErasureVerificationService>.Instance));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenKeyProviderIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() => new ErasureVerificationService(
			_erasureStore, null!, _inventoryService, _auditStore, _options,
			NullLogger<ErasureVerificationService>.Instance));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenInventoryServiceIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() => new ErasureVerificationService(
			_erasureStore, _keyProvider, null!, _auditStore, _options,
			NullLogger<ErasureVerificationService>.Instance));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenAuditStoreIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() => new ErasureVerificationService(
			_erasureStore, _keyProvider, _inventoryService, null!, _options,
			NullLogger<ErasureVerificationService>.Instance));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenOptionsIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() => new ErasureVerificationService(
			_erasureStore, _keyProvider, _inventoryService, _auditStore, null!,
			NullLogger<ErasureVerificationService>.Instance));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() => new ErasureVerificationService(
			_erasureStore, _keyProvider, _inventoryService, _auditStore, _options, null!));
	}

	#endregion

	#region VerifyErasureAsync Tests

	[Fact]
	public async Task VerifyErasureAsync_ReturnsFailed_WhenRequestNotFound()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		A.CallTo(() => _erasureStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(null));

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Verified.ShouldBeFalse();
		result.Failures.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task VerifyErasureAsync_ReturnsFailed_WhenNotCompleted()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.InProgress);
		A.CallTo(() => _erasureStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(status));

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Verified.ShouldBeFalse();
	}

	[Fact]
	public async Task VerifyErasureAsync_ReturnsVerified_WhenKmsAndAuditPass()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed);
		A.CallTo(() => _erasureStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(status));
		A.CallTo(() => _certStore.GetCertificateByIdAsync(A<Guid>._, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureCertificate?>(null));

		// Setup audit store to return completion event
		A.CallTo(() => _auditStore.QueryAsync(A<AuditQuery>._, A<CancellationToken>._))
			.Returns(new List<AuditEvent>
			{
				new()
				{
					EventId = Guid.NewGuid().ToString(),
					Action = "DataErasure.Completed",
					ResourceId = requestId.ToString(),
					ResourceType = "ErasureRequest",
					Timestamp = DateTimeOffset.UtcNow,
					EventType = AuditEventType.Compliance,
					Outcome = AuditOutcome.Success,
					ActorId = "system"
				}
			});

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Verified.ShouldBeTrue();
		result.ResultHash.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public async Task VerifyErasureAsync_ReturnsFailed_WhenExceptionOccurs()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		A.CallTo(() => _erasureStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Throws(new InvalidOperationException("store error"));

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Verified.ShouldBeFalse();
		result.Failures.ShouldContain(f => f.Severity == VerificationSeverity.Critical);
	}

	#endregion

	#region VerifyKeyDeletionAsync Tests

	[Fact]
	public async Task VerifyKeyDeletionAsync_ThrowsArgumentException_WhenKeyIdIsNullOrEmpty()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.VerifyKeyDeletionAsync("", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task VerifyKeyDeletionAsync_ReturnsTrue_WhenKeyNotFound()
	{
		// Arrange
		A.CallTo(() => _keyProvider.GetKeyAsync("key-1", A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(null));

		// Act
		var result = await _sut.VerifyKeyDeletionAsync("key-1", CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task VerifyKeyDeletionAsync_ReturnsTrue_WhenKeyIsDestroyed()
	{
		// Arrange
		A.CallTo(() => _keyProvider.GetKeyAsync("key-1", A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(new KeyMetadata
			{
				KeyId = "key-1",
				Status = KeyStatus.Destroyed,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				CreatedAt = DateTimeOffset.UtcNow,
				Version = 1
			}));

		// Act
		var result = await _sut.VerifyKeyDeletionAsync("key-1", CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task VerifyKeyDeletionAsync_ReturnsTrue_WhenKeyIsPendingDestruction()
	{
		// Arrange
		A.CallTo(() => _keyProvider.GetKeyAsync("key-1", A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(new KeyMetadata
			{
				KeyId = "key-1",
				Status = KeyStatus.PendingDestruction,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				CreatedAt = DateTimeOffset.UtcNow,
				Version = 1
			}));

		// Act
		var result = await _sut.VerifyKeyDeletionAsync("key-1", CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task VerifyKeyDeletionAsync_ReturnsFalse_WhenKeyIsActive()
	{
		// Arrange
		A.CallTo(() => _keyProvider.GetKeyAsync("key-1", A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(new KeyMetadata
			{
				KeyId = "key-1",
				Status = KeyStatus.Active,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				CreatedAt = DateTimeOffset.UtcNow,
				Version = 1
			}));

		// Act
		var result = await _sut.VerifyKeyDeletionAsync("key-1", CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task VerifyKeyDeletionAsync_ReturnsTrue_WhenKeyNotFoundException()
	{
		// Arrange
		A.CallTo(() => _keyProvider.GetKeyAsync("key-1", A<CancellationToken>._))
			.Throws(new KeyNotFoundException("not found"));

		// Act
		var result = await _sut.VerifyKeyDeletionAsync("key-1", CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task VerifyKeyDeletionAsync_ReturnsFalse_WhenGenericExceptionOccurs()
	{
		// Arrange
		A.CallTo(() => _keyProvider.GetKeyAsync("key-1", A<CancellationToken>._))
			.Throws(new InvalidOperationException("some error"));

		// Act
		var result = await _sut.VerifyKeyDeletionAsync("key-1", CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region GenerateReportAsync Tests

	[Fact]
	public async Task GenerateReportAsync_ReturnsFailedReport_WhenRequestNotFound()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		A.CallTo(() => _erasureStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(null));

		// Act
		var report = await _sut.GenerateReportAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		report.ShouldNotBeNull();
		report.RequestId.ShouldBe(requestId);
		report.Result.Verified.ShouldBeFalse();
		report.Steps.ShouldNotBeEmpty();
		report.Steps[0].Passed.ShouldBeFalse();
	}

	[Fact]
	public async Task GenerateReportAsync_IncludesKmsStep_WhenEnabled()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed);
		A.CallTo(() => _erasureStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(status));
		A.CallTo(() => _auditStore.QueryAsync(A<AuditQuery>._, A<CancellationToken>._))
			.Returns(new List<AuditEvent>
			{
				new()
				{
					EventId = Guid.NewGuid().ToString(),
					Action = "DataErasure.Completed",
					ResourceId = requestId.ToString(),
					ResourceType = "ErasureRequest",
					Timestamp = DateTimeOffset.UtcNow,
					EventType = AuditEventType.Compliance,
					Outcome = AuditOutcome.Success,
					ActorId = "system"
				}
			});

		// Act
		var report = await _sut.GenerateReportAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		report.Steps.ShouldContain(s => s.Name == "Key Management System Verification");
		report.Steps.ShouldContain(s => s.Name == "Audit Log Verification");
		report.ReportHash.ShouldNotBeNullOrEmpty();
	}

	#endregion

	#region Helpers

	private static ErasureStatus CreateStatus(Guid requestId, ErasureRequestStatus status) =>
		new()
		{
			RequestId = requestId,
			DataSubjectIdHash = "hash-value",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			Status = status,
			RequestedBy = "admin",
			RequestedAt = DateTimeOffset.UtcNow.AddDays(-1),
			CompletedAt = status == ErasureRequestStatus.Completed ? DateTimeOffset.UtcNow : null,
			UpdatedAt = DateTimeOffset.UtcNow
		};

	#endregion
}
