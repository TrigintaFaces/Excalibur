using Microsoft.Extensions.Logging.Abstractions;

using Excalibur.Compliance.Erasure;

using Excalibur.Compliance;namespace Excalibur.Compliance.Tests.Erasure;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ErasureServiceShould
{
	private readonly IErasureStore _store = A.Fake<IErasureStore>();
	private readonly IKeyManagementAdmin _keyAdmin = A.Fake<IKeyManagementAdmin>();
	private readonly ILegalHoldService _legalHoldService = A.Fake<ILegalHoldService>();
	private readonly IDataInventoryService _dataInventoryService = A.Fake<IDataInventoryService>();
	private readonly ErasureService _sut;

	public ErasureServiceShould()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new ErasureOptions
		{
			Retention = new ErasureRetentionOptions { SigningKey = new byte[32] },
		});

		// Annotated coverage is pinned EMPTY: these arms' subject is contributor/key-deletion coverage,
		// and the production default would otherwise scan the whole test assembly. See TestAnnotationSource.
		_sut = new ErasureService(
			_store,
			_keyAdmin,
			options,
			NullLogger<ErasureService>.Instance,
			TestDataSubjectHasher.Instance,
			_legalHoldService,
			_dataInventoryService,
			null,
			TestAnnotationSource.None,
			[]);
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
		A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult(KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow)));

		SetupNoLegalHolds();

		// A KEY-DESTRUCTION-ONLY host, which is exactly what this arm exercises: there are no
		// contributors and no registry, and erasure is effected by shredding the subject's keys. That is a
		// legitimate configuration and the framework has an explicit opt-in for it, so the fixture states
		// it rather than leaving the service to infer a misconfiguration from an empty registry.
		var sut = CreateKeyShredOnlySut();

		// Act
		var result = await sut.ExecuteAsync(requestId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();
		// Crypto-shred: erasure now also destroys the per-subject key (status.DataSubjectIdHash) in
		// addition to the 2 inventory keys -> 3 keys deleted (ErasureService adds the subject-id hash).
		result.KeysDeleted.ShouldBe(3);
		A.CallTo(() => _store.RecordCompletionAsync(requestId, 3, 0, A<Guid>._, A<CancellationToken>._))
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

		// T.6 TOCTOU fix: ExecuteAsync transitions to InProgress first, then re-checks legal holds
		A.CallTo(() => _store.UpdateStatusAsync(requestId, ErasureRequestStatus.InProgress, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));

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

	/// <summary>
	/// A deployment that operates no legal holds still erases — it just has to SAY it operates none.
	/// </summary>
	/// <remarks>
	/// This replaces an arm that passed <see langword="null"/> for the legal-hold service and asserted
	/// erasure proceeded. That arm's premise is now deliberately unreachable: the dependency is required,
	/// because a null one made "this deployment has no holds" and "nobody wired the service" the same
	/// observation, and the second silently skipped an irreversible check. The capability under test is
	/// unchanged and still covered — erasure proceeds when nothing blocks it — but it is now reached
	/// through the declared no-holds service rather than through an absence.
	/// </remarks>
	[Fact]
	public async Task Work_when_the_deployment_declares_no_legal_holds()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new ErasureOptions
		{
			Retention = new ErasureRetentionOptions { SigningKey = new byte[32] },
		});
		var sut = new ErasureService(
			_store, _keyAdmin, options,
			NullLogger<ErasureService>.Instance,
			TestDataSubjectHasher.Instance,
			new NoLegalHoldsService(), // declared: this deployment operates none
			null, // no data inventory service
			null); // no key escrow service

		var request = CreateValidRequest();

		// Act
		var result = await sut.RequestErasureAsync(request, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Status.ShouldBe(ErasureRequestStatus.Scheduled);
	}

	/// <summary>
	/// The legal-hold service is required, and this is the arm that keeps it required.
	/// </summary>
	/// <remarks>
	/// Without it, a later edit could quietly restore the nullable parameter and every other arm would
	/// still pass — the skipped-hold-check defect is invisible to tests that never had holds to skip.
	/// </remarks>
	[Fact]
	public void Throw_for_null_legal_hold_service()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new ErasureOptions
		{
			Retention = new ErasureRetentionOptions { SigningKey = new byte[32] },
		});

		var ex = Should.Throw<ArgumentNullException>(() =>
			new ErasureService(
				_store, _keyAdmin, options,
				NullLogger<ErasureService>.Instance,
				TestDataSubjectHasher.Instance,
				null!,
				null,
				null));

		ex.ParamName.ShouldBe("legalHoldService");
	}

	[Fact]
	public void Throw_for_null_store()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ErasureService(null!, _keyAdmin,
				Microsoft.Extensions.Options.Options.Create(new ErasureOptions()),
				NullLogger<ErasureService>.Instance,
			TestDataSubjectHasher.Instance,
				null, null, null));
	}


	[Fact]
	public void Throw_for_null_options()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ErasureService(_store, _keyAdmin,
				null!,
				NullLogger<ErasureService>.Instance,
			TestDataSubjectHasher.Instance,
				null, null, null));
	}

	[Fact]
	public void Throw_for_null_logger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ErasureService(_store, _keyAdmin,
				Microsoft.Extensions.Options.Options.Create(new ErasureOptions()),
				null!,
				TestDataSubjectHasher.Instance,
				null, null, null));
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
		A.CallTo(() => _keyAdmin.DeleteKeyAsync("key-1", A<int>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("KMS unavailable"));

		SetupNoLegalHolds();

		// Act - should not throw, handles error gracefully
		var result = await _sut.ExecuteAsync(requestId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert -- Sprint 672 T.2: key deletion failure = not successful (no false compliance)
		result.Success.ShouldBeFalse();
		result.KeysDeleted.ShouldBe(0);
	}

	[Fact]
	public async Task Revoke_escrow_before_destroying_a_key_that_was_escrowed()
	{
		// Arrange -- exb9w4: a subject key that was escrowed must have its escrow revoked BEFORE the
		// working key is destroyed, so a consumer who escrowed the subject key for disaster recovery
		// cannot silently defeat erasure via the still-recoverable escrowed spare.
		var keyEscrowService = A.Fake<IKeyEscrowService>();
		var sut = new ErasureService(
			_store, _keyAdmin,
			Microsoft.Extensions.Options.Options.Create(new ErasureOptions
			{
				Retention = new ErasureRetentionOptions { SigningKey = new byte[32] },
			}),
			NullLogger<ErasureService>.Instance,
			TestDataSubjectHasher.Instance,
			_legalHoldService, _dataInventoryService,
			keyEscrowService,
			TestAnnotationSource.None,
			[]);

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

		A.CallTo(() => keyEscrowService.RevokeEscrowAsync("key-1", A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
		A.CallTo(() => _keyAdmin.DeleteKeyAsync("key-1", A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult(KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow)));

		SetupNoLegalHolds();

		// Act
		var result = await sut.ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		// Assert -- LIVENESS: escrow revocation does not block a normal destruction; the key is still
		// destroyed and counted.
		result.KeysDeleted.ShouldBe(1);

		// Assert -- revoke happened BEFORE destroy (revoke-then-destroy, never the reverse).
		A.CallTo(() => keyEscrowService.RevokeEscrowAsync("key-1", A<string?>._, A<CancellationToken>._))
			.MustHaveHappened()
			.Then(A.CallTo(() => _keyAdmin.DeleteKeyAsync("key-1", A<int>._, A<CancellationToken>._)).MustHaveHappened());
	}

	[Fact]
	public async Task Not_destroy_a_key_when_revoking_its_escrow_fails()
	{
		// Arrange -- SAFETY: destroying the working key while its escrow revocation failed would leave
		// a recoverable escrowed spare behind an erasure certificate that attests the data gone.
		var keyEscrowService = A.Fake<IKeyEscrowService>();
		var sut = new ErasureService(
			_store, _keyAdmin,
			Microsoft.Extensions.Options.Options.Create(new ErasureOptions
			{
				Retention = new ErasureRetentionOptions { SigningKey = new byte[32] },
			}),
			NullLogger<ErasureService>.Instance,
			TestDataSubjectHasher.Instance,
			_legalHoldService, _dataInventoryService,
			keyEscrowService,
			TestAnnotationSource.None,
			[]);

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

		A.CallTo(() => keyEscrowService.RevokeEscrowAsync("key-1", A<string?>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("escrow store unavailable"));

		SetupNoLegalHolds();

		// Act
		var result = await sut.ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		// Assert -- not attested as complete, and the key was never destroyed.
		result.Success.ShouldBeFalse();
		result.KeysDeleted.ShouldBe(0);
		A.CallTo(() => _keyAdmin.DeleteKeyAsync("key-1", A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
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
			.Returns(Task.FromResult(ErasureContributorResult.Succeeded(
				5,
				[new DataLocationKey("Users", "Email")])));

		var options = Microsoft.Extensions.Options.Options.Create(new ErasureOptions
		{
			Retention = new ErasureRetentionOptions { SigningKey = new byte[32] },
		});
		var sut = new ErasureService(
			_store, _keyAdmin, options,
			NullLogger<ErasureService>.Instance,
			TestDataSubjectHasher.Instance,
			_legalHoldService, _dataInventoryService, null,
			TestAnnotationSource.None, [contributor]);

		A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(status));
		A.CallTo(() => _store.UpdateStatusAsync(requestId, ErasureRequestStatus.InProgress, null, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
		SetupNoLegalHolds();

		// A CONFIGURED host: the registry declares a location and the contributor NAMES it as discharged.
		// An empty registry models a MISCONFIGURED host -- coverage unestablished -- which is a different
		// scenario and is asserted by its own arm. Locations stays empty on purpose: this arm's subject is
		// that contributors are invoked, and "we found no rows" must not be what discharges the obligation.
		var inventory = new DataInventory
		{
			DataSubjectId = "abc123hash",
			Locations = [],
			DeclaredLocations = [new DataLocationKey("Users", "Email")],
			AssociatedKeys = [],
		};
		A.CallTo(() => _dataInventoryService.DiscoverAsync(
				A<string>._, DataSubjectIdType.Hash, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(inventory));
		A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult(KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow)));

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

	/// <summary>
	/// A service configured for key-destruction-only erasure -- the host model for an arm whose subject is
	/// crypto-shred: no contributors, no data-location registry, coverage established by destroying the
	/// subject's keys rather than by a registry of tables.
	/// </summary>
	private ErasureService CreateKeyShredOnlySut()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new ErasureOptions
		{
			KeyShredOnlyErasure = true,
			Retention = new ErasureRetentionOptions { SigningKey = new byte[32] },
		});

		return new ErasureService(
			_store, _keyAdmin, options,
			NullLogger<ErasureService>.Instance,
			TestDataSubjectHasher.Instance,
			_legalHoldService, _dataInventoryService, null,
			TestAnnotationSource.None, []);
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
