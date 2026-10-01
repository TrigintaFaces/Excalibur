// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Logging.Abstractions;

using Excalibur.Compliance.Erasure;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// Binds what a consumer gets when an erasure partly succeeds.
/// </summary>
/// <remarks>
/// <para>
/// A contributor that cannot erase its store makes the request <c>PartiallyCompleted</c>, and that used
/// to mean <b>no certificate at all</b> — the call threw, for every subject, permanently, because the
/// request is terminal and can never be re-run. What the consumer was left with is the status row's
/// error string: unsigned, unstructured, mutable, with no retention and no canonical form.
/// </para>
/// <para>
/// Refusing the certificate does not make the failure legible. It destroys the only durable, signed
/// record that the work which DID happen happened — and a controller cannot later produce one, because
/// the request will not run again.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class APartlySucceededErasureStillProducesEvidenceShould
{
	private readonly IErasureStore _store = A.Fake<IErasureStore>();
	private readonly IErasureCertificateStore _certStore = A.Fake<IErasureCertificateStore>();
	private readonly IKeyManagementAdmin _keyAdmin = A.Fake<IKeyManagementAdmin>();
	private readonly ILegalHoldService _legalHoldService = A.Fake<ILegalHoldService>();

	// LIVENESS, and it is the whole bead: a certificate must be produced at all.
	[Fact]
	public async Task Produce_a_certificate_even_though_a_contributor_could_not_erase_its_store()
	{
		var certificate = await ExecuteWithAFailingContributorAsync().ConfigureAwait(false);

		certificate.ShouldNotBeNull(
			"a partly-successful erasure must still yield signed evidence of what WAS done. Withholding "
			+ "it leaves the consumer with a mutable status string and no way to obtain the document "
			+ "later, because the request is terminal.");
	}

	// SAFETY. The evidence must name what was NOT done, or it overstates the erasure.
	[Fact]
	public async Task Name_the_store_it_could_not_reach()
	{
		var certificate = await ExecuteWithAFailingContributorAsync().ConfigureAwait(false);

		certificate.Payload.UnreachedData.ShouldNotBeNull(
			"a certificate for a partial erasure that records nothing outstanding asserts a more "
			+ "complete erasure than occurred");

		certificate.Payload.UnreachedData!.ShouldContain(
			entry => entry.StoreKind.Contains("Stubborn", StringComparison.Ordinal),
			"the entry must name the store that was not reached, so the controller knows where the "
			+ "data still is");
	}

	// SAFETY. The record must be un-confusable with a lawful exemption, which is the reading that would
	// turn an unmet obligation into a defensible retention.
	[Fact]
	public async Task Claim_no_lawful_basis_for_what_it_could_not_reach()
	{
		var certificate = await ExecuteWithAFailingContributorAsync().ConfigureAwait(false);

		certificate.Payload.UnreachedData!.ShouldAllBe(
			entry => !entry.LawfulBasisClaimed,
			"nothing in this list is lawfully retained; it is data the erasure did not reach");

		certificate.Payload.Exceptions.ShouldNotContain(
			e => e.DataCategory.Contains("Stubborn", StringComparison.Ordinal),
			"an unreached store must never appear among the Article 17(3) exemptions -- those carry a "
			+ "legal basis, and presenting a failure as an exemption is the defect this list prevents");
	}

	// SAFETY, and it is the arm that stops the evidence write from eating the erasure. Writing the
	// certificate signs a payload and resolves a store, so it CAN throw. If that throw reaches the
	// execution handler, the request is recorded Failed and the result carries no key count -- so an
	// erasure that destroyed the key reports as never having happened, on a terminal request the
	// consumer cannot re-run. The missing certificate is the lesser loss by a wide margin.
	[Fact]
	public async Task Still_report_the_keys_it_destroyed_when_the_certificate_cannot_be_written()
	{
		var requestId = Guid.NewGuid();
		GivenAnErasureThatDestroysOneKey(requestId);

		A.CallTo(() => _certStore.SaveCertificateAsync(A<ErasureCertificate>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("the certificate store is unreachable"));

		var execution = await NewService([new StubbornStore()])
			.ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		execution.KeysDeleted.ShouldBe(
			1,
			"the key WAS destroyed. A failure to write evidence about the erasure must not erase the "
			+ "record of the erasure -- the key is gone and the request is terminal, so a consumer told "
			+ "it did not happen can neither redo it nor prove it was done.");

		A.CallTo(() => _store.UpdateStatusAsync(
				requestId, ErasureRequestStatus.Failed, A<string?>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	private async Task<ErasureCertificate> ExecuteWithAFailingContributorAsync()
	{
		var requestId = Guid.NewGuid();
		GivenAnErasureThatDestroysOneKey(requestId);

		ErasureCertificate? saved = null;
		A.CallTo(() => _certStore.SaveCertificateAsync(A<ErasureCertificate>._, A<CancellationToken>._))
			.Invokes((ErasureCertificate c, CancellationToken _) => saved = c)
			.Returns(Task.CompletedTask);

		_ = await NewService([new StubbornStore()])
			.ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		saved.ShouldNotBeNull(
			"the execution path is the ONLY place the structured residue exists -- the persisted status "
			+ "keeps counts and one joined error string. If nothing is saved here, no honest certificate "
			+ "can be produced later.");

		return saved!;
	}

	private void GivenAnErasureThatDestroysOneKey(Guid requestId)
	{
		A.CallTo(() => _store.GetService(typeof(IErasureCertificateStore))).Returns(_certStore);
		A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(new ErasureStatus
			{
				RequestId = requestId,
				DataSubjectIdHash = "abc123hash",
				IdType = DataSubjectIdType.UserId,
				Scope = ErasureScope.User,
				LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
				Status = ErasureRequestStatus.Scheduled,
				RequestedAt = DateTimeOffset.UtcNow.AddHours(-1),
				RequestedBy = "admin",
				UpdatedAt = DateTimeOffset.UtcNow,
			}));
		A.CallTo(() => _store.UpdateStatusAsync(
				requestId, A<ErasureRequestStatus>._, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));

		A.CallTo(() => _legalHoldService.CheckHoldsAsync(
				A<string>._, A<DataSubjectIdType>._, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new LegalHoldCheckResult { HasActiveHolds = false, ActiveHolds = [] }));

		// Something must succeed, or the outcome is Failed rather than PartiallyCompleted and no
		// certificate is owed -- there would be nothing to attest.
		A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult(KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow)));

		A.CallTo(() => _certStore.GetCertificateAsync(A<Guid>._, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureCertificate?>(null));
	}

	private ErasureService NewService(IEnumerable<IErasureContributor> contributors) =>
		new ErasureService(
			_store,
			_keyAdmin,
			Microsoft.Extensions.Options.Options.Create(new ErasureOptions
			{
				KeyShredOnlyErasure = true,
				Retention = new ErasureRetentionOptions { SigningKey = new byte[32] },
			}),
			NullLogger<ErasureService>.Instance,
			TestDataSubjectHasher.Instance,
			_legalHoldService,
			null,
			null,
			TestRetentions.None,
			contributors);

	/// <summary>A contributor that runs and cannot do its job — the shape the projection gap has.</summary>
	private sealed class StubbornStore : IErasureContributor
	{
		public string Name => "StubbornStore";

		public IReadOnlySet<DataStoreKind> CoveredStoreKinds { get; } = new HashSet<DataStoreKind>();

		public Task<ErasureContributorResult> EraseAsync(
			ErasureContributorContext context,
			CancellationToken cancellationToken) =>
			Task.FromResult(new ErasureContributorResult
			{
				Success = false,
				ErrorMessage =
					"this store holds data derived from the erased records and is not notified when they "
					+ "are erased, so it keeps them until it is rebuilt",
			});
	}
}
