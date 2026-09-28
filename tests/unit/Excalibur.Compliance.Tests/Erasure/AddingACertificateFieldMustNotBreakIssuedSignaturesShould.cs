// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Erasure;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// Binds the property that makes it safe to add a field to a SIGNED, already-issued document.
/// </summary>
/// <remarks>
/// <para>
/// The signature covers the payload's canonical JSON, and verification recomputes that form from the
/// DESERIALIZED payload. The serializer context sets <c>DefaultIgnoreCondition = Never</c>, so a property
/// that always serializes is emitted even when it holds nothing — which changes the canonical bytes of
/// every certificate issued before the property existed, and makes each of them verify as a FORGERY.
/// </para>
/// <para>
/// <b>A round-trip through the current code cannot detect this.</b> Both halves of a round-trip use the
/// same serializer, so both agree; the disagreement is with documents already in the field. The arms
/// below therefore assert the shape of the canonical form directly rather than round-tripping.
/// </para>
/// <para>
/// This is not specific to the field that prompted it. Any future property added here inherits the same
/// requirement, which is why the first arm asserts the general rule — a certificate with nothing
/// outstanding serializes as though the field did not exist.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AddingACertificateFieldMustNotBreakIssuedSignaturesShould
{
	private static readonly DateTimeOffset Fixed = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

	// SAFETY. The arm that fails if the new property is ever made unconditionally serialized.
	[Fact]
	public void Omit_the_unreached_record_entirely_when_there_is_nothing_outstanding()
	{
		var canonical = ErasureCertificateCanonicalizer.ToCanonicalJson(Payload());

		canonical.Contains("nreachedData", StringComparison.Ordinal).ShouldBeFalse(
			"a certificate with nothing outstanding must serialize exactly as it did before the field "
			+ "existed. The signature covers this text and verification recomputes it, so emitting the "
			+ "property here would make every certificate already issued verify as a forgery. Canonical "
			+ "form was: " + canonical);
	}

	// LIVENESS. Without this, the arm above is satisfied by a property that never serializes at all,
	// which would mean the record never reaches the document it exists to appear on.
	[Fact]
	public void Include_the_unreached_record_when_something_was_not_reached()
	{
		var payload = Payload() with
		{
			UnreachedData =
			[
				new UnreachedDataLocation
				{
					StoreKind = "Projection",
					Mechanism = "erasure does not notify read models",
					ControllerObligation = "the controller must clear the read model",
					RemediationCost = RemediationCost.RequiresReadModelOffline,
				},
			],
		};

		var canonical = ErasureCertificateCanonicalizer.ToCanonicalJson(payload);

		canonical.Contains("nreachedData", StringComparison.Ordinal).ShouldBeTrue(
			"an outstanding obligation must appear in the SIGNED form; a record outside the signature "
			+ "could be removed from the document without invalidating it");
		canonical.Contains("Projection", StringComparison.Ordinal).ShouldBeTrue(
			"the entry must name the store kind that was not reached");
	}

	// SAFETY. The record must never be mistakable for a lawful exemption, which is the one reading that
	// would turn a failure into a defensible retention.
	[Fact]
	public void State_that_no_lawful_basis_is_claimed_rather_than_leaving_it_absent()
	{
		var entry = new UnreachedDataLocation
		{
			StoreKind = "Projection",
			Mechanism = "erasure does not notify read models",
			ControllerObligation = "the controller must clear the read model",
			RemediationCost = RemediationCost.RequiresReadModelOffline,
		};

		entry.LawfulBasisClaimed.ShouldBeFalse(
			"an absent basis is arguable -- someone reading adversarially could claim the silence implied "
			+ "one. The document states it.");

		var canonical = ErasureCertificateCanonicalizer.ToCanonicalJson(
			Payload() with { UnreachedData = [entry] });

		canonical.Contains("awfulBasisClaimed", StringComparison.Ordinal).ShouldBeTrue(
			"the statement must be IN the signed form, not merely available on the object");
	}

	private static ErasureCertificatePayload Payload() => new()
	{
		CertificateId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
		RequestId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
		DataSubjectReference = "subject-hash",
		RequestReceivedAt = Fixed,
		CompletedAt = Fixed.AddHours(1),
		Method = ErasureMethod.CryptographicErasure,
		Summary = new ErasureSummary { KeysDeleted = 1, RecordsAffected = 2 },
		Verification = new VerificationSummary
		{
			Verified = true,
			Methods = VerificationMethod.KeyManagementSystem,
			VerifiedAt = Fixed.AddHours(1),
		},
		LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
		RetainUntil = Fixed.AddYears(7),
	};
}
