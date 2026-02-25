using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.Erasure;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ErasureServiceShould
{
	private readonly IErasureStore _store = A.Fake<IErasureStore>();
	private readonly IKeyManagementProvider _keyProvider = A.Fake<IKeyManagementProvider>();
	private readonly ILegalHoldService _legalHoldService = A.Fake<ILegalHoldService>();
	private readonly IDataInventoryService _dataInventoryService = A.Fake<IDataInventoryService>();
	private readonly ErasureService _sut;

	public ErasureServiceShould()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new ErasureOptions());
		var signingOptions = Microsoft.Extensions.Options.Options.Create(new ErasureSigningOptions
		{
			SigningKey = new byte[32],
		});

		_sut = new ErasureService(
			_store,
			_keyProvider,
			options,
			signingOptions,
			NullLogger<ErasureService>.Instance,
			_legalHoldService,
			_dataInventoryService);
	}

	[Fact]
	public async Task Schedule_erasure_for_valid_request()
	{
		// Arrange
		var request = CreateValidRequest();
		SetupNoLegalHolds();

		// Act
		var result = await _sut.RequestErasureAsync(request, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.RequestId.ShouldBe(request.RequestId);
		result.Status.ShouldBe(ErasureRequestStatus.Scheduled);
		result.ScheduledExecutionTime.ShouldNotBeNull();
		A.CallTo(() => _store.SaveRequestAsync(request, A<DateTimeOffset>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Block_erasure_when_legal_hold_exists()
	{
		// Arrange
		var request = CreateValidRequest();
		var holdInfo = new LegalHoldInfo
		{
			HoldId = Guid.NewGuid(),
			Basis = LegalHoldBasis.LitigationHold,
			CaseReference = "CASE-001",
			CreatedAt = DateTimeOffset.UtcNow,
		};

		A.CallTo(() => _legalHoldService.CheckHoldsAsync(
				request.DataSubjectId, request.IdType, request.TenantId, A<CancellationToken>._))
			.Returns(Task.FromResult(new LegalHoldCheckResult
			{
				HasActiveHolds = true,
				ActiveHolds = [holdInfo],
			}));

		// Act
		var result = await _sut.RequestErasureAsync(request, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Status.ShouldBe(ErasureRequestStatus.BlockedByLegalHold);
		result.BlockingHold.ShouldNotBeNull();
		result.BlockingHold!.HoldId.ShouldBe(holdInfo.HoldId);
		A.CallTo(() => _store.SaveRequestAsync(A<ErasureRequest>._, A<DateTimeOffset>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task Throw_when_data_subject_id_is_empty()
	{
		// Arrange
		var request = new ErasureRequest
		{
			DataSubjectId = "",
			IdType = DataSubjectIdType.UserId,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			RequestedBy = "admin",
		};
		SetupNoLegalHolds();

		// Act & Assert
		await Should.ThrowAsync<ErasureOperationException>(
			() => _sut.RequestErasureAsync(request, CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_when_requested_by_is_empty()
	{
		// Arrange
		var request = new ErasureRequest
		{
			DataSubjectId = "user-1",
			IdType = DataSubjectIdType.UserId,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			RequestedBy = "",
		};
		SetupNoLegalHolds();

		// Act & Assert
		await Should.ThrowAsync<ErasureOperationException>(
			() => _sut.RequestErasureAsync(request, CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_when_tenant_scope_without_tenant_id()
	{
		// Arrange
		var request = new ErasureRequest
		{
			DataSubjectId = "user-1",
			IdType = DataSubjectIdType.UserId,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			RequestedBy = "admin",
			Scope = ErasureScope.Tenant,
			TenantId = null,
		};
		SetupNoLegalHolds();

		// Act & Assert
		await Should.ThrowAsync<ErasureOperationException>(
			() => _sut.RequestErasureAsync(request, CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_when_selective_scope_without_categories()
	{
		// Arrange
		var request = new ErasureRequest
		{
			DataSubjectId = "user-1",
			IdType = DataSubjectIdType.UserId,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			RequestedBy = "admin",
			Scope = ErasureScope.Selective,
			DataCategories = null,
		};
		SetupNoLegalHolds();

		// Act & Assert
		await Should.ThrowAsync<ErasureOperationException>(
			() => _sut.RequestErasureAsync(request, CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_on_null_request()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.RequestErasureAsync(null!, CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Get_status_from_store()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var expected = CreateStatus(requestId, ErasureRequestStatus.Scheduled);

		A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(expected));

		// Act
		var result = await _sut.GetStatusAsync(requestId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldNotBeNull();
		result!.RequestId.ShouldBe(requestId);
	}

	[Fact]
	public async Task Return_null_when_status_not_found()
	{
		// Arrange
		A.CallTo(() => _store.GetStatusAsync(A<Guid>._, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(null));

		// Act
		var result = await _sut.GetStatusAsync(Guid.NewGuid(), CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task Cancel_scheduled_erasure()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Scheduled);

		A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(status));
		A.CallTo(() => _store.RecordCancellationAsync(requestId, "reason", "admin", A<CancellationToken>._))
			.Returns(Task.FromResult(true));

		// Act
		var result = await _sut.CancelErasureAsync(requestId, "reason", "admin", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task Return_false_when_cancelling_nonexistent_request()
	{
		// Arrange
		A.CallTo(() => _store.GetStatusAsync(A<Guid>._, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(null));

		// Act
		var result = await _sut.CancelErasureAsync(Guid.NewGuid(), "reason", "admin", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task Throw_when_cancelling_completed_erasure()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed);

		A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(status));

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.CancelErasureAsync(requestId, "reason", "admin", CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_on_null_reason_for_cancel()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.CancelErasureAsync(Guid.NewGuid(), null!, "admin", CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_on_null_cancelled_by_for_cancel()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.CancelErasureAsync(Guid.NewGuid(), "reason", null!, CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Execute_erasure_deleting_keys()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Scheduled);

		A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(status));
		A.CallTo(() => _store.UpdateStatusAsync(requestId, ErasureRequestStatus.InProgress, null, A<CancellationToken>._))
			.Returns(Task.FromResult(true));

		var inventory = new DataInventory
		{
			DataSubjectId = "abc123hash",
			Locations = [],
			AssociatedKeys =
			[
				new KeyReference { KeyId = "key-1", KeyScope = EncryptionKeyScope.User },
				new KeyReference { KeyId = "key-2", KeyScope = EncryptionKeyScope.User },
			],
		};

		A.CallTo(() => _dataInventoryService.DiscoverAsync(
				A<string>._, DataSubjectIdType.Hash, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(inventory));
		A.CallTo(() => _keyProvider.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));

		SetupNoLegalHolds();

		// Act
		var result = await _sut.ExecuteAsync(requestId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();
		result.KeysDeleted.ShouldBe(2);
		A.CallTo(() => _store.RecordCompletionAsync(requestId, 2, 0, A<Guid>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Fail_execution_when_request_not_found()
	{
		// Arrange
		A.CallTo(() => _store.GetStatusAsync(A<Guid>._, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(null));

		// Act
		var result = await _sut.ExecuteAsync(Guid.NewGuid(), CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeFalse();
	}

	[Fact]
	public async Task Fail_execution_when_status_is_not_scheduled()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Completed);

		A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(status));

		// Act
		var result = await _sut.ExecuteAsync(requestId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeFalse();
	}

	[Fact]
	public async Task Block_execution_when_legal_hold_active()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Scheduled);

		A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(status));

		A.CallTo(() => _legalHoldService.CheckHoldsAsync(
				A<string>._, DataSubjectIdType.Hash, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new LegalHoldCheckResult
			{
				HasActiveHolds = true,
				ActiveHolds =
				[
					new LegalHoldInfo
					{
						HoldId = Guid.NewGuid(),
						Basis = LegalHoldBasis.LitigationHold,
						CaseReference = "CASE-999",
						CreatedAt = DateTimeOffset.UtcNow,
					}
				],
			}));

		// Act
		var result = await _sut.ExecuteAsync(requestId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeFalse();
		A.CallTo(() => _store.UpdateStatusAsync(requestId, ErasureRequestStatus.BlockedByLegalHold, A<string>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Use_grace_period_override_when_provided()
	{
		// Arrange
		var request = new ErasureRequest
		{
			DataSubjectId = "user-1",
			IdType = DataSubjectIdType.UserId,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			RequestedBy = "admin",
			GracePeriodOverride = TimeSpan.FromHours(12),
		};
		SetupNoLegalHolds();

		DateTimeOffset capturedScheduledTime = default;
		A.CallTo(() => _store.SaveRequestAsync(request, A<DateTimeOffset>._, A<CancellationToken>._))
			.Invokes(call => capturedScheduledTime = call.GetArgument<DateTimeOffset>(1))
			.Returns(Task.CompletedTask);

		// Act
		await _sut.RequestErasureAsync(request, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert - scheduled time should be ~12h from now
		var expectedMin = DateTimeOffset.UtcNow.AddHours(11);
		var expectedMax = DateTimeOffset.UtcNow.AddHours(13);
		capturedScheduledTime.ShouldBeGreaterThan(expectedMin);
		capturedScheduledTime.ShouldBeLessThan(expectedMax);
	}

	[Fact]
	public async Task Clamp_grace_period_to_minimum()
	{
		// Arrange - minimum is 1 hour, override is 1 minute
		var request = new ErasureRequest
		{
			DataSubjectId = "user-1",
			IdType = DataSubjectIdType.UserId,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			RequestedBy = "admin",
			GracePeriodOverride = TimeSpan.FromMinutes(1),
		};
		SetupNoLegalHolds();

		DateTimeOffset capturedScheduledTime = default;
		A.CallTo(() => _store.SaveRequestAsync(request, A<DateTimeOffset>._, A<CancellationToken>._))
			.Invokes(call => capturedScheduledTime = call.GetArgument<DateTimeOffset>(1))
			.Returns(Task.CompletedTask);

		// Act
		await _sut.RequestErasureAsync(request, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert - should be clamped to minimum 1 hour
		var expectedMin = DateTimeOffset.UtcNow.AddMinutes(50);
		var expectedMax = DateTimeOffset.UtcNow.AddMinutes(70);
		capturedScheduledTime.ShouldBeGreaterThan(expectedMin);
		capturedScheduledTime.ShouldBeLessThan(expectedMax);
	}

	[Fact]
	public async Task Discover_data_inventory_when_service_available()
	{
		// Arrange
		var request = CreateValidRequest();
		SetupNoLegalHolds();

		var inventory = new DataInventory
		{
			DataSubjectId = "hash-user-123",
			Locations =
			[
				new DataLocation
				{
					TableName = "Users",
					FieldName = "Email",
					DataCategory = "PII",
					RecordId = "rec-1",
					KeyId = "key-1",
				}
			],
			AssociatedKeys = [new KeyReference { KeyId = "key-1", KeyScope = EncryptionKeyScope.User }],
		};

		A.CallTo(() => _dataInventoryService.DiscoverAsync(
				request.DataSubjectId, request.IdType, request.TenantId, A<CancellationToken>._))
			.Returns(Task.FromResult(inventory));

		// Act
		var result = await _sut.RequestErasureAsync(request, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.InventorySummary.ShouldNotBeNull();
		result.InventorySummary!.EncryptedFieldCount.ShouldBe(1);
		result.InventorySummary!.KeyCount.ShouldBe(1);
	}

	[Fact]
	public async Task Work_without_legal_hold_service()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new ErasureOptions());
		var signingOptions = Microsoft.Extensions.Options.Options.Create(new ErasureSigningOptions { SigningKey = new byte[32] });
		var sut = new ErasureService(
			_store, _keyProvider, options, signingOptions,
			NullLogger<ErasureService>.Instance,
			null, // no legal hold service
			null); // no data inventory service

		var request = CreateValidRequest();

		// Act
		var result = await sut.RequestErasureAsync(request, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Status.ShouldBe(ErasureRequestStatus.Scheduled);
	}

	[Fact]
	public void Throw_for_null_store()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ErasureService(null!, _keyProvider,
				Microsoft.Extensions.Options.Options.Create(new ErasureOptions()),
				Microsoft.Extensions.Options.Options.Create(new ErasureSigningOptions()),
				NullLogger<ErasureService>.Instance,
				null, null));
	}

	[Fact]
	public void Throw_for_null_key_provider()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ErasureService(_store, null!,
				Microsoft.Extensions.Options.Options.Create(new ErasureOptions()),
				Microsoft.Extensions.Options.Options.Create(new ErasureSigningOptions()),
				NullLogger<ErasureService>.Instance,
				null, null));
	}

	[Fact]
	public void Throw_for_null_options()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ErasureService(_store, _keyProvider,
				null!,
				Microsoft.Extensions.Options.Options.Create(new ErasureSigningOptions()),
				NullLogger<ErasureService>.Instance,
				null, null));
	}

	[Fact]
	public void Throw_for_null_signing_options()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ErasureService(_store, _keyProvider,
				Microsoft.Extensions.Options.Options.Create(new ErasureOptions()),
				null!,
				NullLogger<ErasureService>.Instance,
				null, null));
	}

	[Fact]
	public void Throw_for_null_logger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ErasureService(_store, _keyProvider,
				Microsoft.Extensions.Options.Options.Create(new ErasureOptions()),
				Microsoft.Extensions.Options.Options.Create(new ErasureSigningOptions()),
				null!,
				null, null));
	}

	[Fact]
	public async Task Handle_key_deletion_failure_gracefully()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Scheduled);

		A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(status));
		A.CallTo(() => _store.UpdateStatusAsync(requestId, ErasureRequestStatus.InProgress, null, A<CancellationToken>._))
			.Returns(Task.FromResult(true));

		var inventory = new DataInventory
		{
			DataSubjectId = "abc123hash",
			Locations = [],
			AssociatedKeys = [new KeyReference { KeyId = "key-1", KeyScope = EncryptionKeyScope.User }],
		};

		A.CallTo(() => _dataInventoryService.DiscoverAsync(
				A<string>._, DataSubjectIdType.Hash, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(inventory));
		A.CallTo(() => _keyProvider.DeleteKeyAsync("key-1", A<int>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("KMS unavailable"));

		SetupNoLegalHolds();

		// Act - should not throw, handles error gracefully
		var result = await _sut.ExecuteAsync(requestId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();
		result.KeysDeleted.ShouldBe(0); // deletion failed but execution continued
	}

	[Fact]
	public async Task Invoke_erasure_contributors()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var status = CreateStatus(requestId, ErasureRequestStatus.Scheduled);
		var contributor = A.Fake<IErasureContributor>();

		A.CallTo(() => contributor.Name).Returns("TestContributor");
		A.CallTo(() => contributor.EraseAsync(A<ErasureContributorContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new ErasureContributorResult { Success = true, RecordsAffected = 5 }));

		var options = Microsoft.Extensions.Options.Options.Create(new ErasureOptions());
		var signingOptions = Microsoft.Extensions.Options.Options.Create(new ErasureSigningOptions { SigningKey = new byte[32] });
		var sut = new ErasureService(
			_store, _keyProvider, options, signingOptions,
			NullLogger<ErasureService>.Instance,
			_legalHoldService, null,
			[contributor]);

		A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(status));
		A.CallTo(() => _store.UpdateStatusAsync(requestId, ErasureRequestStatus.InProgress, null, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
		SetupNoLegalHolds();

		// Act
		var result = await sut.ExecuteAsync(requestId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();
		result.RecordsAffected.ShouldBe(5);
		A.CallTo(() => contributor.EraseAsync(A<ErasureContributorContext>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	private static ErasureRequest CreateValidRequest() =>
		new()
		{
			DataSubjectId = "user-123",
			IdType = DataSubjectIdType.UserId,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			RequestedBy = "admin",
			Scope = ErasureScope.User,
		};

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
