// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Logging.Abstractions;

using Excalibur.Compliance.Erasure;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// An erasure that destroyed a subject's key and then failed part-way must, on a retry, attest the coverage
/// its first pass achieved — otherwise the subject's data is destroyed and their erasure can never be
/// reported complete.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect these arms detect.</b> Only a <see cref="KeyDestructionState.Completed"/> destruction used
/// to enter the coverage set. Asking a key store to destroy a key it has ALREADY destroyed reports the key as
/// absent — <see cref="KeyDestructionState.NotFound"/> — which is the same answer it gives for a key that
/// never existed. So the retry contributed nothing for that key, the coverage gate saw a discovered location
/// whose key was not in the deleted set, and <c>Completed</c> became permanently unreachable. Article 17
/// completion was impossible for any interrupted erasure, and the shortfall was indistinguishable from a key
/// that never existed.
/// </para>
/// <para>
/// <b>Why the record has to live on the REQUEST.</b> The two causes of absence — "a previous pass of THIS
/// request destroyed it" and "we never held it" — have opposite consequences for attestation, and no question
/// put to the key store can separate them: its own contract says it answers <see langword="true"/> for a
/// destroyed key OR one that never existed. So the request remembers what it destroyed, and the retry reads
/// what its own earlier pass wrote.
/// </para>
/// <para>
/// <b>Safety and liveness are both here.</b> "Attest a NotFound" is satisfiable by attesting EVERY absent
/// key, which would let a wrong inventory entry read as an erased location.
/// <see cref="NotCountANotFoundKeyThisRequestNeverDestroyed"/> is the arm that fails for that, and it is the
/// same arm as the first with one variable changed: whether the request recorded the handle.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ARetriedErasureAttestsWhatItsFirstPassDestroyedShould
{
	private const string SubjectHash = "abc123hash";
	private const string SubjectKey = "key-1";

	private readonly IErasureStore _store = A.Fake<IErasureStore>();

	// ALSO a destruction-status provider, and that is not fixture decoration. Before attesting, the erasure
	// re-establishes the state of every handle it destroyed -- because "we destroyed it earlier" is not "it is
	// destroyed now", and a handle can be re-occupied by a write that lands during the erasure. A provider
	// that cannot answer that question leaves the state unmeasured, and the erasure records the absence rather
	// than assuming the handles are clean. So a fixture without this capability is refused for a reason that
	// has nothing to do with coverage, and every arm here would fail without telling you why.
	//
	// It also implements IKeyManagementProvider, and that is load-bearing for a second reason: the service
	// reads a key's GENERATION before destroying it, because the destruction destroys the generation
	// identifier and the destruction record is keyed on it. A fake that could not answer GetKeyAsync would
	// leave every destruction unrecordable, and these arms would fail for a reason that has nothing to do
	// with retry attestation.
	private readonly IKeyManagementAdmin _keyAdmin =
		A.Fake<IKeyManagementAdmin>(o => o
			.Implements<IKeyDestructionStatusProvider>()
			.Implements<IKeyManagementProvider>());
	private readonly ILegalHoldService _legalHolds = A.Fake<ILegalHoldService>();
	private readonly IDataInventoryService _inventory = A.Fake<IDataInventoryService>();

	/// <summary>
	/// SAFETY, and the RED arm. The retry sees the key as absent, and attests it because this request's own
	/// record says it destroyed it.
	/// </summary>
	/// <remarks>
	/// RED input: a request whose <see cref="ErasureStatus.DestroyedKeyHandles"/> contains the subject's key
	/// while <c>DeleteKeyAsync</c> reports <see cref="KeyDestructionState.NotFound"/> — exactly the state a
	/// retry of an interrupted erasure is in. Before the fix the key entered no coverage set, the location
	/// read as uncovered, and the result was a failure that no number of retries could clear.
	/// </remarks>
	[Fact]
	public async Task CountAKeyItsOwnEarlierPassDestroyed()
	{
		var requestId = Guid.NewGuid();

		// The retry's state: the request already recorded this handle as destroyed on an earlier pass.
		SetupScheduledRequest(requestId, alreadyDestroyed: [SubjectKey]);

		// And the key store now reports it absent, because it genuinely is gone.
		A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult(KeyDestructionOutcome.NotFound));

		SetupInventoryCoveredOnlyByTheSubjectKey();

		var result = await CreateService().ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(true);

		result.Success.ShouldBeTrue(
			"a retry must attest the destruction its own earlier pass performed. The key is gone, the location "
			+ "is unreadable, and refusing to count it makes completion unreachable for every interrupted "
			+ "erasure — the subject's data destroyed and their erasure permanently uncertifiable.");
	}

	/// <summary>
	/// SAFETY twin against over-correction. An absent key this request never destroyed is still not coverage.
	/// </summary>
	/// <remarks>
	/// One variable changed from the arm above: the request has no record for the handle. A fix that attested
	/// every <see cref="KeyDestructionState.NotFound"/> would pass that arm and this one would fail — and it
	/// must, because a discovered location whose key never existed is an inventory that disagrees with
	/// reality, and counting it as erased hides exactly that.
	/// </remarks>
	[Fact]
	public async Task NotCountANotFoundKeyThisRequestNeverDestroyed()
	{
		var requestId = Guid.NewGuid();

		SetupScheduledRequest(requestId, alreadyDestroyed: []);

		A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult(KeyDestructionOutcome.NotFound));

		SetupInventoryCoveredOnlyByTheSubjectKey();

		var result = await CreateService().ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(true);

		result.Success.ShouldBeFalse(
			"a key this request never destroyed is not coverage, however absent it is. Counting it would let a "
			+ "location the inventory names — with a key that never existed — report as erased.");
	}

	/// <summary>
	/// LIVENESS. An ordinary first pass, where the destruction completes here and now, still succeeds.
	/// </summary>
	/// <remarks>
	/// Without this, an implementation that counted NOTHING would satisfy the twin above, and one that broke
	/// the uninterrupted path while fixing the retry would look correct.
	/// </remarks>
	[Fact]
	public async Task StillCountAKeyItDestroysOnThisPass()
	{
		var requestId = Guid.NewGuid();

		SetupScheduledRequest(requestId, alreadyDestroyed: []);

		A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult(KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow)));

		SetupInventoryCoveredOnlyByTheSubjectKey();

		var result = await CreateService().ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(true);

		result.Success.ShouldBeTrue(result.ErrorMessage);
	}

	/// <summary>
	/// SAFETY. A destruction that completes is RECORDED durably, per key, before it is counted.
	/// </summary>
	/// <remarks>
	/// The record is what the retry above reads, so a pass that counts a destruction without writing it down
	/// leaves the NEXT pass unable to attest it — which is the whole defect, one pass later. Asserting the
	/// call is what makes the two halves meet: without it, the coverage arms could pass over a record nothing
	/// ever wrote.
	/// </remarks>
	[Fact]
	public async Task RecordEachDestructionAgainstTheRequestAsItHappens()
	{
		var requestId = Guid.NewGuid();

		SetupScheduledRequest(requestId, alreadyDestroyed: []);

		A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult(KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow)));

		SetupInventoryCoveredOnlyByTheSubjectKey();

		_ = await CreateService().ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(true);

		A.CallTo(() => _store.RecordKeyDestroyedAsync(requestId, SubjectKey, A<string>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	/// <summary>
	/// SAFETY. A destruction whose record could not be written is NOT attested on this pass.
	/// </summary>
	/// <remarks>
	/// Attesting a destruction we failed to record is the defect one pass deferred: this pass counts it, the
	/// record is absent, and the retry can never count it again. Failing the pass instead keeps the request
	/// retryable, which is the recoverable direction.
	/// </remarks>
	[Fact]
	public async Task NotAttestADestructionItCouldNotRecord()
	{
		var requestId = Guid.NewGuid();

		SetupScheduledRequest(requestId, alreadyDestroyed: []);

		A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult(KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow)));

		A.CallTo(() => _store.RecordKeyDestroyedAsync(A<Guid>._, A<string>._, A<string>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("the destroyed-key record could not be written"));

		SetupInventoryCoveredOnlyByTheSubjectKey();

		var result = await CreateService().ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(true);

		result.Success.ShouldBeFalse(
			"a destruction whose record could not be written must not be attested: counting it now and losing "
			+ "the record makes it uncountable on every later pass.");
	}

	// TWO locations, and the split is what makes these arms sensitive to the coverage set.
	//
	// The EVENT-STORE location exists to satisfy the surrounding gate rather than to be the subject: the
	// registry must declare at least one location (an EMPTY registry is refused outright -- "an absence of
	// evidence and not a proof of erasure"), and a declared location must be discharged by a contributor or
	// it is reported outstanding. Both of those would fail every arm here for reasons that have nothing to do
	// with the key.
	//
	// The OUTBOX location is the subject. No contributor covers that store kind and it is not a declared
	// exemption, so the ONLY thing that can cover it is its key appearing in the deleted set -- which is
	// exactly the question these arms turn.
	private void SetupInventoryCoveredOnlyByTheSubjectKey()
	{
		var inventory = new DataInventory
		{
			DataSubjectId = SubjectHash,
			Locations =
			[
				new DataLocation
				{
					StoreKind = DataStoreKind.EventStore,
					TableName = "Events",
					FieldName = "Data",
					DataCategory = "PII",
					RecordId = "evt-1",
					KeyId = "events-key",
				},
				new DataLocation
				{
					StoreKind = DataStoreKind.Outbox,
					TableName = "OutboxMessages",
					FieldName = "Payload",
					DataCategory = "PII",
					RecordId = "obx-1",
					KeyId = SubjectKey,
				},
			],
			DeclaredLocations = [new DataLocationKey("Events", "Data")],
			AssociatedKeys =
			[
				new KeyReference { KeyId = SubjectKey, KeyScope = EncryptionKeyScope.User },
				new KeyReference { KeyId = "events-key", KeyScope = EncryptionKeyScope.User },
			],
		};

		A.CallTo(() => _inventory.DiscoverAsync(
				A<string>._, DataSubjectIdType.Hash, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(inventory));
	}

	// Covers the event-store kind and reports the declared pair erased, so the surrounding obligation is
	// discharged. It covers NOTHING in the outbox, which is what leaves the subject location's coverage
	// resting entirely on the destroyed key.
	private static IErasureContributor EventStoreContributor()
	{
		var contributor = A.Fake<IErasureContributor>();
		A.CallTo(() => contributor.Name).Returns("EventStore");
		A.CallTo(() => contributor.CoveredStoreKinds)
			.Returns(new HashSet<DataStoreKind> { DataStoreKind.EventStore });
		A.CallTo(() => contributor.EraseAsync(A<ErasureContributorContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(
				ErasureContributorResult.Succeeded(1, [new DataLocationKey("Events", "Data")])));
		return contributor;
	}

	/// <summary>
	/// SAFETY, and the arm that keeps the two halves in the right order. A handle this request recorded
	/// destroying, which the provider now reports as NOT destroyed, is not attested — the record loses.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>My record makes a retried destruction COUNTABLE. It does not make it TRUE.</b> "This request
	/// destroyed the handle earlier" and "the handle is destroyed now" are different statements, and a write
	/// for the subject landing during the erasure can re-occupy a destroyed handle — at which point the
	/// material behind it is live again and attesting the coverage would tell a data subject their data is
	/// unreadable while it is not.
	/// </para>
	/// <para>
	/// So the provider's answer overrides the record, and this arm exists to stop that ordering being
	/// inverted. If a future change makes the arms above fail, the fix is the fixture or the record — never
	/// relaxing this, which would convert a refusal into a silent attestation.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task NotAttestAHandleItRecordedDestroyingThatIsLiveAgain()
	{
		var requestId = Guid.NewGuid();

		SetupScheduledRequest(requestId, alreadyDestroyed: [SubjectKey]);

		A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult(KeyDestructionOutcome.NotFound));

		// The record says this request destroyed it. The provider says the handle holds material now.
		A.CallTo(() => ((IKeyDestructionStatusProvider)_keyAdmin).IsKeyDestroyedAsync(
				SubjectKey, A<CancellationToken>._))
			.Returns(Task.FromResult(false));

		SetupInventoryCoveredOnlyByTheSubjectKey();

		var result = await CreateService().ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(true);

		result.Success.ShouldBeFalse(
			"a handle that was destroyed earlier and is live again must not be attested. The record makes the "
			+ "retry countable; it does not make the destruction true, and the provider's answer is the one "
			+ "about now.");
	}

	// Every handle this erasure destroyed is still gone when the attestation re-checks it. That is the ordinary
	// case; NotAttestAHandleItRecordedDestroyingThatIsLiveAgain is the arm that turns it.
	private void GivenEveryDestroyedHandleIsStillGone() =>
		A.CallTo(() => ((IKeyDestructionStatusProvider)_keyAdmin).IsKeyDestroyedAsync(
				A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));

	/// <summary>
	/// Every handle reports a generation, so a destruction that reaches Completed can be recorded.
	/// </summary>
	/// <remarks>
	/// The service reads the generation BEFORE destroying, because the destruction destroys the identifier and
	/// the destruction record is keyed on it. A fake that answered nothing here would make every destruction
	/// unrecordable, and these arms -- which are about retry attestation, not about generation lookup -- would
	/// all fail for the wrong reason. The generation is MINTED PER HANDLE -- stable for one handle so a retried
	/// erasure attests what the first pass destroyed, distinct across handles so two destroyed keys do not
	/// collide on the record's key, and not derived FROM the handle, which is what KeyGeneration's consumer
	/// obligation forbids.
	/// </remarks>
	private void GivenEveryHandleReportsAGeneration()
	{
		var generations = new System.Collections.Concurrent.ConcurrentDictionary<string, KeyGeneration>(
			StringComparer.Ordinal);

		A.CallTo(() => ((IKeyManagementProvider)_keyAdmin).GetKeyAsync(A<string>._, A<CancellationToken>._))
			.ReturnsLazily((string keyId, CancellationToken _) =>
				Task.FromResult<KeyMetadata?>(new KeyMetadata
				{
					KeyId = keyId,
					Version = 1,
					Status = KeyStatus.Active,
					Algorithm = EncryptionAlgorithm.Aes256Gcm,
					CreatedAt = DateTimeOffset.UtcNow,
					Generation = generations.GetOrAdd(keyId, static _ => KeyGeneration.Mint()),
				}));
	}

	private void SetupScheduledRequest(Guid requestId, IReadOnlyCollection<string> alreadyDestroyed)
	{
		GivenEveryDestroyedHandleIsStillGone();
		GivenEveryHandleReportsAGeneration();

		var status = new ErasureStatus
		{
			RequestId = requestId,
			DataSubjectIdHash = SubjectHash,
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			Status = ErasureRequestStatus.Scheduled,
			RequestedAt = DateTimeOffset.UtcNow.AddHours(-1),
			RequestedBy = "admin",
			UpdatedAt = DateTimeOffset.UtcNow,

			// The whole variable these arms turn: what this request has already destroyed.
			DestroyedKeyHandles = alreadyDestroyed,
		};

		A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(status));
		A.CallTo(() => _store.UpdateStatusAsync(requestId, ErasureRequestStatus.InProgress, null, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
		A.CallTo(() => _legalHolds.CheckHoldsAsync(
				A<string>._, A<DataSubjectIdType>._, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new LegalHoldCheckResult { HasActiveHolds = false, ActiveHolds = [] }));
	}

	private ErasureService CreateService() =>
		new(
			_store,
			_keyAdmin,
			Microsoft.Extensions.Options.Options.Create(new ErasureOptions
			{
				Retention = new ErasureRetentionOptions { SigningKey = new byte[32] },
			}),
			NullLogger<ErasureService>.Instance,
			TestDataSubjectHasher.Instance,
			_legalHolds,
			_inventory,
			null,
			TestAnnotationSource.None,
			TestRetentions.None,
			contributors: [EventStoreContributor()]);
}
