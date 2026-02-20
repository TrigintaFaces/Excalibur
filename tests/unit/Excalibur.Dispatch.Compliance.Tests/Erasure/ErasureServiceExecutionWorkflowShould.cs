using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.Erasure;

/// <summary>
/// Tests the full erasure execution workflow including contributors,
/// concurrent execution guards, and key deletion error handling.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ErasureServiceExecutionWorkflowShould
{
	private readonly IErasureStore _store = A.Fake<IErasureStore>();
	private readonly IKeyManagementProvider _keyProvider = A.Fake<IKeyManagementProvider>();
	private readonly ILegalHoldService _legalHoldService = A.Fake<ILegalHoldService>();
	private readonly IDataInventoryService _dataInventoryService = A.Fake<IDataInventoryService>();

	private ErasureService CreateSut(
		IEnumerable<IErasureContributor>? contributors = null)
	{
		var options = Microsoft.Extensions.Options.Options.Create(new ErasureOptions
		{
			EnableAutoDiscovery = true,
		});
		var signingOptions = Microsoft.Extensions.Options.Options.Create(new ErasureSigningOptions
		{
			SigningKey = new byte[32],
		});

		return new ErasureService(
			_store,
			_keyProvider,
			options,
			signingOptions,
			NullLogger<ErasureService>.Instance,
			_legalHoldService,
			_dataInventoryService,
			contributors);
	}

	[Fact]
	public async Task Execute_erasure_with_multiple_contributors()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Scheduled);

		var contributor1 = A.Fake<IErasureContributor>();
		A.CallTo(() => contributor1.Name).Returns("EventStore");
		A.CallTo(() => contributor1.EraseAsync(A<ErasureContributorContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new ErasureContributorResult { Success = true, RecordsAffected = 10 }));

		var contributor2 = A.Fake<IErasureContributor>();
		A.CallTo(() => contributor2.Name).Returns("SnapshotStore");
		A.CallTo(() => contributor2.EraseAsync(A<ErasureContributorContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new ErasureContributorResult { Success = true, RecordsAffected = 3 }));

		SetupScheduledExecution(requestId, status);
		SetupNoLegalHolds();

		var sut = CreateSut([contributor1, contributor2]);

		// Act
		var result = await sut.ExecuteAsync(requestId, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		result.RecordsAffected.ShouldBe(13);
		A.CallTo(() => contributor1.EraseAsync(A<ErasureContributorContext>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => contributor2.EraseAsync(A<ErasureContributorContext>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Continue_execution_when_contributor_fails()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Scheduled);

		var failingContributor = A.Fake<IErasureContributor>();
		A.CallTo(() => failingContributor.Name).Returns("FailingStore");
		A.CallTo(() => failingContributor.EraseAsync(A<ErasureContributorContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new ErasureContributorResult
			{
				Success = false,
				ErrorMessage = "Connection refused"
			}));

		var successContributor = A.Fake<IErasureContributor>();
		A.CallTo(() => successContributor.Name).Returns("SuccessStore");
		A.CallTo(() => successContributor.EraseAsync(A<ErasureContributorContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new ErasureContributorResult { Success = true, RecordsAffected = 5 }));

		SetupScheduledExecution(requestId, status);
		SetupNoLegalHolds();

		var sut = CreateSut([failingContributor, successContributor]);

		// Act
		var result = await sut.ExecuteAsync(requestId, CancellationToken.None);

		// Assert - execution continues despite contributor failure
		result.Success.ShouldBeTrue();
		result.RecordsAffected.ShouldBe(5);
	}

	[Fact]
	public async Task Continue_execution_when_contributor_throws()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Scheduled);

		var throwingContributor = A.Fake<IErasureContributor>();
		A.CallTo(() => throwingContributor.Name).Returns("ThrowingStore");
		A.CallTo(() => throwingContributor.EraseAsync(A<ErasureContributorContext>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Unexpected error"));

		SetupScheduledExecution(requestId, status);
		SetupNoLegalHolds();

		var sut = CreateSut([throwingContributor]);

		// Act
		var result = await sut.ExecuteAsync(requestId, CancellationToken.None);

		// Assert - execution still completes despite exception
		result.Success.ShouldBeTrue();
	}

	[Fact]
	public async Task Fail_execution_when_concurrent_claim_detected()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Scheduled);

		A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(status));
		// Simulate another caller already claimed this request
		A.CallTo(() => _store.UpdateStatusAsync(requestId, ErasureRequestStatus.InProgress, null, A<CancellationToken>._))
			.Returns(Task.FromResult(false));
		SetupNoLegalHolds();

		var sut = CreateSut();

		// Act
		var result = await sut.ExecuteAsync(requestId, CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
	}

	[Fact]
	public async Task Delete_multiple_keys_discovered_from_inventory()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Scheduled);

		var inventory = new DataInventory
		{
			DataSubjectId = "hash",
			Locations = [],
			AssociatedKeys =
			[
				new KeyReference { KeyId = "key-a", KeyScope = EncryptionKeyScope.User },
				new KeyReference { KeyId = "key-b", KeyScope = EncryptionKeyScope.User },
				new KeyReference { KeyId = "key-c", KeyScope = EncryptionKeyScope.Tenant },
			],
		};

		SetupScheduledExecution(requestId, status);
		A.CallTo(() => _dataInventoryService.DiscoverAsync(
				A<string>._, DataSubjectIdType.Hash, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(inventory));
		A.CallTo(() => _keyProvider.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
		SetupNoLegalHolds();

		var sut = CreateSut();

		// Act
		var result = await sut.ExecuteAsync(requestId, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		result.KeysDeleted.ShouldBe(3);
		A.CallTo(() => _keyProvider.DeleteKeyAsync("key-a", A<int>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _keyProvider.DeleteKeyAsync("key-b", A<int>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _keyProvider.DeleteKeyAsync("key-c", A<int>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Handle_partial_key_deletion_failure()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Scheduled);

		var inventory = new DataInventory
		{
			DataSubjectId = "hash",
			Locations = [],
			AssociatedKeys =
			[
				new KeyReference { KeyId = "key-ok", KeyScope = EncryptionKeyScope.User },
				new KeyReference { KeyId = "key-fail", KeyScope = EncryptionKeyScope.User },
			],
		};

		SetupScheduledExecution(requestId, status);
		A.CallTo(() => _dataInventoryService.DiscoverAsync(
				A<string>._, DataSubjectIdType.Hash, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(inventory));
		A.CallTo(() => _keyProvider.DeleteKeyAsync("key-ok", A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
		A.CallTo(() => _keyProvider.DeleteKeyAsync("key-fail", A<int>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("KMS timeout"));
		SetupNoLegalHolds();

		var sut = CreateSut();

		// Act
		var result = await sut.ExecuteAsync(requestId, CancellationToken.None);

		// Assert - execution succeeds with partial deletion
		result.Success.ShouldBeTrue();
		result.KeysDeleted.ShouldBe(1);
	}

	[Fact]
	public async Task Generate_certificate_for_completed_erasure()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = new ErasureStatus
		{
			RequestId = requestId,
			DataSubjectIdHash = "abc123hash",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			Status = ErasureRequestStatus.Completed,
			RequestedAt = DateTimeOffset.UtcNow.AddHours(-1),
			CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
			RequestedBy = "admin",
			UpdatedAt = DateTimeOffset.UtcNow,
			KeysDeleted = 2,
			RecordsAffected = 5,
		};

		var certStore = A.Fake<IErasureCertificateStore>();
		A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(status));
		A.CallTo(() => _store.GetService(typeof(IErasureCertificateStore)))
			.Returns(certStore);
		A.CallTo(() => certStore.GetCertificateAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureCertificate?>(null));

		var sut = CreateSut();

		// Act
		var cert = await sut.GenerateCertificateAsync(requestId, CancellationToken.None);

		// Assert
		cert.ShouldNotBeNull();
		cert.RequestId.ShouldBe(requestId);
		cert.Method.ShouldBe(ErasureMethod.CryptographicErasure);
		cert.Signature.ShouldNotBeNullOrWhiteSpace();
		cert.Summary.ShouldNotBeNull();
		A.CallTo(() => certStore.SaveCertificateAsync(A<ErasureCertificate>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Return_existing_certificate_if_already_generated()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = new ErasureStatus
		{
			RequestId = requestId,
			DataSubjectIdHash = "abc123hash",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			Status = ErasureRequestStatus.Completed,
			RequestedAt = DateTimeOffset.UtcNow.AddHours(-1),
			CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
			RequestedBy = "admin",
			UpdatedAt = DateTimeOffset.UtcNow,
		};

		var existingCert = new ErasureCertificate
		{
			CertificateId = Guid.NewGuid(),
			RequestId = requestId,
			DataSubjectReference = "hash-abc123",
			RequestReceivedAt = DateTimeOffset.UtcNow.AddHours(-1),
			CompletedAt = DateTimeOffset.UtcNow,
			Method = ErasureMethod.CryptographicErasure,
			Summary = new ErasureSummary(),
			Verification = new VerificationSummary
			{
				Verified = true,
				Methods = VerificationMethod.KeyManagementSystem,
				VerifiedAt = DateTimeOffset.UtcNow,
			},
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			Signature = "existing-sig",
			RetainUntil = DateTimeOffset.UtcNow.AddYears(7),
		};

		var certStore = A.Fake<IErasureCertificateStore>();
		A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(status));
		A.CallTo(() => _store.GetService(typeof(IErasureCertificateStore)))
			.Returns(certStore);
		A.CallTo(() => certStore.GetCertificateAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureCertificate?>(existingCert));

		var sut = CreateSut();

		// Act
		var cert = await sut.GenerateCertificateAsync(requestId, CancellationToken.None);

		// Assert - should return existing, not create new
		cert.ShouldBeSameAs(existingCert);
		A.CallTo(() => certStore.SaveCertificateAsync(A<ErasureCertificate>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task Throw_when_generating_certificate_for_non_executed_request()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Scheduled);

		A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(status));

		var sut = CreateSut();

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => sut.GenerateCertificateAsync(requestId, CancellationToken.None));
	}

	[Fact]
	public async Task Throw_when_generating_certificate_for_unknown_request()
	{
		// Arrange
		A.CallTo(() => _store.GetStatusAsync(A<Guid>._, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(null));

		var sut = CreateSut();

		// Act & Assert
		await Should.ThrowAsync<KeyNotFoundException>(
			() => sut.GenerateCertificateAsync(Guid.NewGuid(), CancellationToken.None));
	}

	[Fact]
	public async Task Clamp_grace_period_to_maximum()
	{
		// Arrange
		var request = new ErasureRequest
		{
			DataSubjectId = "user-1",
			IdType = DataSubjectIdType.UserId,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			RequestedBy = "admin",
			GracePeriodOverride = TimeSpan.FromDays(365), // way over max
		};
		SetupNoLegalHolds();

		DateTimeOffset capturedScheduledTime = default;
		A.CallTo(() => _store.SaveRequestAsync(request, A<DateTimeOffset>._, A<CancellationToken>._))
			.Invokes(call => capturedScheduledTime = call.GetArgument<DateTimeOffset>(1))
			.Returns(Task.CompletedTask);

		var sut = CreateSut();

		// Act
		await sut.RequestErasureAsync(request, CancellationToken.None);

		// Assert - should be clamped to default max (30 days)
		var maxGracePeriod = new ErasureOptions().MaximumGracePeriod;
		var expectedMin = DateTimeOffset.UtcNow.Add(maxGracePeriod).AddMinutes(-5);
		var expectedMax = DateTimeOffset.UtcNow.Add(maxGracePeriod).AddMinutes(5);
		capturedScheduledTime.ShouldBeGreaterThan(expectedMin);
		capturedScheduledTime.ShouldBeLessThan(expectedMax);
	}

	private static ErasureStatus CreateStatus(Guid requestId, ErasureRequestStatus status) =>
		new()
		{
			RequestId = requestId,
			DataSubjectIdHash = "abc123hash",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			Status = status,
			RequestedAt = DateTimeOffset.UtcNow.AddHours(-1),
			RequestedBy = "admin",
			UpdatedAt = DateTimeOffset.UtcNow,
		};

	private void SetupScheduledExecution(Guid requestId, ErasureStatus status)
	{
		A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(status));
		A.CallTo(() => _store.UpdateStatusAsync(requestId, ErasureRequestStatus.InProgress, null, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
		A.CallTo(() => _dataInventoryService.DiscoverAsync(
				A<string>._, DataSubjectIdType.Hash, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new DataInventory
			{
				DataSubjectId = "hash",
				Locations = [],
				AssociatedKeys = [],
			}));
	}

	private void SetupNoLegalHolds()
	{
		A.CallTo(() => _legalHoldService.CheckHoldsAsync(
				A<string>._, A<DataSubjectIdType>._, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new LegalHoldCheckResult
			{
				HasActiveHolds = false,
				ActiveHolds = [],
			}));
	}
}
