// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Excalibur.Dispatch;
using Excalibur.Compliance.Erasure;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// A host shutdown arriving while a key is being destroyed must not lose the record that it was.
/// </summary>
/// <remarks>
/// <para>
/// Destroying a key is irreversible and the ledger row is the only evidence it happened. The two are
/// separate awaits, so a cancellation can land between them — and when the ledger write carried the
/// CALLER'S token, that is exactly what it did: the write threw, the per-key handler swallowed it as an
/// ordinary failure, the loop continued, and the row was never written.
/// </para>
/// <para>
/// <b>The consequence outlives the run.</b> On a later pass the key provider cannot distinguish a
/// generation it destroyed for us from one it never held, so the only discriminator is a registered
/// destruction-status provider. Where none is registered the request can never be certified, and the
/// material is already gone. That is the silent, permanent case this subsystem exists to prevent.
/// </para>
/// <para>
/// <b>Both halves.</b> A run that records the destruction and then hangs forever would satisfy the
/// safety arm, so the second arm asserts the run actually STOPS at the next key rather than continuing
/// to destroy material after the host asked it to stop.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "Regression")]
public sealed class ADestructionRecordSurvivesAShutdownMidLoopShould
{
	private readonly IKeyManagementAdmin _keyAdmin =
		KeyDestructionFakes.AdminThatReportsAGenerationForEveryKey();

	private readonly ILegalHoldService _legalHoldService = A.Fake<ILegalHoldService>();
	private readonly IDataInventoryService _dataInventory = A.Fake<IDataInventoryService>();

	/// <summary>
	/// SAFETY: the destruction ledger row survives a cancellation that arrives at the destroy itself.
	/// </summary>
	/// <remarks>
	/// RED before the ledger write was moved off the caller's token: the write observes the cancelled
	/// token, throws, is swallowed by the per-key handler as a delete failure, and the handle never
	/// appears in the request's destroyed set even though the provider destroyed it.
	/// </remarks>
	[Fact]
	public async Task Record_a_destruction_whose_cancellation_arrived_at_the_destroy()
	{
		var store = NewStore();
		var requestId = await ScheduleAsync(store).ConfigureAwait(true);

		using var hostStopping = new CancellationTokenSource();
		var destroyed = new List<string>();

		InventoryIs("subject-key-1", "subject-key-2");

		A.CallTo(() => _legalHoldService.CheckHoldsByHashAsync(
				A<string>._, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new LegalHoldCheckResult { HasActiveHolds = false, ActiveHolds = [] }));

		// THE SHUTDOWN ARRIVES AT THE DESTROY, which is the only instant that matters: the act is done
		// and its record is not yet written. Cancelling before the call would destroy nothing, and
		// cancelling after the record would prove nothing.
		A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily((string keyId, int _, CancellationToken _) =>
			{
				destroyed.Add(keyId);
				hostStopping.Cancel();
				return Task.FromResult(KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow));
			});

		var sut = BuildService(ObservingCancellation(store));

		// The cancellation is expected to end the run. What it must NOT do is end it before the record.
		try
		{
			_ = await sut.ExecuteAsync(requestId, hostStopping.Token).ConfigureAwait(true);
		}
		catch (OperationCanceledException)
		{
			// The run stopping is the point of the second arm, not a failure of this one.
		}

		destroyed.ShouldNotBeEmpty(
			"the fixture's own precondition: if the provider was never asked to destroy anything, there is "
			+ "no record for this arm to be about and it would pass against any implementation");

		var status = await store.GetStatusAsync(requestId, CancellationToken.None).ConfigureAwait(true);

