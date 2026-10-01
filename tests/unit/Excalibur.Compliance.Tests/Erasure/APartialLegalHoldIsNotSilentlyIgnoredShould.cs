// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Excalibur.Dispatch;

using Excalibur.Compliance.Erasure;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// Binds the case where a legal hold retains SOME categories rather than blocking the whole request.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect these arms exist for.</b> The service decided whether to proceed by reading one derived
/// flag meaning "there are holds AND none of them names an exempt category". A hold that DOES name
/// categories therefore made that flag false, and the erasure ran as though no hold existed at all --
/// destroying the very categories the controller had registered a basis, a case reference and a
/// description in order to keep.
/// </para>
/// <para>
/// <b>Why the refusal has to sit before key destruction, and not in a contributor.</b> The data subject's
/// own key is queued unconditionally and destroyed BEFORE any contributor runs, and it is scoped to the
/// SUBJECT rather than to a category. So a contributor-level refusal arrives too late: by then the key is
/// gone and every annotated field of that subject is unrecoverable, retained categories included. There is
/// no category-granular destruction step to decline.
/// </para>
/// <para>
/// <b>The direction is deliberate.</b> Withholding an erasure is recoverable -- release or narrow the hold,
/// or store the retained categories separately. Destroying records under hold is not. So an inexpressible
/// restriction refuses rather than widens.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class APartialLegalHoldIsNotSilentlyIgnoredShould
{
	private const string RetainedCategory = "FinancialRecords";

	private readonly IKeyManagementAdmin _keyAdmin = A.Fake<IKeyManagementAdmin>();
	private readonly ILegalHoldService _legalHoldService = A.Fake<ILegalHoldService>();

	// SAFETY. A hold naming a retained category must stop the erasure BEFORE the per-subject key is
	// destroyed, because destroying it is what takes the retained category with it. RED with the
	// partially-blocked branch removed: the derived blocked flag is false for this hold, so the service
	// proceeds, DeleteKeyAsync happens, and the request completes.
	[Fact]
	public async Task Destroy_no_key_when_a_hold_retains_specific_categories()
	{
		var store = NewStore();
		var requestId = await ScheduleAsync(store).ConfigureAwait(false);
		var sut = BuildService(store, exemptCategories: [RetainedCategory]);

		var result = await sut.ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		result.Success.ShouldBeFalse(
			"the restriction cannot be expressed by a subject-scoped key destruction or a whole-aggregate "
			+ "tombstone, so the only honest answer is to refuse");

		A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
			.MustNotHaveHappened();

		var status = await store.GetStatusAsync(requestId, CancellationToken.None).ConfigureAwait(false);
		status.ShouldNotBeNull();
		status!.Status.ShouldNotBe(
			ErasureRequestStatus.Completed,
			"a request whose retained categories were never excluded must not report completion");
	}

	// LIVENESS, and it is what stops the arm above being satisfied by refusing every erasure. With no hold
	// in force the run must still destroy the subject's key. RED if the refusal is widened past the
	// partially-blocked case.
	[Fact]
	public async Task Still_destroy_the_key_when_no_hold_is_in_force()
	{
		var store = NewStore();
		var requestId = await ScheduleAsync(store).ConfigureAwait(false);
		var sut = BuildService(store, exemptCategories: null);

		_ = await sut.ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	private static InMemoryErasureStore NewStore() =>
		new(TestDataSubjectHasher.Instance, UntenantedContext.Instance, Options.Create(new TenantContextOptions()));

	private static async Task<Guid> ScheduleAsync(IErasureStore store)
	{
		var request = new ErasureRequest
		{
			DataSubjectId = "user-partial-hold",
			IdType = DataSubjectIdType.UserId,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			RequestedBy = "compliance-admin",
		};

		await store.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddSeconds(-1), CancellationToken.None)
			.ConfigureAwait(false);

		return request.RequestId;
	}

	private ErasureService BuildService(IErasureStore store, IReadOnlyList<string>? exemptCategories)
	{
		var holds = exemptCategories is null
			? new LegalHoldCheckResult { HasActiveHolds = false, ActiveHolds = [] }
			: new LegalHoldCheckResult
			{
				HasActiveHolds = true,
				ExemptCategories = exemptCategories,
				ActiveHolds =
				[
					new LegalHoldInfo
					{
						HoldId = Guid.NewGuid(),
						Basis = LegalHoldBasis.LegalObligation,
						CaseReference = "retention-schedule-7y",
						CreatedAt = DateTimeOffset.UtcNow,
					},
				],
			};

		A.CallTo(() => _legalHoldService.CheckHoldsAsync(
				A<string>._, A<DataSubjectIdType>._, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(holds));

		// AND the hash-accepting member, which is the one the EXECUTE path calls. The raw overload above
		// is still correct for the request-time check. Both are configured because a fake that answers
		// only one of them makes the other read as "no holds", which is how a double-hashing call site
		// passed its tests while never matching a real hold in production.
		A.CallTo(() => _legalHoldService.CheckHoldsByHashAsync(
				A<string>._, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(holds));

		A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult(KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow)));

		return new ErasureService(
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
			null,
			null,
			TestAnnotationSource.None,
			TestRetentions.None,
			null);
	}
}
