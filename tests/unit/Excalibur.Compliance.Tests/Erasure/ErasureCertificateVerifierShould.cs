// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Erasure;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// Binds the public certificate verifier: what it accepts, what it rejects, and what it refuses to judge.
/// </summary>
/// <remarks>
/// <para>
/// The three outcomes are not three shades of the same answer. <c>SignatureMismatch</c> says the document
/// was altered, which a consumer may be obliged to escalate; <c>NotVerifiable</c> says nothing was
/// established, which they must not escalate. Conflating them either hides a real alteration or
/// manufactures an incident out of a certificate that never carried content integrity.
/// </para>
/// <para>
/// <b>There is no lenient mode and no arm here asks for one.</b> If a future change adds a tolerance, the
/// mismatch arms below are what should go red.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ErasureCertificateVerifierShould
{
	private static readonly DateTimeOffset Fixed = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

	private static byte[] Key => new byte[32];

	/// <summary>LIVENESS. A certificate straight from the signer verifies.</summary>
	[Fact]
	public void Verify_a_certificate_whose_payload_is_the_one_that_was_signed() =>
		ErasureCertificateVerifier.Verify(Sign(Payload()), Key)
			.ShouldBe(
				ErasureCertificateVerificationResult.Verified,
				"without this the mismatch arms below are satisfied by a verifier that never says yes.");

	/// <summary>SAFETY. Altering any claim breaks the signature — here, the count of what was destroyed.</summary>
	[Fact]
	public void Report_a_mismatch_when_a_claim_is_altered_after_signing()
	{
		var issued = Sign(Payload());
		var altered = issued with { Payload = issued.Payload with { Summary = new ErasureSummary { KeysDeleted = 99 } } };

		ErasureCertificateVerifier.Verify(altered, Key)
			.ShouldBe(ErasureCertificateVerificationResult.SignatureMismatch);
	}

	/// <summary>SAFETY. The exemptions are claims like any other, and losing them is the classic alteration.</summary>
	[Fact]
	public void Report_a_mismatch_when_the_lawful_retention_record_is_removed()
	{
		var issued = Sign(Payload() with
		{
			Exceptions =
			[
				new ErasureException
				{
					Basis = LegalHoldBasis.LegalObligation,
					DataCategory = "billing-records",
					Reason = "retained under a statutory accounting obligation",
				},
			],
		});

		var stripped = issued with { Payload = issued.Payload with { Exceptions = [] } };

		ErasureCertificateVerifier.Verify(stripped, Key)
			.ShouldBe(ErasureCertificateVerificationResult.SignatureMismatch);
	}

	/// <summary>SAFETY. A different key does not authenticate the document.</summary>
	[Fact]
	public void Report_a_mismatch_under_a_key_the_certificate_was_not_signed_with()
	{
		var otherKey = new byte[32];
		otherKey[0] = 1;

		ErasureCertificateVerifier.Verify(Sign(Payload()), otherKey)
			.ShouldBe(ErasureCertificateVerificationResult.SignatureMismatch);
	}

	/// <summary>
	/// SAFETY. An earlier scheme's signature establishes nothing, and must not read as tampering.
	/// </summary>
	/// <remarks>
	/// A certificate issued before the signature covered the payload carries a bare Base64 tag. Its claims
	/// were never signed, so no later check can establish their integrity — but the document was not
	/// altered either, and reporting it as altered would send a consumer looking for an intrusion that did
	/// not happen.
	/// </remarks>
	[Fact]
	public void Refuse_to_judge_a_certificate_carrying_an_earlier_scheme_s_bare_tag()
	{
		var issued = Sign(Payload());
		var legacy = issued with { Signature = issued.Signature[(issued.Signature.IndexOf(':', StringComparison.Ordinal) + 1)..] };

		ErasureCertificateVerifier.Verify(legacy, Key)
			.ShouldBe(ErasureCertificateVerificationResult.NotVerifiable);
	}

	/// <summary>SAFETY. An unknown scheme is refused rather than checked against the one we happen to have.</summary>
	[Fact]
	public void Refuse_to_judge_a_certificate_whose_tag_names_a_scheme_this_build_does_not_implement()
	{
		var issued = Sign(Payload());

		ErasureCertificateVerifier.Verify(issued with { Signature = "v3" + issued.Signature[2..] }, Key)
			.ShouldBe(ErasureCertificateVerificationResult.NotVerifiable);
	}

	/// <summary>
	/// SAFETY. The tag's scheme and the payload's own version must agree, or nothing is established.
	/// </summary>
	/// <remarks>
	/// This is the downgrade oracle closed. A document that authenticates under one canonical form while
	/// declaring another is a document a verifier could be steered into reading the weaker way.
	/// </remarks>
	[Fact]
	public void Refuse_to_judge_a_certificate_whose_payload_version_disagrees_with_its_tag()
	{
		var issued = Sign(Payload());

		ErasureCertificateVerifier.Verify(issued with { Payload = issued.Payload with { Version = "1.0" } }, Key)
			.ShouldBe(ErasureCertificateVerificationResult.NotVerifiable);
	}

	/// <summary>SAFETY. A truncated or empty tag establishes nothing.</summary>
	[Theory]
	[InlineData("")]
	[InlineData("v2:")]
	[InlineData("v2:not-base64!!")]
	[InlineData("v2:c2hvcnQ=")]
	public void Refuse_to_judge_a_certificate_whose_tag_is_not_well_formed(string signature) =>
		ErasureCertificateVerifier.Verify(Sign(Payload()) with { Signature = signature }, Key)
			.ShouldBe(ErasureCertificateVerificationResult.NotVerifiable);

	/// <summary>
	/// SAFETY. Verifying with no key is a caller's mistake, not a verdict about the document.
	/// </summary>
	/// <remarks>
	/// Returning a mismatch here would report every certificate in a host that forgot its key as altered —
	/// a security incident manufactured out of a missing setting, on exactly the document where a consumer
	/// can least afford a false one.
	/// </remarks>
	[Fact]
	public void Refuse_to_verify_at_all_when_no_key_is_supplied() =>
		Should.Throw<InvalidOperationException>(() => ErasureCertificateVerifier.Verify(Sign(Payload()), []));

	/// <summary>SAFETY. The signer will not issue a document that names a scheme it did not run.</summary>
	[Fact]
	public void Refuse_to_sign_a_payload_declaring_another_scheme_s_version() =>
		Should.Throw<InvalidOperationException>(
			() => ErasureCertificateSigner.Sign(Payload() with { Version = "1.0" }, Key));

	/// <summary>The tag carries its scheme, so a verifier need not parse an unauthenticated payload to pick one.</summary>
	[Fact]
	public void Emit_a_signature_that_names_the_scheme_that_produced_it() =>
		ErasureCertificateSigner.Sign(Payload(), Key).ShouldStartWith("v2:");

	private static ErasureCertificate Sign(ErasureCertificatePayload payload) =>
		new() { Payload = payload, Signature = ErasureCertificateSigner.Sign(payload, Key) };

	private static ErasureCertificatePayload Payload() =>
		new()
		{
			CertificateId = new Guid("22222222-2222-2222-2222-222222222222"),
			RequestId = new Guid("11111111-1111-1111-1111-111111111111"),
			DataSubjectReference = "subject-hash",
			RequestReceivedAt = Fixed.AddDays(-1),
			CompletedAt = Fixed,
			Method = ErasureMethod.CryptographicErasure,
			Summary = new ErasureSummary { KeysDeleted = 5, RecordsAffected = 9 },
			Verification = new VerificationSummary
			{
				Verified = true,
				Methods = VerificationMethod.KeyManagementSystem,
				VerifiedAt = Fixed,
			},
			LegalBasis = ErasureLegalBasis.DataNoLongerNecessary,
			GeneratedAt = Fixed,
			RetainUntil = Fixed.AddYears(7),
		};
}
