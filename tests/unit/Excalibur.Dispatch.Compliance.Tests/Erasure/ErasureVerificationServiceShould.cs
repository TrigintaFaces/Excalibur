using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.Erasure;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ErasureVerificationServiceShould
{
	private readonly IErasureStore _erasureStore = A.Fake<IErasureStore>();
	private readonly IKeyManagementProvider _keyProvider = A.Fake<IKeyManagementProvider>();
	private readonly IDataInventoryService _inventoryService = A.Fake<IDataInventoryService>();
	private readonly IAuditStore _auditStore = A.Fake<IAuditStore>();
	private readonly ErasureOptions _erasureOptions = new()
	{
		VerificationMethods = VerificationMethod.KeyManagementSystem | VerificationMethod.AuditLog
	};

	private readonly NullLogger<ErasureVerificationService> _logger = NullLogger<ErasureVerificationService>.Instance;

	[Fact]
	public async Task Return_failed_when_erasure_request_not_found()
	{
		var requestId = Guid.NewGuid();
		A.CallTo(() => _erasureStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns((ErasureStatus?)null);

		var sut = CreateService();

		var result = await sut.VerifyErasureAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		result.Verified.ShouldBeFalse();
		result.Failures.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task Return_failed_when_erasure_not_completed()
	{
		var requestId = Guid.NewGuid();
		A.CallTo(() => _erasureStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(CreateErasureStatus(requestId, ErasureRequestStatus.InProgress));

		var sut = CreateService();

		var result = await sut.VerifyErasureAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		result.Verified.ShouldBeFalse();
	}

	[Fact]
	public async Task Verify_erasure_with_kms_method()
	{
		var requestId = Guid.NewGuid();
		SetupCompletedErasure(requestId);

		A.CallTo(() => _keyProvider.GetKeyAsync(A<string>._, A<CancellationToken>._))
			.Returns((KeyMetadata?)null); // Key is deleted

		var sut = CreateService();

		var result = await sut.VerifyErasureAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		result.ShouldNotBeNull();
		result.Duration.ShouldNotBe(TimeSpan.Zero);
	}

	[Fact]
	public async Task Generate_report_returns_steps()
	{
		var requestId = Guid.NewGuid();
		A.CallTo(() => _erasureStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns((ErasureStatus?)null);

		var sut = CreateService();

		var report = await sut.GenerateReportAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		report.ShouldNotBeNull();
		report.RequestId.ShouldBe(requestId);
	}

	[Fact]
	public async Task Generate_report_populates_non_negative_step_durations()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		SetupCompletedErasure(requestId);
		A.CallTo(() => _keyProvider.GetKeyAsync(A<string>._, A<CancellationToken>._))
			.Returns((KeyMetadata?)null);

		var sut = CreateService();

		// Act
		var report = await sut.GenerateReportAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		report.Steps.ShouldNotBeEmpty();
		report.Steps.All(step => step.Duration >= TimeSpan.Zero).ShouldBeTrue();
		report.Steps.All(step => step.Timestamp != default).ShouldBeTrue();
		report.Result.Duration.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
	}

	[Fact]
	public async Task Generate_report_includes_enabled_verification_steps_with_timing_details()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var certificateId = Guid.NewGuid();
		var status = CreateErasureStatus(
			requestId,
			ErasureRequestStatus.Completed,
			keysDeleted: 1,
			certificateId: certificateId);
		A.CallTo(() => _erasureStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);

		var certificate = CreateCertificate(requestId, certificateId, "key-1");
		var certificateStore = A.Fake<IErasureCertificateStore>();
		A.CallTo(() => certificateStore.GetCertificateByIdAsync(certificateId, A<CancellationToken>._))
			.Returns(certificate);
		A.CallTo(() => _erasureStore.GetService(typeof(IErasureCertificateStore)))
			.Returns(certificateStore);

		A.CallTo(() => _keyProvider.GetKeyAsync("key-1", A<CancellationToken>._))
			.Throws(new KeyNotFoundException("deleted"));

		var now = DateTimeOffset.UtcNow;
		A.CallTo(() => _auditStore.QueryAsync(A<AuditQuery>._, A<CancellationToken>._))
			.Returns([
				new AuditEvent
				{
					EventId = "audit-completed",
					EventType = AuditEventType.Compliance,
					Action = "DataErasure.Completed",
					Outcome = AuditOutcome.Success,
					Timestamp = now,
					ActorId = "system",
					ResourceId = requestId.ToString(),
					ResourceType = "ErasureRequest",
				},
				new AuditEvent
				{
					EventId = "audit-key-deleted",
					EventType = AuditEventType.Compliance,
					Action = "DataErasure.KeyDeleted",
					Outcome = AuditOutcome.Success,
					Timestamp = now,
					ActorId = "system",
					ResourceId = requestId.ToString(),
					ResourceType = "ErasureRequest",
				}
			]);

		A.CallTo(() => _inventoryService.DiscoverAsync(
				A<string>._,
				A<DataSubjectIdType>._,
				A<string?>._,
				A<CancellationToken>._))
			.Returns(new DataInventory
			{
				DataSubjectId = "hash-abc123",
				Locations =
				[
					new DataLocation
					{
						TableName = "Orders",
						FieldName = "EncryptedPayload",
						DataCategory = "Operational",
						RecordId = "order-1",
						KeyId = "key-1",
					}
				]
			});

		var options = new ErasureOptions
		{
			VerificationMethods = VerificationMethod.KeyManagementSystem |
			                      VerificationMethod.AuditLog |
			                      VerificationMethod.DecryptionFailure
		};
		var sut = CreateService(options);

		// Act
		var report = await sut.GenerateReportAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		report.Result.Verified.ShouldBeTrue();
		report.Steps.ShouldContain(step => step.Name == "Retrieve Certificate" && step.Duration >= TimeSpan.Zero);
		report.Steps.ShouldContain(step => step.Name == "Key Management System Verification" && step.Passed && step.Duration >= TimeSpan.Zero);
		report.Steps.ShouldContain(step => step.Name == "Audit Log Verification" && step.Passed && step.Duration >= TimeSpan.Zero);
		report.Steps.ShouldContain(step => step.Name == "Decryption Failure Verification" && step.Passed && step.Duration >= TimeSpan.Zero);
	}

	[Fact]
	public async Task Verify_erasure_returns_verified_with_warnings_when_audit_contains_failure_events()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var certificateId = Guid.NewGuid();
		var status = CreateErasureStatus(
			requestId,
			ErasureRequestStatus.Completed,
			keysDeleted: 1,
			certificateId: certificateId);
		A.CallTo(() => _erasureStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);

		var certificate = CreateCertificate(requestId, certificateId, "key-1");
		var certificateStore = A.Fake<IErasureCertificateStore>();
		A.CallTo(() => certificateStore.GetCertificateByIdAsync(certificateId, A<CancellationToken>._))
			.Returns(certificate);
		A.CallTo(() => _erasureStore.GetService(typeof(IErasureCertificateStore)))
			.Returns(certificateStore);

		// KMS passes by confirming key is gone.
		A.CallTo(() => _keyProvider.GetKeyAsync("key-1", A<CancellationToken>._))
			.Returns((KeyMetadata?)null);

		var now = DateTimeOffset.UtcNow;
		A.CallTo(() => _auditStore.QueryAsync(A<AuditQuery>._, A<CancellationToken>._))
			.Returns([
				new AuditEvent
				{
					EventId = "audit-completed",
					EventType = AuditEventType.Compliance,
					Action = "DataErasure.Completed",
					Outcome = AuditOutcome.Success,
					Timestamp = now,
					ActorId = "system",
					ResourceId = requestId.ToString(),
					ResourceType = "ErasureRequest"
				},
				new AuditEvent
				{
					EventId = "audit-failed",
					EventType = AuditEventType.Compliance,
					Action = "DataErasure.Failed",
					Outcome = AuditOutcome.Failure,
					Timestamp = now,
					ActorId = "system",
					ResourceId = requestId.ToString(),
					ResourceType = "ErasureRequest"
				}
			]);

		var options = new ErasureOptions
		{
			VerificationMethods = VerificationMethod.KeyManagementSystem | VerificationMethod.AuditLog
		};
		var sut = CreateService(options);

		// Act
		var result = await sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert
		result.Verified.ShouldBeTrue();
		result.Methods.ShouldBe(VerificationMethod.KeyManagementSystem);
		result.Warnings.Any(w => w.Contains("failure/rollback", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
	}

	[Fact]
	public async Task Handle_exception_during_verification()
	{
		var requestId = Guid.NewGuid();
		A.CallTo(() => _erasureStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Store unavailable"));

		var sut = CreateService();

		var result = await sut.VerifyErasureAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		result.Verified.ShouldBeFalse();
		result.Failures.ShouldNotBeEmpty();
		result.Failures[0].Reason.ShouldContain("Store unavailable");
	}

	[Fact]
	public void Throw_for_null_erasure_store()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ErasureVerificationService(
				null!, _keyProvider, _inventoryService, _auditStore,
				Microsoft.Extensions.Options.Options.Create(_erasureOptions), _logger));
	}

	[Fact]
	public void Throw_for_null_key_provider()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ErasureVerificationService(
				_erasureStore, null!, _inventoryService, _auditStore,
				Microsoft.Extensions.Options.Options.Create(_erasureOptions), _logger));
	}

	[Fact]
	public void Throw_for_null_inventory_service()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ErasureVerificationService(
				_erasureStore, _keyProvider, null!, _auditStore,
				Microsoft.Extensions.Options.Options.Create(_erasureOptions), _logger));
	}

	[Fact]
	public void Throw_for_null_audit_store()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ErasureVerificationService(
				_erasureStore, _keyProvider, _inventoryService, null!,
				Microsoft.Extensions.Options.Options.Create(_erasureOptions), _logger));
	}

	[Fact]
	public void Throw_for_null_options()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ErasureVerificationService(
				_erasureStore, _keyProvider, _inventoryService, _auditStore,
				null!, _logger));
	}

	[Fact]
	public void Throw_for_null_logger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ErasureVerificationService(
				_erasureStore, _keyProvider, _inventoryService, _auditStore,
				Microsoft.Extensions.Options.Options.Create(_erasureOptions), null!));
	}

	private static ErasureStatus CreateErasureStatus(
		Guid requestId,
		ErasureRequestStatus status,
		int? keysDeleted = null,
		Guid? certificateId = null) =>
		new()
		{
			RequestId = requestId,
			DataSubjectIdHash = "hash-abc123",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			Status = status,
			RequestedBy = "test-admin",
			RequestedAt = DateTimeOffset.UtcNow.AddHours(-1),
			UpdatedAt = DateTimeOffset.UtcNow,
			KeysDeleted = keysDeleted,
			CertificateId = certificateId
		};

	private void SetupCompletedErasure(Guid requestId)
	{
		A.CallTo(() => _erasureStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(CreateErasureStatus(requestId, ErasureRequestStatus.Completed, keysDeleted: 1));
		A.CallTo(() => _erasureStore.GetService(typeof(IErasureCertificateStore)))
			.Returns(null);
	}

	private ErasureVerificationService CreateService(ErasureOptions? options = null) =>
		new(
			_erasureStore,
			_keyProvider,
			_inventoryService,
			_auditStore,
			Microsoft.Extensions.Options.Options.Create(options ?? _erasureOptions),
			_logger);

	private static ErasureCertificate CreateCertificate(Guid requestId, Guid certificateId, string keyId) =>
		new()
		{
			CertificateId = certificateId,
			RequestId = requestId,
			DataSubjectReference = "hash-abc123",
			RequestReceivedAt = DateTimeOffset.UtcNow.AddHours(-2),
			CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
			Method = ErasureMethod.CryptographicErasure,
			Summary = new ErasureSummary
			{
				KeysDeleted = 1,
				RecordsAffected = 1,
				DataCategories = ["Operational"],
				TablesAffected = ["Orders"],
				DataSizeBytes = 128
			},
			Verification = new VerificationSummary
			{
				Verified = true,
				Methods = VerificationMethod.KeyManagementSystem | VerificationMethod.AuditLog,
				VerifiedAt = DateTimeOffset.UtcNow,
				DeletedKeyIds = [keyId]
			},
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			Signature = "test-signature",
			RetainUntil = DateTimeOffset.UtcNow.AddYears(7)
		};
}
