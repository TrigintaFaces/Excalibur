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

	private ErasureVerificationService CreateService() =>
		new(
			_erasureStore,
			_keyProvider,
			_inventoryService,
			_auditStore,
			Microsoft.Extensions.Options.Options.Create(_erasureOptions),
			_logger);
}
