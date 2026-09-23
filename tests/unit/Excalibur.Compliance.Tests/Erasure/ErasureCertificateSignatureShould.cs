// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Logging.Abstractions;

using Excalibur.Compliance.Erasure;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// Binds what the erasure certificate's signature must cover.
/// </summary>
/// <remarks>
/// <para>
/// <b>What a signature on this document is for.</b> The certificate is the artifact a consumer hands to a
/// regulator to evidence an erasure. Signing it makes a promise to that reader: the claims in this
/// document are the claims we made, and an alteration is detectable. That promise is the only reason a
/// signature belongs here at all.
/// </para>
/// <para>
/// <b>What it covers today.</b> Three identity fields — the request id, the subject hash, and the
/// completion time. Not whether verification succeeded, not how many keys were destroyed, not how many
/// records were affected, not which categories or tables were touched, and not the method. So two
/// certificates that disagree about everything a reader cares about carry the <i>same</i> signature, and
/// editing any claim leaves a document that still authenticates.
/// </para>
/// <para>
/// <b>Why that is worse than an unsigned document.</b> An unsigned certificate makes no promise and is
/// read accordingly. A signed one invites the reader to rely on the signature — and the shipped
/// configuration guidance asks consumers to supply a signing key, from a secret manager, for exactly this
/// purpose. A promise of tamper evidence that does not cover the tampering a reader would care about is
/// not a weaker guarantee; it is a misleading one, and we are the party making it.
/// </para>
/// <para>
/// <b>The arms are split deliberately.</b> One is RED at the time of writing and names the defect. The
/// others pin behaviour that is already correct and must survive the fix — in particular the refusal to
/// emit a keyless signature, which is right, was argued for, and is the thing most likely to be discarded
/// by someone rewriting this method.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ErasureCertificateSignatureShould
{
	private static readonly DateTimeOffset FixedCompletion =
		new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

	private readonly IErasureStore _store = A.Fake<IErasureStore>();
	private readonly IErasureCertificateStore _certStore = A.Fake<IErasureCertificateStore>();
	private readonly IKeyManagementAdmin _keyAdmin = A.Fake<IKeyManagementAdmin>();
	private readonly ILegalHoldService _legalHoldService = A.Fake<ILegalHoldService>();
	private readonly IDataInventoryService _dataInventoryService = A.Fake<IDataInventoryService>();

	public ErasureCertificateSignatureShould()
	{
		A.CallTo(() => _store.GetService(typeof(IErasureCertificateStore))).Returns(_certStore);
		A.CallTo(() => _certStore.GetCertificateAsync(A<Guid>._, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureCertificate?>(null));
	}

	/// <summary>
	/// SAFETY, and RED at the time of writing. The signature must distinguish certificates whose CLAIMS
	/// differ, not merely ones whose identity differs.
	/// </summary>
	/// <remarks>
	/// Both certificates below describe the same request, the same subject and the same completion
	/// instant. They disagree about whether anything was verified and about how much was destroyed —
	/// which is the entire content a regulator reads. If those two documents carry the same signature,
	/// the signature authenticates the envelope and not the statement inside it.
	/// </remarks>
	[Fact]
	public async Task Distinguish_two_certificates_that_disagree_about_what_was_erased()
	{
		var requestId = Guid.NewGuid();

		var nothingErased = await GenerateAsync(requestId, keysDeleted: 0, recordsAffected: 0)
			.ConfigureAwait(false);
		var muchErased = await GenerateAsync(requestId, keysDeleted: 97, recordsAffected: 4210)
			.ConfigureAwait(false);

		// Guard: the two really do differ in what they claim, so a passing arm cannot be explained by the
		// fixture having produced the same certificate twice.
		muchErased.Payload.Summary.KeysDeleted.ShouldNotBe(nothingErased.Payload.Summary.KeysDeleted);
		muchErased.Payload.Summary.RecordsAffected.ShouldNotBe(nothingErased.Payload.Summary.RecordsAffected);

		// This guard used to compare Verification.Verified, which varied only because the reconstruction
		// path once stamped Verified=true for a zero-key erasure. That was itself a false attestation and
		// was fixed, so the field is now constant-false on this path and can no longer discriminate. The
		// arm's REQUIREMENT is unchanged -- Verified is still inside the signed payload and still covered;
		// only the fixture's way of proving the two documents differ has moved to a claim that still varies.

		muchErased.Signature.ShouldNotBe(
			nothingErased.Signature,
			"these two certificates describe the same request and the same subject, and disagree about "
			+ "whether anything was verified and how much was destroyed. An identical signature means the "
			+ "signature covers the identity of the document and none of its claims -- so every claim can "
			+ "be altered and the certificate still authenticates. The reader we are making that promise "
			+ "to is a regulator.");
	}

	/// <summary>
	/// LIVENESS. The signing is not simply constant — identity still changes it.
	/// </summary>
	/// <remarks>
	/// Without this, a signer that returned the same string for everything would satisfy nothing above
	/// but would also be indistinguishable from the real defect when someone comes to fix it. This arm
	/// says the mechanism works; the arm above says it is pointed at too little.
	/// </remarks>
	[Fact]
	public async Task Still_distinguish_certificates_for_different_requests()
	{
		var first = await GenerateAsync(Guid.NewGuid(), keysDeleted: 1, recordsAffected: 1)
			.ConfigureAwait(false);
		var second = await GenerateAsync(Guid.NewGuid(), keysDeleted: 1, recordsAffected: 1)
			.ConfigureAwait(false);

		first.Signature.ShouldNotBeNullOrEmpty();
		second.Signature.ShouldNotBe(
			first.Signature,
			"two different requests must not share a signature; if they did the value would carry no "
			+ "information at all.");
	}

	/// <summary>
	/// LIVENESS. The same payload signs identically, so the value is reproducible by a verifier.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A signature nobody can recompute cannot be checked. This is the property a future verifier will
	/// depend on, and it is worth pinning before one exists.
	/// </para>
	/// <para>
	/// <b>DO NOT restore the previous version of this arm.</b> It generated the certificate twice through
	/// the service and asserted the two signatures matched. That assertion could only hold while the
	/// signature ignored <see cref="ErasureCertificatePayload.CertificateId"/> and
	/// <see cref="ErasureCertificatePayload.GeneratedAt"/> — both of which are fresh on every generation.
	/// In other words it passed BECAUSE of the forgeability defect, and it went red when that defect was
	/// fixed. Two certificates with different identifiers and different generation instants are two
	/// DOCUMENTS; a scheme that gives them one tag is a scheme that cannot tell them apart, which is the
	/// bug one level up. The property the old name meant is determinism of the signing FUNCTION, and that
	/// is what is asserted here: one payload, signed twice, same bytes.
	/// </para>
	/// <para>
	/// The old flow was not a supported consumer path in any case — the store refuses a second certificate
	/// for a request that already has one.
	/// </para>
	/// </remarks>
	[Fact]
	public void Produce_the_same_signature_for_the_same_payload_twice()
	{
		var payload = PayloadWith(Guid.NewGuid());
		var key = new byte[32];

		ErasureCertificateSigner.Sign(payload, key)
			.ShouldBe(ErasureCertificateSigner.Sign(payload, key));
	}

	/// <summary>
	/// SAFETY. The document's own identity is a claim, so altering it must break the signature.
	/// </summary>
	/// <remarks>
	/// Pins the behaviour that replaced the old determinism assertion, so nobody "fixes" the signature back
	/// to ignoring identity. A regulator reads "this certificate, with this id, was generated at this
	/// instant". If those are outside the signature they are alterable on a document that still
	/// authenticates — which is the whole defect this signing scheme exists to close.
	/// </remarks>
	[Fact]
	public void Distinguish_two_payloads_that_differ_only_in_certificate_id()
	{
		var key = new byte[32];
		var first = PayloadWith(Guid.NewGuid());
		var second = first with { CertificateId = Guid.NewGuid() };

		ErasureCertificateSigner.Sign(second, key)
			.ShouldNotBe(
				ErasureCertificateSigner.Sign(first, key),
				"the certificate id is part of what the document asserts, so two documents carrying "
				+ "different ids must not share one signature.");
	}

	/// <summary>
	/// SAFETY. A keyless certificate is refused rather than emitted with a forgeable digest.
	/// </summary>
	/// <remarks>
	/// This behaviour is already correct and is pinned so a rewrite cannot quietly drop it. A keyless
	/// SHA-256 digest is not a signature — anyone can recompute it, so it carries no tamper evidence
	/// while looking exactly like something that does. Refusing to issue is the honest outcome, and it is
	/// the arm most at risk from someone "simplifying" the signing path.
	/// </remarks>
	[Fact]
	public async Task Refuse_to_issue_a_certificate_when_no_signing_key_is_configured()
	{
		var requestId = Guid.NewGuid();
		GivenCompletedStatus(requestId, keysDeleted: 3, recordsAffected: 7);

		var keyless = CreateService(signingKey: []);

		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => keyless.GenerateCertificateAsync(requestId, CancellationToken.None)).ConfigureAwait(false);
	}

	// A payload whose every field is FIXED, so the only thing a signature can differ on is what an arm
	// deliberately changes. The service's own payloads carry a fresh CertificateId and GeneratedAt on every
	// call, which is exactly why signing determinism cannot be observed through the service at all.
	private static ErasureCertificatePayload PayloadWith(Guid certificateId) =>
		new()
		{
			CertificateId = certificateId,
			RequestId = new Guid("11111111-1111-1111-1111-111111111111"),
			DataSubjectReference = "subject-hash",
			RequestReceivedAt = FixedCompletion.AddDays(-1),
			CompletedAt = FixedCompletion,
			Method = ErasureMethod.CryptographicErasure,
			Summary = new ErasureSummary { KeysDeleted = 5, RecordsAffected = 9 },
			Verification = new VerificationSummary
			{
				Verified = true,
				Methods = VerificationMethod.KeyManagementSystem,
				VerifiedAt = FixedCompletion,
			},
			LegalBasis = ErasureLegalBasis.DataNoLongerNecessary,
			GeneratedAt = FixedCompletion,
			RetainUntil = FixedCompletion.AddYears(7),
		};

	private async Task<ErasureCertificate> GenerateAsync(Guid requestId, int keysDeleted, int recordsAffected)
	{
		GivenCompletedStatus(requestId, keysDeleted, recordsAffected);

		return await CreateService(signingKey: new byte[32])
			.GenerateCertificateAsync(requestId, CancellationToken.None)
			.ConfigureAwait(false);
	}

	private ErasureService CreateService(byte[] signingKey) =>
		new(
			_store,
			_keyAdmin,
			Microsoft.Extensions.Options.Options.Create(new ErasureOptions
			{
				Retention = new ErasureRetentionOptions { SigningKey = signingKey },
			}),
			NullLogger<ErasureService>.Instance,
			TestDataSubjectHasher.Instance,
			_legalHoldService,
			_dataInventoryService,
			null,
			TestAnnotationSource.None,
			[]);

	private void GivenCompletedStatus(Guid requestId, int keysDeleted, int recordsAffected)
	{
		A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(new ErasureStatus
			{
				RequestId = requestId,
				Status = ErasureRequestStatus.Completed,

				// Identity is held FIXED across the arms above on purpose. The three fields the signature
				// currently covers are the request id, this hash and the completion instant -- so holding
				// them constant is what isolates the question "does anything else reach the signature?".
				DataSubjectIdHash = "subject-hash",
				CompletedAt = FixedCompletion,

				IdType = DataSubjectIdType.UserId,
				Scope = ErasureScope.User,
				LegalBasis = ErasureLegalBasis.DataSubjectRequest,
				RequestedAt = FixedCompletion.AddMinutes(-10),
				RequestedBy = "operator",
				UpdatedAt = FixedCompletion,
				KeysDeleted = keysDeleted,
				RecordsAffected = recordsAffected,
			}));
	}
}
