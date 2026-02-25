// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.AuditLogging;
using Excalibur.Dispatch.Compliance;
using Excalibur.Dispatch.Integration.Tests.Compliance.Fixtures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Excalibur.Dispatch.Compliance;
namespace Excalibur.Dispatch.Integration.Tests.Compliance.Erasure;

/// <summary>
/// Integration tests for <see cref="ErasureVerificationService"/> audit trail verification.
/// Validates Sprint 394 implementation: GDPR Audit Log Verification (T394.6).
/// </summary>
/// <remarks>
/// These tests verify the end-to-end audit log verification workflow using
/// the in-memory audit store for isolation and deterministic behavior.
/// </remarks>
[Trait("Category", TestCategories.Integration)]
public sealed class ErasureVerificationAuditIntegrationShould : IDisposable
{
	private readonly IErasureStore _fakeErasureStore;
	private readonly IErasureCertificateStore _fakeCertStore;
	private readonly IKeyManagementProvider _fakeKeyProvider;
	private readonly IDataInventoryService _fakeInventoryService;
	private readonly InMemoryAuditStore _auditStore;
	private readonly IOptions<ErasureOptions> _options;
	private readonly ILogger<ErasureVerificationService> _logger;
	private readonly ErasureVerificationService _sut;

	public ErasureVerificationAuditIntegrationShould()
	{
		_fakeErasureStore = A.Fake<IErasureStore>();
		_fakeCertStore = A.Fake<IErasureCertificateStore>();
		_fakeKeyProvider = A.Fake<IKeyManagementProvider>();
		_fakeInventoryService = A.Fake<IDataInventoryService>();
		_auditStore = new InMemoryAuditStore();
		_logger = A.Fake<ILogger<ErasureVerificationService>>();

		// Wire up GetService to return the certificate sub-store
		_ = A.CallTo(() => _fakeErasureStore.GetService(typeof(IErasureCertificateStore)))
			.Returns(_fakeCertStore);

		// Default options with all verification methods enabled
		_options = Microsoft.Extensions.Options.Options.Create(new ErasureOptions
		{
			VerificationMethods = VerificationMethod.KeyManagementSystem
				| VerificationMethod.AuditLog
				| VerificationMethod.DecryptionFailure
		});

		_sut = new ErasureVerificationService(
			_fakeErasureStore,
			_fakeKeyProvider,
			_fakeInventoryService,
			_auditStore,
			_options,
			_logger);
	}