		status!.DestroyedKeyHandles.ShouldContain(
			destroyed[0],
			"a key the provider destroyed must be recorded even though the host cancelled at that instant. "
			+ "The destruction is irreversible and this row is the only evidence of it; without the row a "
			+ "later pass cannot tell a generation we destroyed from one that never existed, and where no "
			+ "destruction-status provider is registered the request can never be certified");
	}

	/// <summary>
	/// LIVENESS, and the reason the arm above cannot stand alone: the run must STOP at the next key.
	/// </summary>
	/// <remarks>
	/// A run that records its destruction and then keeps destroying would satisfy the safety arm while
	/// ignoring the host entirely. Cancellation is observed between keys, where nothing irreversible is
	/// waiting for its record — so exactly one key is destroyed, not both.
	/// </remarks>
	[Fact]
	public async Task Destroy_no_further_key_once_the_host_has_asked_it_to_stop()
	{
		var store = NewStore();
		var requestId = await ScheduleAsync(store).ConfigureAwait(true);

		using var hostStopping = new CancellationTokenSource();
		var destroyed = new List<string>();

		InventoryIs("subject-key-1", "subject-key-2");

		A.CallTo(() => _legalHoldService.CheckHoldsByHashAsync(
				A<string>._, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new LegalHoldCheckResult { HasActiveHolds = false, ActiveHolds = [] }));

		A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily((string keyId, int _, CancellationToken _) =>
			{
				destroyed.Add(keyId);
				hostStopping.Cancel();
				return Task.FromResult(KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow));
			});

		var sut = BuildService(ObservingCancellation(store));

		try
		{
			_ = await sut.ExecuteAsync(requestId, hostStopping.Token).ConfigureAwait(true);
		}
		catch (OperationCanceledException)
		{
			// Expected: the loop observes the cancellation at the top of the next iteration.
		}

		destroyed.Count.ShouldBe(
			1,
			customMessage: "once the host has asked the run to stop, no FURTHER key may be destroyed. "
			+ "Destruction is irreversible, so continuing through the remaining keys after a shutdown "
			+ "destroys material the operator tried to stop. Two handles were in the inventory and exactly "
			+ $"one must have been destroyed; destroyed: {string.Join(", ", destroyed)}");
	}

	private static InMemoryErasureStore NewStore() =>
		new(TestDataSubjectHasher.Instance,
			UntenantedContext.Instance,
			Options.Create(new TenantContextOptions()),
			TimeProvider.System);

	/// <summary>
	/// Wraps the reference store so that the ledger write OBSERVES cancellation, the way both SQL
	/// providers do and the in-memory store does not.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Without this the arms are vacuous, and they passed before it existed.</b>
	/// <c>InMemoryErasureStore.RecordKeyDestroyedAsync</c> accepts a token and never consults it, so the
	/// row lands whether the token is cancelled or not — which means the reference store cannot
	/// distinguish the fixed code from the defect, and a mutant restoring the defect left both arms
	/// green. Measured, not assumed: that is exactly what happened on the first attempt.
	/// </para>
	/// <para>
	/// Both SQL providers open their connection with the caller's token, so a cancelled token means no
	/// write. This decorator models that one behaviour and delegates everything else, which keeps the
	/// subject of these arms where it belongs: the SERVICE'S token discipline, not the store's.
	/// </para>
	/// </remarks>
	private static IErasureStore ObservingCancellation(InMemoryErasureStore store)
	{
		var wrapped = A.Fake<IErasureStore>(options => options.Wrapping(store));

		A.CallTo(() => wrapped.RecordKeyDestroyedAsync(
				A<Guid>._, A<string>._, A<string>._, A<CancellationToken>._))
			.ReturnsLazily((Guid requestId, string keyHandle, string keyGeneration, CancellationToken ct) =>
				ct.IsCancellationRequested
					? Task.FromException(new OperationCanceledException(ct))
					: store.RecordKeyDestroyedAsync(requestId, keyHandle, keyGeneration, ct));

		return wrapped;
	}

	private void InventoryIs(params string[] keyIds) =>
		A.CallTo(() => _dataInventory.DiscoverAsync(
				A<string>._, A<DataSubjectIdType>._, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new DataInventory
			{
				DataSubjectId = "user-shutdown-mid-destruction",
				AssociatedKeys = [.. keyIds.Select(static id =>
					new KeyReference { KeyId = id, KeyScope = EncryptionKeyScope.User })],
			}));

	private static async Task<Guid> ScheduleAsync(IErasureStore store)
	{
		var request = new ErasureRequest
		{
			DataSubjectId = "user-shutdown-mid-destruction",
			IdType = DataSubjectIdType.UserId,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			RequestedBy = "compliance-admin",
		};

		await store.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddSeconds(-1), CancellationToken.None)
			.ConfigureAwait(false);

		return request.RequestId;
	}

	private ErasureService BuildService(IErasureStore store) =>
		new(
			store,
			_keyAdmin,
			Options.Create(new ErasureOptions
			{
				KeyShredOnlyErasure = true,
				Retention = new ErasureRetentionOptions { SigningKey = new byte[32] },
			}),
			NullLogger<ErasureService>.Instance,
			TestDataSubjectHasher.Instance,
			_legalHoldService,
			_dataInventory,
			null,
			TestAnnotationSource.None,
			TestRetentions.None,
			TestMintLedger.None,
			contributors: null);
}
