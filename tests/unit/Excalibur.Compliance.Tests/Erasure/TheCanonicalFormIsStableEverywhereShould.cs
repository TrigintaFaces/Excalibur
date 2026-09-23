// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;

using Excalibur.Compliance.Erasure;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// Binds the canonical form's exact bytes, and their independence from the machine that produces them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is the riskiest arm on the seam.</b> A signature is a promise about exact bytes. If the
/// canonical form varies with the runtime, the culture or the SDK that compiled it, a certificate signed on
/// one machine reports as altered on another — and the symptom a consumer sees is not "our serializer
/// changed", it is a compliance record accusing them of tampering. That failure is worse than shipping no
/// verifier at all, because a verifier that fires on correct data misinforms rather than merely leaving
/// someone uninformed.
/// </para>
/// <para>
/// <b>The golden vector is the part that catches an SDK change.</b> Pinning the options in the serializer
/// context makes omission inexpressible; it does not make the byte layout contractual. Only a literal
/// expected string does that. When this arm goes red, the question is never "update the string" — it is
/// "which change moved the bytes, and does it invalidate every signature already issued?"
/// </para>
/// <para>
/// <b>What this does not establish.</b> These arms run on one runtime, so they bind the form against
/// culture and against future edits to this repository. They cannot observe a different .NET version or a
/// different operating system; that remains unproven here, and the golden vector is what would report it
/// on the first machine that disagreed.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class TheCanonicalFormIsStableEverywhereShould
{
	private static readonly DateTimeOffset Fixed = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

	// Cultures chosen to break the things a hand-rolled canonical form gets wrong: de-DE renders a decimal
	// comma and groups digits; tr-TR is the classic casing trap, where "I".ToLower() is not "i"; ar-SA
	// defaults to a non-Gregorian calendar, which moves a date's year.
	public static TheoryData<string> HostileCultures => ["de-DE", "tr-TR", "ar-SA", "en-US", ""];

	/// <summary>
	/// The exact bytes the signature covers, pinned.
	/// </summary>
	[Fact]
	public void Render_a_payload_to_these_exact_bytes() =>
		ErasureCertificateCanonicalizer.ToCanonicalJson(Payload())
			.ShouldBe(
				Expected,
				"these are the bytes every signature already issued was computed over. If this arm is red, do "
				+ "not update the expected string until you know WHICH change moved the bytes -- a serializer "
				+ "or SDK change that alters the layout invalidates every certificate ever signed, and the "
				+ "consumer-visible symptom is a compliance record reporting as tampered with.");

	/// <summary>
	/// The same payload renders identically whatever culture the issuing host runs under.
	/// </summary>
	/// <remarks>
	/// A host in Berlin and a host in Ankara must produce the same document, or a certificate signed by one
	/// cannot be checked by the other. Numbers, dates, the <c>TimeSpan</c> on a retention exemption and the
	/// enum names all go through a formatter, and each is a place a culture could enter.
	/// </remarks>
	[Theory]
	[MemberData(nameof(HostileCultures))]
	public void Render_the_same_bytes_under_any_culture(string culture)
	{
		var original = CultureInfo.CurrentCulture;
		var originalUi = CultureInfo.CurrentUICulture;

		try
		{
			var hostile = CultureInfo.GetCultureInfo(culture);
			CultureInfo.CurrentCulture = hostile;
			CultureInfo.CurrentUICulture = hostile;

			ErasureCertificateCanonicalizer.ToCanonicalJson(Payload())
				.ShouldBe(
					Expected,
					$"a host running under '{culture}' must sign the same bytes as any other host, or a "
					+ "certificate issued by one reports as altered when checked by another.");
		}
		finally
		{
			CultureInfo.CurrentCulture = original;
			CultureInfo.CurrentUICulture = originalUi;
		}
	}

	/// <summary>
	/// A payload that goes to a store and comes back renders to the same bytes it went in with.
	/// </summary>
	/// <remarks>
	/// This is the property the SQL stores depend on: they persist the canonical form and restore the
	/// payload from it, so reading a certificate back and re-signing it must reproduce the original tag.
	/// The conformance kit proves it against real engines; this proves the serializer half without one.
	/// </remarks>
	[Fact]
	public void Survive_a_round_trip_through_its_own_canonical_form()
	{
		var canonical = ErasureCertificateCanonicalizer.ToCanonicalJson(Payload());

		ErasureCertificateCanonicalizer.ToCanonicalJson(ErasureCertificateCanonicalizer.FromCanonicalJson(canonical))
			.ShouldBe(canonical);
	}

	/// <summary>
	/// A stored payload that cannot be read back is refused, not replaced with a plausible one.
	/// </summary>
	[Theory]
	[InlineData("null")]
	[InlineData("{ not json")]
	public void Refuse_a_stored_payload_it_cannot_read_back(string stored) =>
		Should.Throw<InvalidOperationException>(() => ErasureCertificateCanonicalizer.FromCanonicalJson(stored));

	// The golden vector. A LITERAL, deliberately: computing it would make the culture arms compare a
	// value against itself under the same culture, which is the vacuous shape those arms exist to avoid.
	//
	// NOTE the flags order: "AuditLog, KeyManagementSystem" is ASCENDING ENUM VALUE, which is what the
	// serializer emits -- not the order the fixture happens to OR them in. Written out because the
	// initializer order is the intuitive guess and it is wrong; this literal was corrected against a
	// real run rather than reasoned about.
	private const string Expected =
		"""
		{"CertificateId":"22222222-2222-2222-2222-222222222222","RequestId":"11111111-1111-1111-1111-111111111111","DataSubjectReference":"subject-hash","RequestReceivedAt":"2026-03-03T05:06:07+00:00","CompletedAt":"2026-03-04T05:06:07+00:00","Method":"CryptographicErasure","Summary":{"KeysDeleted":5,"RecordsAffected":9,"DataCategories":["personal","contact"],"TablesAffected":["Users","Contacts"],"DataSizeBytes":10240},"Verification":{"Verified":true,"Methods":"AuditLog, KeyManagementSystem","VerifiedAt":"2026-03-04T05:06:07+00:00","ReportHash":"report-hash","DeletedKeyIds":["key-1","key-2"],"Warnings":["one warning"]},"LegalBasis":"DataNoLongerNecessary","Exceptions":[{"Basis":"LegalObligation","DataCategory":"billing-records","Reason":"retained under a statutory accounting obligation","RetentionPeriod":"02:00:00","HoldId":"33333333-3333-3333-3333-333333333333"}],"GeneratedAt":"2026-03-04T05:06:07+00:00","RetainUntil":"2033-03-04T05:06:07+00:00","Version":"2.0"}
		""";

	private static ErasureCertificatePayload Payload() =>
		new()
		{
			CertificateId = new Guid("22222222-2222-2222-2222-222222222222"),
			RequestId = new Guid("11111111-1111-1111-1111-111111111111"),
			DataSubjectReference = "subject-hash",
			RequestReceivedAt = Fixed.AddDays(-1),
			CompletedAt = Fixed,
			Method = ErasureMethod.CryptographicErasure,
			Summary = new ErasureSummary
			{
				KeysDeleted = 5,
				RecordsAffected = 9,
				DataCategories = ["personal", "contact"],
				TablesAffected = ["Users", "Contacts"],
				DataSizeBytes = 10240,
			},
			Verification = new VerificationSummary
			{
				Verified = true,
				Methods = VerificationMethod.KeyManagementSystem | VerificationMethod.AuditLog,
				VerifiedAt = Fixed,
				ReportHash = "report-hash",
				DeletedKeyIds = ["key-1", "key-2"],
				Warnings = ["one warning"],
			},
			LegalBasis = ErasureLegalBasis.DataNoLongerNecessary,
			Exceptions =
			[
				new ErasureException
				{
					Basis = LegalHoldBasis.LegalObligation,
					DataCategory = "billing-records",
					Reason = "retained under a statutory accounting obligation",
					RetentionPeriod = TimeSpan.FromHours(2),
					HoldId = new Guid("33333333-3333-3333-3333-333333333333"),
				},
			],
			GeneratedAt = Fixed,
			RetainUntil = Fixed.AddYears(7),
		};
}