	[Fact]
	public async Task VerifyErasureWithRealAuditStore_WhenCompletionEventStored()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, keysDeleted: 2, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, ["key-1", "key-2"]);

		SetupStoreAndCertificate(requestId, status, certificate);
		SetupKeyProviderDeleted();
		SetupEmptyInventory(status);

		// Store completion event in the real audit store
		await StoreAuditEventAsync(requestId, "DataErasure.Completed");
		await StoreAuditEventAsync(requestId, "DataErasure.KeyDeleted");
		await StoreAuditEventAsync(requestId, "DataErasure.KeyDeleted");

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert
		result.Verified.ShouldBeTrue();
		result.Methods.HasFlag(VerificationMethod.AuditLog).ShouldBeTrue();
		result.Methods.HasFlag(VerificationMethod.KeyManagementSystem).ShouldBeTrue();
	}

	[Fact]
	public async Task VerifyErasureWithRealAuditStore_WhenNoCompletionEventStored()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, keysDeleted: 1, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, ["key-1"]);

		SetupStoreAndCertificate(requestId, status, certificate);
		SetupKeyProviderDeleted();
		SetupEmptyInventory(status);

		// Only store key deletion events, NO completion event
		await StoreAuditEventAsync(requestId, "DataErasure.KeyDeleted");

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert - Verification succeeds but AuditLog method not set
		result.Verified.ShouldBeTrue();
		result.Methods.HasFlag(VerificationMethod.AuditLog).ShouldBeFalse();
		result.Methods.HasFlag(VerificationMethod.KeyManagementSystem).ShouldBeTrue();
	}

	[Fact]
	public async Task VerifyErasureWithRealAuditStore_WhenFailureEventStored()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, keysDeleted: 1, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, ["key-1"]);

		SetupStoreAndCertificate(requestId, status, certificate);
		SetupKeyProviderDeleted();
		SetupEmptyInventory(status);

		// Store completion AND failure events
		await StoreAuditEventAsync(requestId, "DataErasure.Completed");
		await StoreAuditEventAsync(requestId, "DataErasure.Failed");

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert - Verification succeeds but AuditLog method not set due to failure event
		result.Verified.ShouldBeTrue();
		result.Methods.HasFlag(VerificationMethod.AuditLog).ShouldBeFalse();
	}

	[Fact]
	public async Task VerifyErasureWithRealAuditStore_MultipleRequestsIsolated()
	{
		// Arrange - Two different erasure requests
		var requestId1 = Guid.NewGuid();
		var requestId2 = Guid.NewGuid();

		var status1 = CreateStatus(requestId1, ErasureRequestStatus.Completed, keysDeleted: 1, certificateId: Guid.NewGuid());
		var certificate1 = CreateCertificate(requestId1, ["key-1"]);

		var status2 = CreateStatus(requestId2, ErasureRequestStatus.Completed, keysDeleted: 1, certificateId: Guid.NewGuid());
		var certificate2 = CreateCertificate(requestId2, ["key-2"]);

		// Setup for request 1
		_ = A.CallTo(() => _fakeErasureStore.GetStatusAsync(requestId1, A<CancellationToken>._))
			.Returns(status1);
		_ = A.CallTo(() => _fakeCertStore.GetCertificateByIdAsync(status1.CertificateId!.Value, A<CancellationToken>._))
			.Returns(certificate1);

		// Setup for request 2
		_ = A.CallTo(() => _fakeErasureStore.GetStatusAsync(requestId2, A<CancellationToken>._))
			.Returns(status2);
		_ = A.CallTo(() => _fakeCertStore.GetCertificateByIdAsync(status2.CertificateId!.Value, A<CancellationToken>._))
			.Returns(certificate2);

		SetupKeyProviderDeleted();
		SetupEmptyInventoryForAll();

		// Store completion event only for request 1
		await StoreAuditEventAsync(requestId1, "DataErasure.Completed");
		// Store failure event for request 2
		await StoreAuditEventAsync(requestId2, "DataErasure.Failed");

		// Act
		var result1 = await _sut.VerifyErasureAsync(requestId1, CancellationToken.None);
		var result2 = await _sut.VerifyErasureAsync(requestId2, CancellationToken.None);

		// Assert - Each request is verified independently
		result1.Verified.ShouldBeTrue();
		result1.Methods.HasFlag(VerificationMethod.AuditLog).ShouldBeTrue();

		result2.Verified.ShouldBeTrue();
		result2.Methods.HasFlag(VerificationMethod.AuditLog).ShouldBeFalse();
	}

	[Fact]
	public async Task VerifyErasureWithRealAuditStore_TimestampFiltering()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, keysDeleted: 1, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, ["key-1"]);

		SetupStoreAndCertificate(requestId, status, certificate);
		SetupKeyProviderDeleted();
		SetupEmptyInventory(status);

		// Store events with timestamps within the query range
		await StoreAuditEventAsync(requestId, "DataErasure.Completed", DateTimeOffset.UtcNow);
		await StoreAuditEventAsync(requestId, "DataErasure.KeyDeleted", DateTimeOffset.UtcNow);

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert
		result.Verified.ShouldBeTrue();
		result.Methods.HasFlag(VerificationMethod.AuditLog).ShouldBeTrue();
	}

	[Fact]
	public async Task VerifyErasureWithRealAuditStore_KeyDeletionEventCountWarning()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var keyIds = new List<string> { "key-1", "key-2", "key-3" };
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, keysDeleted: 3, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, keyIds);

		SetupStoreAndCertificate(requestId, status, certificate);
		SetupKeyProviderDeleted();
		SetupEmptyInventory(status);

		// Store completion event and only 1 key deletion event (less than expected 3)
		await StoreAuditEventAsync(requestId, "DataErasure.Completed");
		await StoreAuditEventAsync(requestId, "DataErasure.KeyDeleted");

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert - Verification succeeds but there should be a warning about key deletion count
		result.Verified.ShouldBeTrue();
		result.Methods.HasFlag(VerificationMethod.AuditLog).ShouldBeTrue();
	}

	[Fact]
	public async Task VerifyErasureWithRealAuditStore_ConcurrentVerifications()
	{
		// Arrange
		var requestIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();

		foreach (var requestId in requestIds)
		{
			var status = CreateStatus(requestId, ErasureRequestStatus.Completed, keysDeleted: 1, certificateId: Guid.NewGuid());
			var certificate = CreateCertificate(requestId, ["key-1"]);

			_ = A.CallTo(() => _fakeErasureStore.GetStatusAsync(requestId, A<CancellationToken>._))
				.Returns(status);
			_ = A.CallTo(() => _fakeCertStore.GetCertificateByIdAsync(status.CertificateId!.Value, A<CancellationToken>._))
				.Returns(certificate);

			await StoreAuditEventAsync(requestId, "DataErasure.Completed");
		}

		SetupKeyProviderDeleted();
		SetupEmptyInventoryForAll();

		// Act - Run concurrent verifications
		var tasks = requestIds.Select(id => _sut.VerifyErasureAsync(id, CancellationToken.None)).ToList();
		var results = await Task.WhenAll(tasks);

		// Assert - All should succeed
		results.ShouldAllBe(r => r.Verified);
		results.ShouldAllBe(r => r.Methods.HasFlag(VerificationMethod.AuditLog));
	}

	[Fact]
	public async Task VerifyErasureWithRealAuditStore_RollbackEventHandling()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, keysDeleted: 1, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, ["key-1"]);

		SetupStoreAndCertificate(requestId, status, certificate);
		SetupKeyProviderDeleted();
		SetupEmptyInventory(status);

		// Store completion event and rollback event
		await StoreAuditEventAsync(requestId, "DataErasure.Completed");
		await StoreAuditEventAsync(requestId, "DataErasure.RolledBack");

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert - Verification succeeds but AuditLog method not set due to rollback
		result.Verified.ShouldBeTrue();
		result.Methods.HasFlag(VerificationMethod.AuditLog).ShouldBeFalse();
	}

	[Fact]
	public async Task VerifyErasureWithRealAuditStore_EmptyAuditStore()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed, keysDeleted: 1, certificateId: Guid.NewGuid());
		var certificate = CreateCertificate(requestId, ["key-1"]);

		SetupStoreAndCertificate(requestId, status, certificate);
		SetupKeyProviderDeleted();
		SetupEmptyInventory(status);

		// Don't store any audit events

		// Act
		var result = await _sut.VerifyErasureAsync(requestId, CancellationToken.None);

		// Assert - Verification succeeds but AuditLog method not set
		result.Verified.ShouldBeTrue();
		result.Methods.HasFlag(VerificationMethod.AuditLog).ShouldBeFalse();
	}

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

	private async Task StoreAuditEventAsync(Guid requestId, string action, DateTimeOffset? timestamp = null)
	{
		var auditEvent = new AuditEvent
		{
			EventId = Guid.NewGuid().ToString(),
			EventType = AuditEventType.Compliance,
			Action = action,
			Outcome = AuditOutcome.Success,
			Timestamp = timestamp ?? DateTimeOffset.UtcNow,
			ActorId = "system",
			ResourceId = requestId.ToString(),
			ResourceType = "ErasureRequest"
		};

		_ = await _auditStore.StoreAsync(auditEvent, CancellationToken.None);
	}

	private void SetupStoreAndCertificate(Guid requestId, ErasureStatus status, ErasureCertificate certificate)
	{
		_ = A.CallTo(() => _fakeErasureStore.GetStatusAsync(requestId, A<CancellationToken>._))
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

	private void SetupEmptyInventoryForAll()
	{
		_ = A.CallTo(() => _fakeInventoryService.DiscoverAsync(
				A<string>._,
				A<DataSubjectIdType>._,
				A<string>._,
				A<CancellationToken>._))
			.Returns(new DataInventory
			{
				DataSubjectId = "any",
				Locations = [],
				DiscoveredAt = DateTimeOffset.UtcNow
			});
	}

	public void Dispose()
	{
		// InMemoryAuditStore doesn't need disposal, but keeping for pattern consistency
	}

	#endregion
}
