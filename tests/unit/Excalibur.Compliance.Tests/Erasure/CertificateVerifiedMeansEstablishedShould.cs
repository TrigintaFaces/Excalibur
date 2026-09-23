// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Logging.Abstractions;

using Excalibur.Compliance.Erasure;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// Binds <c>VerificationSummary.Verified</c> to the one claim the issuing path can positively establish.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect this arm exists for is a flag that could not go false.</b> The live path computed
/// <c>Verified</c> as <c>deletedKeyIds.Count == keysDeleted</c>, and the two are incremented in the same
/// branch of key destruction, one line apart. The comparison restated an assignment; it tested nothing,
/// and it was true on every certificate the framework has ever issued. A check that cannot fail is not a
/// check, and on a document a data subject or a regulator reads, it is a claim nothing stands behind.
/// </para>
/// <para>
/// <b>The honest claim is narrow and that is the point.</b> An erasure reaches certificate issuance only
/// with the whole coverage gate clean, so <c>Verified = false</c> is not "the erasure failed". It means
/// the framework did not establish the destruction itself — the ordinary outcome of an erasure discharged
/// entirely by record deletion, where contributors reported erasing rows and the framework recorded their
/// reports. It took their word. Saying so is the difference between a certificate and an assertion.
/// </para>
/// <para>
/// <b>Why the consistency arm is here rather than left to review.</b> <c>Verified</c> and <c>Methods</c>
/// are two renderings of one fact, and the cheapest wrong fix for the arm below is to loosen one of them
/// alone. A certificate reading <c>Verified = true, Methods = None</c> — verified, by nothing — would then
/// be expressible, and it is the exact shape an auditor cannot be handed.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class CertificateVerifiedMeansEstablishedShould
{
	private readonly IErasureStore _store = A.Fake<IErasureStore>();
	private readonly IErasureCertificateStore _certStore = A.Fake<IErasureCertificateStore>();
	private readonly IKeyManagementAdmin _keyAdmin = A.Fake<IKeyManagementAdmin>();
	private readonly ILegalHoldService _legalHoldService = A.Fake<ILegalHoldService>();
	private readonly IDataInventoryService _dataInventoryService = A.Fake<IDataInventoryService>();

	// SAFETY, and the arm the fix exists for. No key was destroyed, so the framework confirmed nothing;
	// the certificate must not claim it verified anything. RED on the pre-fix expression, which returns
	// true here because zero equals zero.
	[Fact]
	public async Task Refuse_to_claim_verification_when_no_destruction_was_established()
	{
		var certificate = await IssueAsync(keyDestructionSucceeds: false).ConfigureAwait(false);

		certificate.Payload.Verification.DeletedKeyIds.ShouldBeEmpty(
			"precondition: this erasure destroyed no key, so there is nothing for it to have verified");

		certificate.Payload.Verification.Verified.ShouldBeFalse(
			"the framework established no destruction here — the erasure was discharged without one — so "
			+ "Verified must read 'not established'. Claiming verification over an empty set of key ids is "
			+ "an attestation of nothing, and the signature covers the claim.");
	}

	// LIVENESS. Without it, hardcoding false satisfies the arm above while telling a regulator nothing,
	// and the fix would be indistinguishable from deleting the field.
	[Fact]
	public async Task Claim_verification_when_the_provider_confirmed_the_destruction()
	{
		var certificate = await IssueAsync(keyDestructionSucceeds: true).ConfigureAwait(false);

		certificate.Payload.Verification.DeletedKeyIds.ShouldNotBeEmpty(
			"precondition: a key was destroyed and the certificate names it");

		certificate.Payload.Verification.Verified.ShouldBeTrue(
			"the key-management provider reported this key irrecoverable, which is destruction the "
			+ "framework positively established and may attest");
	}

	// SAFETY, structural. Verified and Methods render one fact, so they may never disagree. Run over both
	// inputs, because a fix that loosened only one of them passes whichever arm it did not touch.
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Never_claim_verification_by_a_method_it_did_not_perform(bool keyDestructionSucceeds)
	{
		var verification = (await IssueAsync(keyDestructionSucceeds).ConfigureAwait(false))
			.Payload.Verification;

		if (verification.Verified)
		{
			verification.Methods.ShouldNotBe(
				VerificationMethod.None,
				"'verified by nothing' is not a claim a certificate may carry; if Verified is true, the "
				+ "document must name what performed the verification");
			verification.DeletedKeyIds.ShouldNotBeEmpty(
				"a verified erasure must name what it erased, or the claim cannot be checked by anyone");
		}
		else
		{
			verification.Methods.ShouldBe(
				VerificationMethod.None,
				"nothing was established, so no verification method ran and None is the enum's own word "
				+ "for that");
		}
	}

	/// <summary>
	/// Executes a real erasure through <see cref="ErasureService"/> and returns the certificate it issued.
	/// </summary>
	/// <param name="keyDestructionSucceeds">
	/// When <see langword="true"/> the key-management admin reports the subject key irrecoverable, so the
	/// execution collects its id. When <see langword="false"/> it reports the key absent, which is an
	/// idempotent no-op that collects nothing and is the ordinary shape of a record-deletion erasure.
	/// </param>
	/// <returns>The issued certificate.</returns>
	private async Task<ErasureCertificate> IssueAsync(bool keyDestructionSucceeds)
	{
		var requestId = Guid.NewGuid();

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

		// Completed is the only destruction state that may be attested as erased. NotFound is an idempotent
		// no-op: it collects no id, which is the "established nothing" input.
		A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult(keyDestructionSucceeds
				? KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow)
				: KeyDestructionOutcome.NotFound));

		ErasureCertificate? saved = null;
		A.CallTo(() => _certStore.SaveCertificateAsync(A<ErasureCertificate>._, A<CancellationToken>._))
			.Invokes((ErasureCertificate c, CancellationToken _) => saved = c)
			.Returns(Task.CompletedTask);
		A.CallTo(() => _certStore.GetCertificateAsync(A<Guid>._, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureCertificate?>(null));

		var sut = new ErasureService(
			_store,
			_keyAdmin,
			Microsoft.Extensions.Options.Options.Create(new ErasureOptions
			{
				// This arm is about what the certificate may CLAIM, so the coverage gate is taken off the
				// critical path by the documented opt-in rather than by a fixture that fakes coverage.
				KeyShredOnlyErasure = true,
				Retention = new ErasureRetentionOptions { SigningKey = new byte[32] },
			}),
			NullLogger<ErasureService>.Instance,
			TestDataSubjectHasher.Instance,
			_legalHoldService,
			_dataInventoryService,
			null,
			TestAnnotationSource.None,
			null);

		_ = await sut.ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		return saved.ShouldNotBeNull(
			"the execution must have issued a certificate, or these arms assert nothing");
	}
}
