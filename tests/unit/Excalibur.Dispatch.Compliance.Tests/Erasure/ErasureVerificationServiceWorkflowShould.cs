using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.Erasure;

/// <summary>
/// Tests the erasure verification service deep workflows including
/// multi-method verification, key deletion confirmation, and report generation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ErasureVerificationServiceWorkflowShould
{
	private readonly IErasureStore _erasureStore = A.Fake<IErasureStore>();
	private readonly IKeyManagementProvider _keyProvider = A.Fake<IKeyManagementProvider>();
	private readonly IDataInventoryService _inventoryService = A.Fake<IDataInventoryService>();
	private readonly IAuditStore _auditStore = A.Fake<IAuditStore>();

	[Fact]
	public async Task Verify_succeeds_when_key_confirmed_deleted()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateCompletedStatus(requestId, keysDeleted: 2);
		A.CallTo(() => _erasureStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(status));
		A.CallTo(() => _erasureStore.GetService(typeof(IErasureCertificateStore)))
			.Returns(null);

		// Key should return null = deleted
		A.CallTo(() => _keyProvider.GetKeyAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(null));

		var sut = CreateService(VerificationMethod.KeyManagementSystem);

		// Act
		var result = await sut.VerifyErasureAsync(requestId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldNotBeNull();
		result.Duration.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public async Task Fail_verification_when_erasure_request_not_found()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		A.CallTo(() => _erasureStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(null));

		var sut = CreateService(VerificationMethod.KeyManagementSystem);

		// Act
		var result = await sut.VerifyErasureAsync(requestId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Verified.ShouldBeFalse();
		result.Failures.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task Fail_verification_when_erasure_not_completed()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.InProgress);
		A.CallTo(() => _erasureStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(status));

		var sut = CreateService(VerificationMethod.KeyManagementSystem);

		// Act
		var result = await sut.VerifyErasureAsync(requestId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Verified.ShouldBeFalse();
	}

	[Fact]
	public async Task Fail_verification_when_scheduled_not_yet_executed()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Scheduled);
		A.CallTo(() => _erasureStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(status));

		var sut = CreateService(VerificationMethod.KeyManagementSystem);

		// Act
		var result = await sut.VerifyErasureAsync(requestId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Verified.ShouldBeFalse();
	}

	[Fact]
	public async Task Handle_store_exception_during_verification()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		A.CallTo(() => _erasureStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Database unavailable"));

		var sut = CreateService(VerificationMethod.KeyManagementSystem);

		// Act
		var result = await sut.VerifyErasureAsync(requestId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert - should handle gracefully, not throw
		result.Verified.ShouldBeFalse();
		result.Failures.ShouldNotBeEmpty();
		result.Failures[0].Reason.ShouldContain("Database unavailable");
	}

	[Fact]
	public async Task Generate_report_for_nonexistent_request()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		A.CallTo(() => _erasureStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(null));

		var sut = CreateService(VerificationMethod.KeyManagementSystem);

		// Act
		var report = await sut.GenerateReportAsync(requestId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		report.ShouldNotBeNull();
		report.RequestId.ShouldBe(requestId);
	}

	[Fact]
	public async Task Generate_report_for_completed_erasure()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateCompletedStatus(requestId, keysDeleted: 3);
		A.CallTo(() => _erasureStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(status));
		A.CallTo(() => _erasureStore.GetService(typeof(IErasureCertificateStore)))
			.Returns(null);

		A.CallTo(() => _keyProvider.GetKeyAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(null));

		var sut = CreateService(VerificationMethod.KeyManagementSystem);

		// Act
		var report = await sut.GenerateReportAsync(requestId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		report.ShouldNotBeNull();
		report.RequestId.ShouldBe(requestId);
	}

	[Fact]
	public async Task Verify_with_audit_log_method()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateCompletedStatus(requestId, keysDeleted: 1);
		A.CallTo(() => _erasureStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(status));
		A.CallTo(() => _erasureStore.GetService(typeof(IErasureCertificateStore)))
			.Returns(null);

		var sut = CreateService(VerificationMethod.AuditLog);

		// Act
		var result = await sut.VerifyErasureAsync(requestId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public async Task Verify_with_combined_methods()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateCompletedStatus(requestId, keysDeleted: 1);
		A.CallTo(() => _erasureStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(status));
		A.CallTo(() => _erasureStore.GetService(typeof(IErasureCertificateStore)))
			.Returns(null);

		A.CallTo(() => _keyProvider.GetKeyAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(null));

		var sut = CreateService(VerificationMethod.KeyManagementSystem | VerificationMethod.AuditLog);

		// Act
		var result = await sut.VerifyErasureAsync(requestId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldNotBeNull();
		result.Duration.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	private static ErasureStatus CreateStatus(Guid requestId, ErasureRequestStatus status) =>
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
		};

	private static ErasureStatus CreateCompletedStatus(Guid requestId, int keysDeleted) =>
		new()
		{
			RequestId = requestId,
			DataSubjectIdHash = "hash-abc123",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			Status = ErasureRequestStatus.Completed,
			RequestedBy = "test-admin",
			RequestedAt = DateTimeOffset.UtcNow.AddHours(-2),
			CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
			UpdatedAt = DateTimeOffset.UtcNow,
			KeysDeleted = keysDeleted,
		};

	private ErasureVerificationService CreateService(VerificationMethod methods) =>
		new(
			_erasureStore,
			_keyProvider,
			_inventoryService,
			_auditStore,
			Microsoft.Extensions.Options.Options.Create(new ErasureOptions
			{
				VerificationMethods = methods,
			}),
			NullLogger<ErasureVerificationService>.Instance);
}
