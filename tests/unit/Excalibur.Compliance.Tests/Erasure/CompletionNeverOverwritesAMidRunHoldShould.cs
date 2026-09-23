// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Excalibur.Dispatch;

using Excalibur.Compliance.Erasure;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// Binds Article 17(3) against the window between the legal-hold check and the completion write.
/// </summary>
/// <remarks>
/// <para>
/// <b>Holds are re-checked exactly once, and then the contributors run.</b> That pass is long by design —
/// it reaches event stores, snapshot stores and whatever else a consumer registered. A hold recorded
/// during it arrives after the only check that would have seen it, so the completion write is the last
/// writer and wins. Before this guard, that write stamped <c>Completed</c> over the hold and attached a
/// signed certificate to it: a compliance artifact attesting to an erasure the law had just forbidden,
/// and the signature now covers the whole payload, so the false attestation is authenticated.
/// </para>
/// <para>
/// <b>The guard is stated as a permitted set, not a forbidden one.</b> A status the store does not
/// recognise must refuse rather than inherit "fine" — a denylist would silently admit any state added to
/// the enum later, which is how a fail-closed property becomes fail-open without anyone editing it.
/// </para>
/// <para>
/// <b>Scope, stated so nobody inherits it wider.</b> These arms drive the in-memory store, so they bind
/// the CONTRACT every provider owes. The equivalent SQL clause was added to the PostgreSQL and SQL Server
/// stores in the same change and is <b>not</b> exercised here — it needs a real server, and its home is
/// the store conformance kit.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class CompletionNeverOverwritesAMidRunHoldShould
{
	private readonly IKeyManagementAdmin _keyAdmin = A.Fake<IKeyManagementAdmin>();
	private readonly ILegalHoldService _legalHoldService = A.Fake<ILegalHoldService>();

	// SAFETY, and the arm the guard exists for. The hold lands while the contributor is working, which is
	// after the only check that looks for one. The request must not end Completed and must not carry a
	// certificate. RED with the status clause removed: the completion write wins and stamps Completed.
	[Fact]
	public async Task Refuse_to_record_completion_over_a_hold_placed_during_the_contributor_pass()
	{
		var store = new InMemoryErasureStore(TestDataSubjectHasher.Instance, UntenantedContext.Instance, Options.Create(new TenantContextOptions()));
		var requestId = await ScheduleAsync(store).ConfigureAwait(false);

		// The hold arrives mid-run: the contributor is the thing that is "long", so recording it from
		// inside EraseAsync places it in exactly the window the re-check cannot see.
		var sut = BuildService(store, new HoldPlacingContributor(store, requestId));

		_ = await sut.ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		var status = await store.GetStatusAsync(requestId, CancellationToken.None).ConfigureAwait(false);
		status.ShouldNotBeNull();

		status!.Status.ShouldNotBe(
			ErasureRequestStatus.Completed,
			"a legal hold recorded during the run forbids the erasure under Article 17(3); a completion "
			+ "written after it would attest to an erasure the law had just blocked");

		status.CertificateId.ShouldBeNull(
			"no certificate may be attached to a request whose completion was refused — the certificate is "
			+ "the artifact a data subject and a regulator are shown");
	}

	// LIVENESS. Without it, a store that refused every completion would satisfy the arm above while
	// breaking erasure entirely. Nothing interferes here and the request must complete.
	[Fact]
	public async Task Record_completion_normally_when_nothing_interrupts_the_run()
	{
		var store = new InMemoryErasureStore(TestDataSubjectHasher.Instance, UntenantedContext.Instance, Options.Create(new TenantContextOptions()));
		var requestId = await ScheduleAsync(store).ConfigureAwait(false);

		var sut = BuildService(store, contributor: null);

		_ = await sut.ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		var status = await store.GetStatusAsync(requestId, CancellationToken.None).ConfigureAwait(false);
		status.ShouldNotBeNull();
		status!.Status.ShouldBe(
			ErasureRequestStatus.Completed,
			"an uninterrupted erasure must still complete — a guard that refused this one would be a "
			+ "blanket failure wearing a safety property's name");
		status.CertificateId.ShouldNotBeNull();
	}

	// SAFETY. The refusal must be distinguishable from "no such request". Reporting a request that exists
	// and moved as one that never existed is the absence-of-evidence error this subsystem exists to
	// prevent, committed by the guard that was added to prevent it.
	[Fact]
	public async Task Say_why_a_completion_was_refused_rather_than_reporting_the_request_missing()
	{
		var store = new InMemoryErasureStore(TestDataSubjectHasher.Instance, UntenantedContext.Instance, Options.Create(new TenantContextOptions()));
		var requestId = await ScheduleAsync(store).ConfigureAwait(false);
		_ = await store.UpdateStatusAsync(
			requestId, ErasureRequestStatus.BlockedByLegalHold, "legal hold", CancellationToken.None).ConfigureAwait(false);

		var refusal = await Should.ThrowAsync<KeyNotFoundException>(
			() => store.RecordCompletionAsync(requestId, 1, 1, Guid.NewGuid(), CancellationToken.None))
			.ConfigureAwait(false);

		refusal.Message.ShouldContain(
			"status changed",
			customMessage: "the operator has to be able to tell a request that moved from one that was "
			+ "never there; the two have different remedies and only one of them is a bug");
	}

	private static async Task<Guid> ScheduleAsync(IErasureStore store)
	{
		var request = new ErasureRequest
		{
			DataSubjectId = "user-mid-run-hold",
			IdType = DataSubjectIdType.UserId,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			RequestedBy = "compliance-admin",
		};

		await store.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddSeconds(-1), CancellationToken.None)
			.ConfigureAwait(false);

		return request.RequestId;
	}

	/// <summary>Builds the real service over the real in-memory store, with holds reported clear.</summary>
	/// <param name="store">The store under test.</param>
	/// <param name="contributor">An optional contributor, which is what makes the run "long".</param>
	/// <returns>The service.</returns>
	private ErasureService BuildService(IErasureStore store, IErasureContributor? contributor)
	{
		// Clear at the one point the service looks. The whole defect is that this answer is taken once,
		// before a pass during which it can stop being true.
		A.CallTo(() => _legalHoldService.CheckHoldsAsync(
				A<string>._, A<DataSubjectIdType>._, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new LegalHoldCheckResult { HasActiveHolds = false, ActiveHolds = [] }));

		A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult(KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow)));

		return new ErasureService(
			store,
			_keyAdmin,
			Options.Create(new ErasureOptions
			{
				// The subject here is the completion write, so coverage is taken off the critical path by
				// the documented opt-in rather than by a fixture that fakes coverage.
				KeyShredOnlyErasure = true,
				Retention = new ErasureRetentionOptions { SigningKey = new byte[32] },
			}),
			NullLogger<ErasureService>.Instance,
			TestDataSubjectHasher.Instance,
			_legalHoldService,
			null,
			null,
			TestAnnotationSource.None,
			contributor is null ? null : [contributor]);
	}

	/// <summary>
	/// A contributor that records a legal hold on the request it is erasing, which is the mid-run arrival
	/// the single hold check structurally cannot see.
	/// </summary>
	private sealed class HoldPlacingContributor(IErasureStore store, Guid requestId) : IErasureContributor
	{
		public string Name => "test-hold-placing";

		public IReadOnlySet<DataStoreKind> CoveredStoreKinds { get; } = new HashSet<DataStoreKind>();

		public async Task<ErasureContributorResult> EraseAsync(
			ErasureContributorContext context,
			CancellationToken cancellationToken)
		{
			_ = await store.UpdateStatusAsync(
				requestId,
				ErasureRequestStatus.BlockedByLegalHold,
				"legal hold recorded while the erasure was executing",
				cancellationToken).ConfigureAwait(false);

			return ErasureContributorResult.Succeeded(1);
		}
	}
}
