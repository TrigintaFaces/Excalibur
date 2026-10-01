// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Erasure;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// What a contributor lawfully KEPT must reach the certificate on the failure path as well as the
/// success path.
/// </summary>
/// <remarks>
/// <para>
/// <b>What was kept is a property of the STORE; whether the same pass also failed is a property of the
/// RUN.</b> Reading the retentions only off a successful contributor result makes one briefly
/// unreachable read model enough to strike every retention from the record — and the partial-erasure
/// certificate is the one a controller reconciles by hand, so it is the one that most needs the list.
/// </para>
/// <para>
/// The failure is silent by construction: the certificate is signed, it verifies, it names the store
/// that was not reached, and it simply does not mention that a whole aggregate type survived intact.
/// Nothing downstream ever learns otherwise, which is what makes this class of defect catastrophic
/// rather than merely wrong.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ARetainedTypeIsNamedEvenOnAPartialErasureShould
{
	private const string RetainedType = "SalesRecord";
	private const string Justification =
		"Vehicle sales records are kept for six years under the tax code's record-keeping requirement.";

	private readonly IErasureStore _store = A.Fake<IErasureStore>();
	private readonly IErasureCertificateStore _certStore = A.Fake<IErasureCertificateStore>();
	private readonly IKeyManagementAdmin _keyAdmin =
		KeyDestructionFakes.AdminThatReportsAGenerationForEveryKey();
	private readonly ILegalHoldService _legalHolds = A.Fake<ILegalHoldService>();

	/// <summary>
	/// SAFETY. A contributor that both retained and failed has its retention on the certificate.
	/// </summary>
	/// <remarks>
	/// RED input, either half independently: return the one-argument <c>Failed</c> from the event-store
	/// contributor's error path, or harvest <c>RetainedData</c> only from successful results in
	/// <c>InvokeContributorsAsync</c>. Both make this arm fail, and both leave every other arm green.
	/// </remarks>
	[Fact]
	public async Task Carry_the_retention_onto_the_certificate_when_the_same_pass_also_failed()
	{
		var certificate = await ExecuteWithAContributorThatRetainedAndFailedAsync();

		certificate.Payload.Exceptions.ShouldContain(
			entry => string.Equals(entry.DataCategory, RetainedType, StringComparison.Ordinal),
			"a certificate that does not name what was lawfully kept cannot be told from one over data "
			+ "that was destroyed, and the reader has no other source for the difference");
	}

	/// <summary>
	/// SAFETY. The entry carries the basis and the justification — not merely the name — and it does NOT
	/// state a retention length with no instant to measure it from.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This arm used to assert the declared six-year period was carried, which was the defect rather than
	/// the requirement. A duration with no anchor reads as computable, and the only instant a reader has in
	/// front of them is the certificate's own date — anchoring there restarts a statutory clock at the moment
	/// the data subject asked to be erased, so the document becomes evidence for a longer retention than the
	/// obligation supports. The assertion is flipped rather than relaxed: it now binds the honest contract.
	/// </para>
	/// <para>
	/// RED input: emit the contributor's duration onto the signed payload again. The basis and justification
	/// assertions are unchanged, so this arm still fails if the entry loses what it DOES establish.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Carry_the_basis_and_justification_without_an_unanchored_period()
	{
		var certificate = await ExecuteWithAContributorThatRetainedAndFailedAsync();

		var entry = certificate.Payload.Exceptions.ShouldHaveSingleItem();
		entry.Basis.ShouldBe(LegalHoldBasis.LegalObligation);
		entry.Reason.ShouldBe(Justification, "an auditor evaluates the obligation, not the ground's name");
		entry.RetentionPeriod.ShouldBeNull(
			"a reader cannot obtain an end from a duration this document does not anchor, so stating one "
			+ "invites them to anchor it on the certificate's own date and read a longer retention than the "
			+ "obligation gives");
	}

	/// <summary>
	/// LIVENESS. A retained type is not confused with a store the erasure failed to reach: the two lists
	/// are siblings and an entry belongs to exactly one of them.
	/// </summary>
	[Fact]
	public async Task Keep_a_lawful_retention_out_of_the_unreached_list()
	{
		var certificate = await ExecuteWithAContributorThatRetainedAndFailedAsync();

		certificate.Payload.UnreachedData?.ShouldNotContain(
			entry => entry.StoreKind.Contains(RetainedType, StringComparison.Ordinal),
			"data kept under a legal basis is not data the erasure failed to reach; presenting one as "
			+ "the other turns an unmet obligation into a defensible retention, or the reverse");
	}

	private async Task<ErasureCertificate> ExecuteWithAContributorThatRetainedAndFailedAsync()
	{
		var requestId = Guid.NewGuid();
		GivenAnErasureThatDestroysOneKey(requestId);

		ErasureCertificate? saved = null;
		A.CallTo(() => _certStore.SaveCertificateAsync(A<ErasureCertificate>._, A<CancellationToken>._))
			.Invokes((ErasureCertificate c, CancellationToken _) => saved = c)
			.Returns(Task.CompletedTask);

		_ = await NewService([new RetainedAndFailed()])
			.ExecuteAsync(requestId, CancellationToken.None);

		saved.ShouldNotBeNull(
			"the execution path is the only place the structured residue exists; if nothing is saved "
			+ "here there is no honest certificate to produce later");

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

		A.CallTo(() => _legalHolds.CheckHoldsAsync(
				A<string>._, A<DataSubjectIdType>._, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new LegalHoldCheckResult { HasActiveHolds = false, ActiveHolds = [] }));

		// Something must succeed, or the outcome is Failed rather than PartiallyCompleted and no
		// certificate is owed — there would be nothing to attest.
		A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult(KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow)));

		A.CallTo(() => _certStore.GetCertificateAsync(A<Guid>._, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureCertificate?>(null));
	}

	private ErasureService NewService(IEnumerable<IErasureContributor> contributors) =>
		new(_store,
			_keyAdmin,
			Microsoft.Extensions.Options.Options.Create(new ErasureOptions
			{
				KeyShredOnlyErasure = true,
				Retention = new ErasureRetentionOptions { SigningKey = new byte[32] },
			}),
			NullLogger<ErasureService>.Instance,
			TestDataSubjectHasher.Instance,
			_legalHolds,
			null,
			null,
			TestRetentions.None,
			contributors);

	/// <summary>
	/// The shape the event-store contributor has when one aggregate type is lawfully retained and an
	/// unrelated aggregate's store could not be reached in the same pass.
	/// </summary>
	private sealed class RetainedAndFailed : IErasureContributor
	{
		public string Name => "EventStore";

		public IReadOnlySet<DataStoreKind> CoveredStoreKinds { get; } = new HashSet<DataStoreKind>();

		public Task<ErasureContributorResult> EraseAsync(
			ErasureContributorContext context,
			CancellationToken cancellationToken) =>
			Task.FromResult(ErasureContributorResult.Failed(
				"Partial erasure: 3 events erased, 1 failure. First error: the read model is unreachable.",
				[
					new ErasureException
					{
						Basis = LegalHoldBasis.LegalObligation,
						DataCategory = RetainedType,
						Reason = Justification,
						RetentionPeriod = TimeSpan.FromDays(365 * 6),
					},
				]));
	}
}
