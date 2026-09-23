// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Erasure;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// Binds the interaction between certificate STORAGE and certificate SIGNING.
/// </summary>
/// <remarks>
/// <para>
/// Signing the payload whole makes a requirement of the stores that did not exist while the signature
/// covered three identity fields: <b>a stored certificate must come back byte-identical, or its signature
/// can never be recomputed.</b> Any field a store drops is a field the verifier will not see, so the
/// recomputed tag differs and the document reports as TAMPERED — not "incomplete".
/// </para>
/// <para>
/// This arm does not need a database. It reproduces what a lossy round-trip produces — a payload
/// reassembled without the fields the SQL certificate tables have no columns for — and asks the signer
/// the question a verifier would ask.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class RoundTrippedCertificateStillVerifiesShould
{
	private static readonly DateTimeOffset Fixed = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

	// SAFETY. A certificate that loses its Article 17(3) exemptions on the way through a store does not
	// merely under-report: it becomes unverifiable, because the signature covered the exemptions.
	[Fact]
	public void Fail_verification_when_a_store_drops_the_lawful_retention_record()
	{
		var key = new byte[32];

		var issued = Payload() with
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
		};

		// What the SQL certificate tables can actually hold: there is no exceptions column, so the row
		// mapper reassembles the payload with an empty list.
		var readBack = issued with { Exceptions = [] };

		ErasureCertificateSigner.Sign(readBack, key)
			.ShouldNotBe(
				ErasureCertificateSigner.Sign(issued, key),
				"a dropped exemption changes the signed bytes, so the round-tripped document cannot "
				+ "authenticate. The consumer-visible symptom is not a missing field -- it is a "
				+ "certificate that reports as tampered.");
	}

	// SAFETY. GeneratedAt has no column either, so a reassembled payload takes the property initializer's
	// DateTimeOffset.UtcNow -- a DIFFERENT value on every read of the same row.
	[Fact]
	public void Fail_verification_when_a_store_cannot_persist_the_generation_instant()
	{
		var key = new byte[32];
		var issued = Payload();
		var readBack = issued with { GeneratedAt = DateTimeOffset.UtcNow };

		ErasureCertificateSigner.Sign(readBack, key)
			.ShouldNotBe(ErasureCertificateSigner.Sign(issued, key));
	}

	// LIVENESS. Without this, a signer that returned a different string every call would satisfy both
	// arms above while telling us nothing. A LOSSLESS round-trip must still verify.
	[Fact]
	public void Still_verify_when_the_round_trip_loses_nothing()
	{
		var key = new byte[32];
		var issued = Payload();
		var readBack = issued with { };

		ErasureCertificateSigner.Sign(readBack, key)
			.ShouldBe(
				ErasureCertificateSigner.Sign(issued, key),
				"a store that returns what it was given must produce a certificate that still "
				+ "authenticates, or the requirement above is unsatisfiable rather than merely unmet.");
	}

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
