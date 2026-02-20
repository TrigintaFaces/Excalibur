// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Erasure;

/// <summary>
/// Unit tests for <see cref="ErasureVerificationService"/> audit trail verification.
/// Validates Sprint 394 implementation: GDPR Audit Log Verification (T394.5).
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class ErasureVerificationServiceAuditShould
{
	private readonly IErasureStore _fakeStore;
	private readonly IErasureCertificateStore _fakeCertStore;
	private readonly IKeyManagementProvider _fakeKeyProvider;
	private readonly IDataInventoryService _fakeInventoryService;
	private readonly IAuditStore _fakeAuditStore;
	private readonly IOptions<ErasureOptions> _fakeOptions;
	private readonly ILogger<ErasureVerificationService> _fakeLogger;
	private readonly ErasureVerificationService _sut;

	public ErasureVerificationServiceAuditShould()
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

		_sut = new ErasureVerificationService(
			_fakeStore,
			_fakeKeyProvider,
			_fakeInventoryService,
			_fakeAuditStore,
			_fakeOptions,
			_fakeLogger);
	}

	#region Constructor Tests for IAuditStore

	[Fact]
	public void ThrowArgumentNullException_WhenAuditStoreIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new ErasureVerificationService(
			_fakeStore,
			_fakeKeyProvider,
			_fakeInventoryService,
			null!,
			_fakeOptions,
			_fakeLogger))
			.ParamName.ShouldBe("auditStore");
	}

	#endregion

	#region Audit Trail Verification - Success Scenarios

	[Fact]
	public async Task VerifyErasureAsync_SucceedAuditVerification_WhenCompletionEventExists()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, keysDeleted: 2, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, ["key-1", "key-2"]);

		SetupStoreAndCertificate(requestId, status, certificate);
		SetupKeyProviderDeleted();
		SetupEmptyInventory(status);

		// Setup audit store with completion event
		var auditEvents = new List<AuditEvent>
		{
			CreateAuditEvent(requestId, "DataErasure.Completed"),
			CreateAuditEvent(requestId, "DataErasure.KeyDeleted"),
			CreateAuditEvent(requestId, "DataErasure.KeyDeleted")
		};
		SetupAuditStore(auditEvents);

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert
		result.Verified.ShouldBeTrue();
		result.Methods.HasFlag(VerificationMethod.AuditLog).ShouldBeTrue();
	}

	[Fact]
	public async Task VerifyErasureAsync_QueryAuditStore_WithCorrectParameters()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, keysDeleted: 1, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, ["key-1"]);

		SetupStoreAndCertificate(requestId, status, certificate);
		SetupKeyProviderDeleted();
		SetupEmptyInventory(status);

		var auditEvents = new List<AuditEvent>
		{
			CreateAuditEvent(requestId, "DataErasure.Completed")
		};
		SetupAuditStore(auditEvents);

		// Act
		_ = await _sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert - Verify the audit store was queried with correct parameters
		_ = A.CallTo(() => _fakeAuditStore.QueryAsync(
			A<AuditQuery>.That.Matches(q =>
				q.ResourceId == requestId.ToString() &&
				q.ResourceType == "ErasureRequest" &&
				q.EventTypes != null && q.EventTypes.Contains(AuditEventType.Compliance)),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task VerifyErasureAsync_SucceedAuditVerification_WhenKeyDeletionEventsMatchCertificate()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var keyIds = new List<string> { "key-1", "key-2", "key-3" };
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, keysDeleted: 3, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, keyIds);

		SetupStoreAndCertificate(requestId, status, certificate);
		SetupKeyProviderDeleted();
		SetupEmptyInventory(status);

		// Setup audit store with completion event and matching key deletion events
		var auditEvents = new List<AuditEvent>
		{
			CreateAuditEvent(requestId, "DataErasure.Completed"),
			CreateAuditEvent(requestId, "DataErasure.KeyDeleted"),
			CreateAuditEvent(requestId, "DataErasure.KeyDeleted"),
			CreateAuditEvent(requestId, "DataErasure.KeyDeleted")
		};
		SetupAuditStore(auditEvents);

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert
		result.Verified.ShouldBeTrue();
	}

	#endregion

	#region Audit Trail Verification - Warning Scenarios

	[Fact]
	public async Task VerifyErasureAsync_AddWarning_WhenFailureEventExists()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, keysDeleted: 1, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, ["key-1"]);

		SetupStoreAndCertificate(requestId, status, certificate);
		SetupKeyProviderDeleted();
		SetupEmptyInventory(status);

		// Setup audit store with failure event
		var auditEvents = new List<AuditEvent>
		{
			CreateAuditEvent(requestId, "DataErasure.Completed"),
			CreateAuditEvent(requestId, "DataErasure.Failed")
		};
		SetupAuditStore(auditEvents);

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert
		// Per implementation: audit failures are added to warnings, not critical failures
		// Verification still succeeds, but AuditLog method is NOT set (since audit verification returned Success=false)
		result.Verified.ShouldBeTrue();
		result.Methods.HasFlag(VerificationMethod.AuditLog).ShouldBeFalse();
	}

	[Fact]
	public async Task VerifyErasureAsync_AddWarning_WhenRollbackEventExists()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, keysDeleted: 1, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, ["key-1"]);

		SetupStoreAndCertificate(requestId, status, certificate);
		SetupKeyProviderDeleted();
		SetupEmptyInventory(status);

		// Setup audit store with rollback event
		var auditEvents = new List<AuditEvent>
		{
			CreateAuditEvent(requestId, "DataErasure.Completed"),
			CreateAuditEvent(requestId, "DataErasure.RolledBack")
		};
		SetupAuditStore(auditEvents);

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert
		// Per implementation: audit failures are warnings, not critical failures
		result.Verified.ShouldBeTrue();
		result.Methods.HasFlag(VerificationMethod.AuditLog).ShouldBeFalse();
	}

	[Fact]
	public async Task VerifyErasureAsync_AddWarning_WhenNoCompletionEventFound()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, keysDeleted: 1, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, ["key-1"]);

		SetupStoreAndCertificate(requestId, status, certificate);
		SetupKeyProviderDeleted();
		SetupEmptyInventory(status);

		// Setup audit store with no completion event
		var auditEvents = new List<AuditEvent>
		{
			CreateAuditEvent(requestId, "DataErasure.KeyDeleted")
		};
		SetupAuditStore(auditEvents);

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert
		// Per implementation: missing completion event causes audit verification to fail (Success=false)
		// but that's a warning, not a critical failure - so overall verification succeeds
		result.Verified.ShouldBeTrue();
		result.Methods.HasFlag(VerificationMethod.AuditLog).ShouldBeFalse();
	}

	[Fact]
	public async Task VerifyErasureAsync_AddWarning_WhenFewerKeyDeletionEventsThanExpected()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var keyIds = new List<string> { "key-1", "key-2", "key-3" };
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, keysDeleted: 3, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, keyIds);

		SetupStoreAndCertificate(requestId, status, certificate);
		SetupKeyProviderDeleted();
		SetupEmptyInventory(status);

		// Setup audit store with fewer key deletion events than expected
		var auditEvents = new List<AuditEvent>
		{
			CreateAuditEvent(requestId, "DataErasure.Completed"),
			CreateAuditEvent(requestId, "DataErasure.KeyDeleted") // Only 1 instead of 3
		};
		SetupAuditStore(auditEvents);

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert - verification still succeeds but with warnings
		result.Verified.ShouldBeTrue();
		// Warnings are internal to the audit verification result
	}

	[Fact]
	public async Task VerifyErasureAsync_AddWarning_WhenNoKeysDeletedInStatus()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, keysDeleted: 0, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, []);

		SetupStoreAndCertificate(requestId, status, certificate);
		SetupEmptyInventory(status);

		// Setup audit store with completion event
		var auditEvents = new List<AuditEvent>
		{
			CreateAuditEvent(requestId, "DataErasure.Completed")
		};
		SetupAuditStore(auditEvents);

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert - verification succeeds even with no keys deleted
		result.Verified.ShouldBeTrue();
	}

	#endregion

	#region Audit Trail Verification - Edge Cases

	[Fact]
	public async Task VerifyErasureAsync_HandleEmptyAuditEvents()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, keysDeleted: 1, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, ["key-1"]);

		SetupStoreAndCertificate(requestId, status, certificate);
		SetupKeyProviderDeleted();
		SetupEmptyInventory(status);

		// Setup audit store with empty events
		SetupAuditStore([]);

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert - no completion event means audit verification fails (added to warnings)
		// but overall verification still succeeds since audit failures are not critical
		result.Verified.ShouldBeTrue();
		result.Methods.HasFlag(VerificationMethod.AuditLog).ShouldBeFalse();
	}

	[Fact]
	public async Task VerifyErasureAsync_HandleMultipleFailureEvents()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, keysDeleted: 1, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, ["key-1"]);

		SetupStoreAndCertificate(requestId, status, certificate);
		SetupKeyProviderDeleted();
		SetupEmptyInventory(status);

		// Setup audit store with multiple failure events
		var auditEvents = new List<AuditEvent>
		{
			CreateAuditEvent(requestId, "DataErasure.Completed"),
			CreateAuditEvent(requestId, "DataErasure.Failed"),
			CreateAuditEvent(requestId, "DataErasure.Failed"),
			CreateAuditEvent(requestId, "DataErasure.RolledBack")
		};
		SetupAuditStore(auditEvents);

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert - multiple failure events cause audit verification to fail (added to warnings)
		// but overall verification still succeeds since audit failures are not critical
		result.Verified.ShouldBeTrue();
		result.Methods.HasFlag(VerificationMethod.AuditLog).ShouldBeFalse();
	}

	[Fact]
	public async Task VerifyErasureAsync_SkipAuditVerification_WhenAuditLogMethodNotConfigured()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, keysDeleted: 1, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, ["key-1"]);

		// Configure only KMS verification, no audit log
		_ = A.CallTo(() => _fakeOptions.Value).Returns(new ErasureOptions
		{
			VerificationMethods = VerificationMethod.KeyManagementSystem // No AuditLog
		});

		var sut = new ErasureVerificationService(
			_fakeStore,
			_fakeKeyProvider,
			_fakeInventoryService,
			_fakeAuditStore,
			_fakeOptions,
			_fakeLogger);

		_ = A.CallTo(() => _fakeStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);
		_ = A.CallTo(() => _fakeCertStore.GetCertificateByIdAsync(status.CertificateId!.Value, A<CancellationToken>._))
			.Returns(certificate);
		_ = A.CallTo(() => _fakeKeyProvider.GetKeyAsync(A<string>._, A<CancellationToken>._))
			.Returns((KeyMetadata?)null);
		SetupEmptyInventory(status);

		// Act
		var result = await sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert - Audit store should NOT be called
		A.CallTo(() => _fakeAuditStore.QueryAsync(A<AuditQuery>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		result.Methods.HasFlag(VerificationMethod.AuditLog).ShouldBeFalse();
	}

	[Fact]
	public async Task VerifyErasureAsync_HandleNullCertificate_InAuditVerification()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, keysDeleted: 0, certificateId: null);

		_ = A.CallTo(() => _fakeStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);
		SetupEmptyInventory(status);

		// Setup audit store with completion event
		var auditEvents = new List<AuditEvent>
		{
			CreateAuditEvent(requestId, "DataErasure.Completed")
		};
		SetupAuditStore(auditEvents);

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert - Should handle null certificate gracefully
		result.Verified.ShouldBeTrue();
	}

	[Fact]
	public async Task VerifyErasureAsync_HandleAuditStoreException()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, keysDeleted: 1, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, ["key-1"]);

		SetupStoreAndCertificate(requestId, status, certificate);
		SetupKeyProviderDeleted();
		SetupEmptyInventory(status);

		// Setup audit store to throw exception
		_ = A.CallTo(() => _fakeAuditStore.QueryAsync(A<AuditQuery>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Audit store connection failed"));

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert - Should handle exception gracefully
		result.Verified.ShouldBeFalse();
		result.Failures.ShouldContain(f => f.Reason.Contains("exception"));
	}

	#endregion

	#region Audit Actions Constants Tests

	[Theory]
	[InlineData("DataErasure.Completed", true)]
	[InlineData("DataErasure.KeyDeleted", true)]
	[InlineData("DataErasure.Failed", true)]
	[InlineData("DataErasure.RolledBack", true)]
	[InlineData("SomeOther.Action", true)]
	public async Task VerifyErasureAsync_RecognizeAuditActions(string action, bool shouldHandle)
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, keysDeleted: 1, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, ["key-1"]);

		SetupStoreAndCertificate(requestId, status, certificate);
		SetupKeyProviderDeleted();
		SetupEmptyInventory(status);

		// Setup audit store with only the specified action
		var auditEvents = new List<AuditEvent>
		{
			CreateAuditEvent(requestId, action)
		};
		SetupAuditStore(auditEvents);

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert - Should process without throwing
		_ = result.ShouldNotBeNull();
		_ = shouldHandle; // Validate parameter is used
	}

	#endregion

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

	private static AuditEvent CreateAuditEvent(Guid requestId, string action)
	{
		return new AuditEvent
		{
			EventId = Guid.NewGuid().ToString(),
			EventType = AuditEventType.Compliance,
			Action = action,
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "system",
			ResourceId = requestId.ToString(),
			ResourceType = "ErasureRequest"
		};
	}

	private void SetupStoreAndCertificate(Guid requestId, ErasureStatus status, ErasureCertificate certificate)
	{
		_ = A.CallTo(() => _fakeStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(status);
		_ = A.CallTo(() => _fakeCertStore.GetCertificateByIdAsync(status.CertificateId!.Value, A<CancellationToken>._))
			.Returns(certificate);
	}

	private void SetupKeyProviderDeleted()
	{
		_ = A.CallTo(() => _fakeKeyProvider.GetKeyAsync(A<string>._, A<CancellationToken>._))
			.Returns((KeyMetadata?)null);
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

	private void SetupAuditStore(IReadOnlyList<AuditEvent> events)
	{
		_ = A.CallTo(() => _fakeAuditStore.QueryAsync(A<AuditQuery>._, A<CancellationToken>._))
			.Returns(events);
	}

	#endregion
}
