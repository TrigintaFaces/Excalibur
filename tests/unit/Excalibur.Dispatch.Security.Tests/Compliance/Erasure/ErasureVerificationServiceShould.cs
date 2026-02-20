// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Erasure;

/// <summary>
/// Unit tests for <see cref="ErasureVerificationService"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class ErasureVerificationServiceShould
{
	private readonly IErasureStore _fakeStore;
	private readonly IErasureCertificateStore _fakeCertStore;
	private readonly IKeyManagementProvider _fakeKeyProvider;
	private readonly IDataInventoryService _fakeInventoryService;
	private readonly IAuditStore _fakeAuditStore;
	private readonly IOptions<ErasureOptions> _fakeOptions;
	private readonly ILogger<ErasureVerificationService> _fakeLogger;
	private readonly ErasureVerificationService _sut;

	public ErasureVerificationServiceShould()
	{
		_fakeStore = A.Fake<IErasureStore>();
		_fakeCertStore = A.Fake<IErasureCertificateStore>();
		_fakeKeyProvider = A.Fake<IKeyManagementProvider>();
		_fakeInventoryService = A.Fake<IDataInventoryService>();
		_fakeAuditStore = A.Fake<IAuditStore>();
		_fakeOptions = A.Fake<IOptions<ErasureOptions>>();
		_fakeLogger = A.Fake<ILogger<ErasureVerificationService>>();

		// Wire up GetService to return the certificate sub-store
		_ = A.CallTo(() => _fakeStore.GetService(typeof(IErasureCertificateStore)))
			.Returns(_fakeCertStore);

		// Default options with all verification methods enabled
		_ = A.CallTo(() => _fakeOptions.Value).Returns(new ErasureOptions
		{
			VerificationMethods = VerificationMethod.KeyManagementSystem
				| VerificationMethod.AuditLog
				| VerificationMethod.DecryptionFailure
		});

		// Setup default audit store behavior (return completion event)
		_ = A.CallTo(() => _fakeAuditStore.QueryAsync(A<AuditQuery>._, A<CancellationToken>._))
			.Returns(new List<AuditEvent>
			{
				new AuditEvent
				{
					EventId = Guid.NewGuid().ToString(),
					EventType = AuditEventType.Compliance,
					Action = "DataErasure.Completed",
					Outcome = AuditOutcome.Success,
					Timestamp = DateTimeOffset.UtcNow,
					ActorId = "system"
				}
			});

		_sut = new ErasureVerificationService(
			_fakeStore,
			_fakeKeyProvider,
			_fakeInventoryService,
			_fakeAuditStore,
			_fakeOptions,
			_fakeLogger);
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenErasureStoreIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new ErasureVerificationService(
			null!,
			_fakeKeyProvider,
			_fakeInventoryService,
			_fakeAuditStore,
			_fakeOptions,
			_fakeLogger))
			.ParamName.ShouldBe("erasureStore");
	}

	[Fact]
	public void ThrowArgumentNullException_WhenKeyProviderIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new ErasureVerificationService(
			_fakeStore,
			null!,
			_fakeInventoryService,
			_fakeAuditStore,
			_fakeOptions,
			_fakeLogger))
			.ParamName.ShouldBe("keyProvider");
	}

	[Fact]
	public void ThrowArgumentNullException_WhenInventoryServiceIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new ErasureVerificationService(
			_fakeStore,
			_fakeKeyProvider,
			null!,
			_fakeAuditStore,
			_fakeOptions,
			_fakeLogger))
			.ParamName.ShouldBe("inventoryService");
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new ErasureVerificationService(
			_fakeStore,
			_fakeKeyProvider,
			_fakeInventoryService,
			_fakeAuditStore,
			null!,
			_fakeLogger))
			.ParamName.ShouldBe("options");
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new ErasureVerificationService(
			_fakeStore,
			_fakeKeyProvider,
			_fakeInventoryService,
			_fakeAuditStore,
			_fakeOptions,
			null!))
			.ParamName.ShouldBe("logger");
	}

	#endregion Constructor Tests

	#region VerifyErasureAsync Tests

	[Fact]
	public async Task VerifyErasureAsync_ReturnFailure_WhenRequestNotFound()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		_ = A.CallTo(() => _fakeStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns((ErasureStatus?)null);

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert
		result.Verified.ShouldBeFalse();
		result.Failures.ShouldContain(f => f.Reason.Contains("not found"));
		result.Failures[0].Severity.ShouldBe(VerificationSeverity.Critical);
	}

	[Fact]
	public async Task VerifyErasureAsync_ReturnFailure_WhenRequestNotCompleted()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Pending);
		_ = A.CallTo(() => _fakeStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert
		result.Verified.ShouldBeFalse();
		result.Failures.ShouldContain(f => f.Reason.Contains("not completed"));
	}

	[Fact]
	public async Task VerifyErasureAsync_ReturnSuccess_WhenAllKeysVerifiedDeleted()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var keyIds = new List<string> { "key-1", "key-2" };
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, keyIds);

		_ = A.CallTo(() => _fakeStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);
		_ = A.CallTo(() => _fakeCertStore.GetCertificateByIdAsync(status.CertificateId!.Value, A<CancellationToken>._))
			.Returns(certificate);
		_ = A.CallTo(() => _fakeKeyProvider.GetKeyAsync(A<string>._, A<CancellationToken>._))
			.Returns((KeyMetadata?)null); // Keys not found = deleted

		SetupEmptyInventory(status);

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert
		result.Verified.ShouldBeTrue();
		result.DeletedKeyIds.ShouldContain("key-1");
		result.DeletedKeyIds.ShouldContain("key-2");
		result.Methods.HasFlag(VerificationMethod.KeyManagementSystem).ShouldBeTrue();
	}

	[Fact]
	public async Task VerifyErasureAsync_ReturnSuccess_WhenKeysMarkedDestroyed()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var keyIds = new List<string> { "key-1" };
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, keyIds);

		_ = A.CallTo(() => _fakeStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);
		_ = A.CallTo(() => _fakeCertStore.GetCertificateByIdAsync(status.CertificateId!.Value, A<CancellationToken>._))
			.Returns(certificate);
		_ = A.CallTo(() => _fakeKeyProvider.GetKeyAsync("key-1", A<CancellationToken>._))
			.Returns(new KeyMetadata
			{
				KeyId = "key-1",
				Version = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				Status = KeyStatus.Destroyed,
				CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
				ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1)
			});

		SetupEmptyInventory(status);

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert
		result.Verified.ShouldBeTrue();
		result.DeletedKeyIds.ShouldContain("key-1");
	}

	[Fact]
	public async Task VerifyErasureAsync_ReturnSuccess_WhenKeysMarkedPendingDestruction()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var keyIds = new List<string> { "key-1" };
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, keyIds);

		_ = A.CallTo(() => _fakeStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);
		_ = A.CallTo(() => _fakeCertStore.GetCertificateByIdAsync(status.CertificateId!.Value, A<CancellationToken>._))
			.Returns(certificate);
		_ = A.CallTo(() => _fakeKeyProvider.GetKeyAsync("key-1", A<CancellationToken>._))
			.Returns(new KeyMetadata
			{
				KeyId = "key-1",
				Version = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				Status = KeyStatus.PendingDestruction,
				CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
				ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
			});

		SetupEmptyInventory(status);

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert
		result.Verified.ShouldBeTrue();
	}

	[Fact]
	public async Task VerifyErasureAsync_ReturnFailure_WhenKeyStillActive()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var keyIds = new List<string> { "key-1" };
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, keyIds);

		_ = A.CallTo(() => _fakeStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);
		_ = A.CallTo(() => _fakeCertStore.GetCertificateByIdAsync(status.CertificateId!.Value, A<CancellationToken>._))
			.Returns(certificate);
		_ = A.CallTo(() => _fakeKeyProvider.GetKeyAsync("key-1", A<CancellationToken>._))
			.Returns(new KeyMetadata
			{
				KeyId = "key-1",
				Version = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				Status = KeyStatus.Active,
				CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
				ExpiresAt = DateTimeOffset.UtcNow.AddDays(365)
			});

		SetupEmptyInventory(status);

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert
		result.Verified.ShouldBeFalse();
		result.Failures.ShouldContain(f => f.Subject.Contains("key-1"));
		result.Failures.ShouldContain(f => f.FailedMethod == VerificationMethod.KeyManagementSystem);
	}

	[Fact]
	public async Task VerifyErasureAsync_IncludeVerificationDuration()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, []);

		_ = A.CallTo(() => _fakeStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);
		_ = A.CallTo(() => _fakeCertStore.GetCertificateByIdAsync(status.CertificateId!.Value, A<CancellationToken>._))
			.Returns(certificate);

		SetupEmptyInventory(status);

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert
		result.Duration.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public async Task VerifyErasureAsync_GenerateResultHash()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, []);

		_ = A.CallTo(() => _fakeStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);
		_ = A.CallTo(() => _fakeCertStore.GetCertificateByIdAsync(status.CertificateId!.Value, A<CancellationToken>._))
			.Returns(certificate);

		SetupEmptyInventory(status);

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert
		result.ResultHash.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public async Task VerifyErasureAsync_HandleExceptionGracefully()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		_ = A.CallTo(() => _fakeStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Database error"));

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert
		result.Verified.ShouldBeFalse();
		result.Failures.ShouldContain(f => f.Reason.Contains("exception"));
		result.Failures[0].Severity.ShouldBe(VerificationSeverity.Critical);
	}

	[Fact]
	public async Task VerifyErasureAsync_VerifyNoCertificate_WhenCertificateIdMissing()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, certificateId: null);

		_ = A.CallTo(() => _fakeStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);

		SetupEmptyInventory(status);

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert
		result.Verified.ShouldBeTrue();
		result.DeletedKeyIds.ShouldBeEmpty();
		A.CallTo(() => _fakeCertStore.GetCertificateByIdAsync(A<Guid>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task VerifyErasureAsync_UseOnlyConfiguredMethods()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, ["key-1"]);

		_ = A.CallTo(() => _fakeOptions.Value).Returns(new ErasureOptions
		{
			VerificationMethods = VerificationMethod.KeyManagementSystem // Only KMS, no audit or decryption
		});

		_ = A.CallTo(() => _fakeStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);
		_ = A.CallTo(() => _fakeCertStore.GetCertificateByIdAsync(status.CertificateId!.Value, A<CancellationToken>._))
			.Returns(certificate);
		_ = A.CallTo(() => _fakeKeyProvider.GetKeyAsync("key-1", A<CancellationToken>._))
			.Returns((KeyMetadata?)null);

		var sut = new ErasureVerificationService(
			_fakeStore,
			_fakeKeyProvider,
			_fakeInventoryService,
			_fakeAuditStore,
			_fakeOptions,
			_fakeLogger);

		// Act
		var result = await sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert
		result.Methods.ShouldBe(VerificationMethod.KeyManagementSystem);
		result.Methods.HasFlag(VerificationMethod.AuditLog).ShouldBeFalse();
		result.Methods.HasFlag(VerificationMethod.DecryptionFailure).ShouldBeFalse();
	}

	[Fact]
	public async Task VerifyErasureAsync_StillPassesVerification_WhenNoKeysDeleted()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, keysDeleted: 0, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, []);

		_ = A.CallTo(() => _fakeStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);
		_ = A.CallTo(() => _fakeCertStore.GetCertificateByIdAsync(status.CertificateId!.Value, A<CancellationToken>._))
			.Returns(certificate);

		SetupEmptyInventory(status);

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert
		// Verification can still pass when no keys were deleted (e.g., data was deleted directly)
		// The audit log verification passes internally but warnings are only added on failure path
		result.Verified.ShouldBeTrue();
	}

	#endregion VerifyErasureAsync Tests

	#region GenerateReportAsync Tests

	[Fact]
	public async Task GenerateReportAsync_ReturnReport_WhenRequestNotFound()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		_ = A.CallTo(() => _fakeStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns((ErasureStatus?)null);

		// Act
		var report = await _sut.GenerateReportAsync(requestId, CancellationToken.None);

		// Assert
		_ = report.ShouldNotBeNull();
		report.RequestId.ShouldBe(requestId);
		report.Result.Verified.ShouldBeFalse();
		report.Steps.ShouldContain(s => s.Name == "Retrieve Erasure Request" && !s.Passed);
	}

	[Fact]
	public async Task GenerateReportAsync_IncludeAllVerificationSteps()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, ["key-1"]);

		_ = A.CallTo(() => _fakeStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);
		_ = A.CallTo(() => _fakeCertStore.GetCertificateByIdAsync(status.CertificateId!.Value, A<CancellationToken>._))
			.Returns(certificate);
		_ = A.CallTo(() => _fakeKeyProvider.GetKeyAsync("key-1", A<CancellationToken>._))
			.Returns((KeyMetadata?)null);

		SetupEmptyInventory(status);

		// Act
		var report = await _sut.GenerateReportAsync(requestId, CancellationToken.None);

		// Assert
		report.Steps.ShouldContain(s => s.Name == "Retrieve Erasure Request");
		report.Steps.ShouldContain(s => s.Name == "Retrieve Certificate");
		report.Steps.ShouldContain(s => s.Name == "Key Management System Verification");
		report.Steps.ShouldContain(s => s.Name == "Audit Log Verification");
		report.Steps.ShouldContain(s => s.Name == "Decryption Failure Verification");
	}

	[Fact]
	public async Task GenerateReportAsync_GenerateReportId()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, []);

		_ = A.CallTo(() => _fakeStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);
		_ = A.CallTo(() => _fakeCertStore.GetCertificateByIdAsync(status.CertificateId!.Value, A<CancellationToken>._))
			.Returns(certificate);

		SetupEmptyInventory(status);

		// Act
		var report = await _sut.GenerateReportAsync(requestId, CancellationToken.None);

		// Assert
		report.ReportId.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public async Task GenerateReportAsync_IncludeReportHash()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, []);

		_ = A.CallTo(() => _fakeStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);
		_ = A.CallTo(() => _fakeCertStore.GetCertificateByIdAsync(status.CertificateId!.Value, A<CancellationToken>._))
			.Returns(certificate);

		SetupEmptyInventory(status);

		// Act
		var report = await _sut.GenerateReportAsync(requestId, CancellationToken.None);

		// Assert
		report.ReportHash.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public async Task GenerateReportAsync_IncludeGeneratedTimestamp()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, []);

		_ = A.CallTo(() => _fakeStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);
		_ = A.CallTo(() => _fakeCertStore.GetCertificateByIdAsync(status.CertificateId!.Value, A<CancellationToken>._))
			.Returns(certificate);

		SetupEmptyInventory(status);

		var beforeTime = DateTimeOffset.UtcNow;

		// Act
		var report = await _sut.GenerateReportAsync(requestId, CancellationToken.None);

		// Assert
		report.GeneratedAt.ShouldBeGreaterThanOrEqualTo(beforeTime);
		report.GeneratedAt.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);
	}

	[Fact]
	public async Task GenerateReportAsync_IncludeStepDurations()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, []);

		_ = A.CallTo(() => _fakeStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);
		_ = A.CallTo(() => _fakeCertStore.GetCertificateByIdAsync(status.CertificateId!.Value, A<CancellationToken>._))
			.Returns(certificate);

		SetupEmptyInventory(status);

		// Act
		var report = await _sut.GenerateReportAsync(requestId, CancellationToken.None);

		// Assert
		report.Steps.ShouldAllBe(s => s.Duration >= TimeSpan.Zero);
	}

	[Fact]
	public async Task GenerateReportAsync_SkipCertificateStep_WhenNoCertificateId()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, certificateId: null);

		_ = A.CallTo(() => _fakeStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);

		SetupEmptyInventory(status);

		// Act
		var report = await _sut.GenerateReportAsync(requestId, CancellationToken.None);

		// Assert
		report.Steps.ShouldNotContain(s => s.Name == "Retrieve Certificate");
	}

	[Fact]
	public async Task GenerateReportAsync_IncludeFailureDetails_WhenKmsVerificationFails()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, ["key-1"]);

		_ = A.CallTo(() => _fakeStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);
		_ = A.CallTo(() => _fakeCertStore.GetCertificateByIdAsync(status.CertificateId!.Value, A<CancellationToken>._))
			.Returns(certificate);
		_ = A.CallTo(() => _fakeKeyProvider.GetKeyAsync("key-1", A<CancellationToken>._))
			.Returns(new KeyMetadata
			{
				KeyId = "key-1",
				Version = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				Status = KeyStatus.Active, // Still active = failure
				CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
				ExpiresAt = DateTimeOffset.UtcNow.AddDays(365)
			});

		SetupEmptyInventory(status);

		// Act
		var report = await _sut.GenerateReportAsync(requestId, CancellationToken.None);

		// Assert
		var kmsStep = report.Steps.First(s => s.Name == "Key Management System Verification");
		kmsStep.Passed.ShouldBeFalse();
		kmsStep.Details.ShouldContain("Failed");
	}

	#endregion GenerateReportAsync Tests

	#region VerifyKeyDeletionAsync Tests

	[Fact]
	public async Task VerifyKeyDeletionAsync_ThrowArgumentException_WhenKeyIdIsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => _sut.VerifyKeyDeletionAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task VerifyKeyDeletionAsync_ThrowArgumentException_WhenKeyIdIsEmpty()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => _sut.VerifyKeyDeletionAsync(string.Empty, CancellationToken.None));
	}

	[Fact]
	public async Task VerifyKeyDeletionAsync_ThrowArgumentException_WhenKeyIdIsWhitespace()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => _sut.VerifyKeyDeletionAsync("   ", CancellationToken.None));
	}

	[Fact]
	public async Task VerifyKeyDeletionAsync_ReturnTrue_WhenKeyNotFound()
	{
		// Arrange
		var keyId = "deleted-key-1";
		_ = A.CallTo(() => _fakeKeyProvider.GetKeyAsync(keyId, A<CancellationToken>._))
			.Returns((KeyMetadata?)null);

		// Act
		var result = await _sut.VerifyKeyDeletionAsync(keyId, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task VerifyKeyDeletionAsync_ReturnTrue_WhenKeyNotFoundExceptionThrown()
	{
		// Arrange
		var keyId = "deleted-key-1";
		_ = A.CallTo(() => _fakeKeyProvider.GetKeyAsync(keyId, A<CancellationToken>._))
			.ThrowsAsync(new KeyNotFoundException($"Key {keyId} not found"));

		// Act
		var result = await _sut.VerifyKeyDeletionAsync(keyId, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task VerifyKeyDeletionAsync_ReturnTrue_WhenKeyIsDestroyed()
	{
		// Arrange
		var keyId = "destroyed-key-1";
		_ = A.CallTo(() => _fakeKeyProvider.GetKeyAsync(keyId, A<CancellationToken>._))
			.Returns(new KeyMetadata
			{
				KeyId = keyId,
				Version = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				Status = KeyStatus.Destroyed,
				CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
				ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1)
			});

		// Act
		var result = await _sut.VerifyKeyDeletionAsync(keyId, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task VerifyKeyDeletionAsync_ReturnTrue_WhenKeyIsPendingDestruction()
	{
		// Arrange
		var keyId = "pending-destruction-key-1";
		_ = A.CallTo(() => _fakeKeyProvider.GetKeyAsync(keyId, A<CancellationToken>._))
			.Returns(new KeyMetadata
			{
				KeyId = keyId,
				Version = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				Status = KeyStatus.PendingDestruction,
				CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
				ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
			});

		// Act
		var result = await _sut.VerifyKeyDeletionAsync(keyId, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task VerifyKeyDeletionAsync_ReturnFalse_WhenKeyIsActive()
	{
		// Arrange
		var keyId = "active-key-1";
		_ = A.CallTo(() => _fakeKeyProvider.GetKeyAsync(keyId, A<CancellationToken>._))
			.Returns(new KeyMetadata
			{
				KeyId = keyId,
				Version = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				Status = KeyStatus.Active,
				CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
				ExpiresAt = DateTimeOffset.UtcNow.AddDays(365)
			});

		// Act
		var result = await _sut.VerifyKeyDeletionAsync(keyId, CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task VerifyKeyDeletionAsync_ReturnFalse_WhenKeyIsRotated()
	{
		// Arrange
		var keyId = "rotated-key-1";
		_ = A.CallTo(() => _fakeKeyProvider.GetKeyAsync(keyId, A<CancellationToken>._))
			.Returns(new KeyMetadata
			{
				KeyId = keyId,
				Version = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				Status = KeyStatus.DecryptOnly,
				CreatedAt = DateTimeOffset.UtcNow.AddDays(-60),
				ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
			});

		// Act
		var result = await _sut.VerifyKeyDeletionAsync(keyId, CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task VerifyKeyDeletionAsync_ReturnFalse_WhenExceptionOccurs()
	{
		// Arrange
		var keyId = "problematic-key-1";
		_ = A.CallTo(() => _fakeKeyProvider.GetKeyAsync(keyId, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Connection error"));

		// Act
		var result = await _sut.VerifyKeyDeletionAsync(keyId, CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion VerifyKeyDeletionAsync Tests

	#region Helper Methods

	private static ErasureStatus CreateStatus(
		Guid requestId,
		ErasureRequestStatus status,
		int keysDeleted = 2,
		Guid? certificateId = null)
	{
		return new ErasureStatus
		{
			RequestId = requestId,
			DataSubjectIdHash = "hash-abc123",
			IdType = DataSubjectIdType.UserId,
			TenantId = "tenant-1",
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			Status = status,
			RequestedAt = DateTimeOffset.UtcNow.AddDays(-1),
			RequestedBy = "test-operator",
			CompletedAt = status == ErasureRequestStatus.Completed ? DateTimeOffset.UtcNow : null,
			KeysDeleted = keysDeleted,
			CertificateId = certificateId,
			UpdatedAt = DateTimeOffset.UtcNow
		};
	}

	private static ErasureCertificate CreateCertificate(
		Guid requestId,
		IReadOnlyList<string> deletedKeyIds)
	{
		return new ErasureCertificate
		{
			CertificateId = Guid.NewGuid(),
			RequestId = requestId,
			DataSubjectReference = "hash-abc123",
			RequestReceivedAt = DateTimeOffset.UtcNow.AddDays(-1),
			CompletedAt = DateTimeOffset.UtcNow,
			Method = ErasureMethod.CryptographicErasure,
			Summary = new ErasureSummary { KeysDeleted = deletedKeyIds.Count, RecordsAffected = 10 },
			Verification = new VerificationSummary
			{
				Verified = true,
				Methods = VerificationMethod.KeyManagementSystem,
				DeletedKeyIds = deletedKeyIds.ToList(),
				VerifiedAt = DateTimeOffset.UtcNow
			},
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			Signature = "signature-123",
			RetainUntil = DateTimeOffset.UtcNow.AddYears(7)
		};
	}

	private void SetupEmptyInventory(ErasureStatus status)
	{
		_ = A.CallTo(() => _fakeInventoryService.DiscoverAsync(
				status.DataSubjectIdHash,
				status.IdType,
				status.TenantId,
				A<CancellationToken>._))
			.Returns(new DataInventory
			{
				DataSubjectId = status.DataSubjectIdHash,
				Locations = [],
				DiscoveredAt = DateTimeOffset.UtcNow
			});
	}

	#endregion Helper Methods
}
