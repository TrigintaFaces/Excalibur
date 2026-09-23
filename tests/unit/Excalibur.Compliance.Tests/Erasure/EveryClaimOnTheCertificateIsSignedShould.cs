// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections;
using System.Reflection;

using Excalibur.Compliance.Erasure;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// Binds the rule that every claim on a certificate is covered by its signature — leaf by leaf.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists beside the arm that varies two claims.</b>
/// <c>ErasureCertificateSignatureShould.Distinguish_two_certificates_that_disagree_about_what_was_erased</c>
/// varies the verification summary and the counts. It is the right RED for the original defect, and it is a
/// PROXY for the requirement: a repair that enumerated a handful of fields into the signed input — and
/// happened to include those two — would turn it green while the legal basis, the exemptions, the method,
/// the retention date and the version stayed alterable. That is the exact failure the design forbids, and
/// the coarser arm would certify it as fixed.
/// </para>
/// <para>
/// <b>Why LEAVES and not properties.</b> Three of the payload's members are records with claims of their
/// own. An arm that swapped a whole <see cref="ErasureSummary"/> for a different one would prove only that
/// <i>something</i> about the summary reaches the signature; if the canonical form covered four of its five
/// fields, that arm stays green while the fifth is free. The claims a regulator actually reads — how much
/// was destroyed, whether anything was verified, which exemptions were claimed and on what basis — live in
/// those nested records.
/// </para>
/// <para>
/// <b>What binds the hand-written table to the type.</b> The mutators below are written out one per leaf,
/// and <see cref="Cover_every_leaf_the_payload_declares"/> asserts their key set equals the leaf set
/// reflected off the types. So a claim added to the payload, or to any record it nests, reddens here until
/// somebody writes a mutator for it. There is no list to remember to update, because forgetting is what
/// fails the arm.
/// </para>
/// <para>
/// <b>What this proves, exactly.</b> That varying any single claim changes the canonical bytes the
/// signature is computed over. It binds the canonicalizer, not the MAC: that different bytes give different
/// tags is HMAC-SHA256's property and is not re-proved here.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EveryClaimOnTheCertificateIsSignedShould
{
	private static readonly DateTimeOffset Fixed = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

	// One mutator per leaf, keyed "DeclaringType.Property". Each changes exactly that leaf and nothing
	// else. The values are arbitrary -- only "different from the fixture" matters.
	private static readonly Dictionary<string, Func<ErasureCertificatePayload, ErasureCertificatePayload>> Mutators =
		new(StringComparer.Ordinal)
		{
			// ErasureCertificatePayload
			["ErasureCertificatePayload.CertificateId"] = p => p with { CertificateId = Guid.NewGuid() },
			["ErasureCertificatePayload.RequestId"] = p => p with { RequestId = Guid.NewGuid() },
			["ErasureCertificatePayload.DataSubjectReference"] = p => p with { DataSubjectReference = "a-different-subject-hash" },
			["ErasureCertificatePayload.RequestReceivedAt"] = p => p with { RequestReceivedAt = p.RequestReceivedAt.AddSeconds(1) },
			["ErasureCertificatePayload.CompletedAt"] = p => p with { CompletedAt = p.CompletedAt.AddSeconds(1) },
			["ErasureCertificatePayload.Method"] = p => p with { Method = ErasureMethod.SecureOverwrite },
			["ErasureCertificatePayload.LegalBasis"] = p => p with { LegalBasis = ErasureLegalBasis.ConsentWithdrawal },
			["ErasureCertificatePayload.GeneratedAt"] = p => p with { GeneratedAt = p.GeneratedAt.AddSeconds(1) },
			["ErasureCertificatePayload.RetainUntil"] = p => p with { RetainUntil = p.RetainUntil.AddDays(1) },
			["ErasureCertificatePayload.Version"] = p => p with { Version = "99.0" },
			["ErasureCertificatePayload.Summary"] = p => p with { Summary = new ErasureSummary { KeysDeleted = 4242 } },
			["ErasureCertificatePayload.Verification"] = p => p with
			{
				Verification = new VerificationSummary { Verified = false, Methods = VerificationMethod.None, VerifiedAt = Fixed },
			},
			["ErasureCertificatePayload.Exceptions"] = p => p with { Exceptions = [Exception("a-different-category")] },

			// ErasureSummary — how much was destroyed
			["ErasureSummary.KeysDeleted"] = p => p with { Summary = p.Summary with { KeysDeleted = p.Summary.KeysDeleted + 1 } },
			["ErasureSummary.RecordsAffected"] = p => p with { Summary = p.Summary with { RecordsAffected = p.Summary.RecordsAffected + 1 } },
			["ErasureSummary.DataCategories"] = p => p with { Summary = p.Summary with { DataCategories = ["contact"] } },
			["ErasureSummary.TablesAffected"] = p => p with { Summary = p.Summary with { TablesAffected = ["Invoices"] } },
			["ErasureSummary.DataSizeBytes"] = p => p with { Summary = p.Summary with { DataSizeBytes = p.Summary.DataSizeBytes + 1 } },

			// VerificationSummary — whether anything was verified, and on what evidence
			["VerificationSummary.Verified"] = p => p with { Verification = p.Verification with { Verified = !p.Verification.Verified } },
			["VerificationSummary.Methods"] = p => p with { Verification = p.Verification with { Methods = VerificationMethod.AuditLog } },
			["VerificationSummary.VerifiedAt"] = p => p with { Verification = p.Verification with { VerifiedAt = p.Verification.VerifiedAt.AddSeconds(1) } },
			["VerificationSummary.ReportHash"] = p => p with { Verification = p.Verification with { ReportHash = "a-different-report-hash" } },
			["VerificationSummary.DeletedKeyIds"] = p => p with { Verification = p.Verification with { DeletedKeyIds = ["key-9"] } },
			["VerificationSummary.Warnings"] = p => p with { Verification = p.Verification with { Warnings = ["a warning nobody signed"] } },

			// ErasureException — the Article 17(3) record of what was lawfully RETAINED
			["ErasureException.Basis"] = p => p with { Exceptions = [Exception() with { Basis = LegalHoldBasis.ArchivingResearchStatistics }] },
			["ErasureException.DataCategory"] = p => p with { Exceptions = [Exception() with { DataCategory = "tax-records" }] },
			["ErasureException.Reason"] = p => p with { Exceptions = [Exception() with { Reason = "a different reason entirely" }] },
			["ErasureException.RetentionPeriod"] = p => p with { Exceptions = [Exception() with { RetentionPeriod = TimeSpan.FromDays(3650) }] },
			["ErasureException.HoldId"] = p => p with { Exceptions = [Exception() with { HoldId = Guid.NewGuid() }] },
		};

	/// <summary>
	/// CENSUS. Every leaf the types declare has a mutator, and every mutator names a leaf that exists.
	/// </summary>
	[Fact]
	public void Cover_every_leaf_the_payload_declares()
	{
		var declared = LeavesOf(typeof(ErasureCertificatePayload)).ToHashSet(StringComparer.Ordinal);

		string.Join(", ", declared.Except(Mutators.Keys).OrderBy(n => n, StringComparer.Ordinal))
			.ShouldBeEmpty(
				"these claims are carried on an erasure certificate and nothing here proves the signature "
				+ "covers them. Add a mutator above that changes exactly that leaf. Do not delete the name "
				+ "from the census -- the census is read off the type, which is the point.");

		string.Join(", ", Mutators.Keys.Except(declared).OrderBy(n => n, StringComparer.Ordinal))
			.ShouldBeEmpty("these mutators name leaves the payload no longer declares.");
	}

	/// <summary>
	/// SAFETY. Changing any single claim changes the bytes the signature is computed over.
	/// </summary>
	[Fact]
	public void Change_the_canonical_bytes_when_any_single_leaf_changes()
	{
		var baseline = ErasureCertificateCanonicalizer.ToCanonicalJson(Payload());

		var uncovered = Mutators
			.Where(m => string.Equals(
				ErasureCertificateCanonicalizer.ToCanonicalJson(m.Value(Payload())),
				baseline,
				StringComparison.Ordinal))
			.Select(m => m.Key)
			.OrderBy(n => n, StringComparer.Ordinal);

		string.Join(", ", uncovered).ShouldBeEmpty(
			"changing these claims left the signed bytes identical, so a certificate carrying either value "
			+ "authenticates equally. Whoever holds the document can set them to anything.");
	}

	/// <summary>
	/// LIVENESS. The comparison above can report "same", so an all-different result is not vacuous.
	/// </summary>
	[Fact]
	public void Produce_identical_canonical_bytes_for_an_unchanged_payload() =>
		ErasureCertificateCanonicalizer.ToCanonicalJson(Payload())
			.ShouldBe(
				ErasureCertificateCanonicalizer.ToCanonicalJson(Payload()),
				"two equal payloads must canonicalize identically, or the arm above passes because the "
				+ "canonical form is non-deterministic rather than because every claim is covered.");

	/// <summary>
	/// SAFETY. "No exemptions were claimed" and "exemptions were considered and none applied" are different
	/// statements to a regulator, so they must not be the same document.
	/// </summary>
	[Fact]
	public void Distinguish_an_absent_exemption_list_from_an_empty_one()
	{
		var none = Payload() with { Exceptions = null! };
		var empty = Payload() with { Exceptions = [] };

		ErasureCertificateCanonicalizer.ToCanonicalJson(none)
			.ShouldNotBe(ErasureCertificateCanonicalizer.ToCanonicalJson(empty));
	}

	/// <summary>
	/// SAFETY. Two payloads whose claims differ only in where a boundary falls must not collide.
	/// </summary>
	/// <remarks>
	/// The scheme this replaced joined its inputs with <c>|</c>, so any claim able to contain that character
	/// let one document be read as another. The vary-each-leaf arm above cannot catch it: each leaf varies
	/// legitimately and the collision happens across the boundary between two of them. This arm holds the
	/// concatenated text constant and moves only the boundary.
	/// </remarks>
	[Fact]
	public void Distinguish_two_payloads_whose_claims_differ_only_in_where_a_boundary_falls()
	{
		var left = Payload() with { Summary = Summary() with { DataCategories = ["billing|contact", "profile"] } };
		var right = Payload() with { Summary = Summary() with { DataCategories = ["billing", "contact|profile"] } };

		ErasureCertificateCanonicalizer.ToCanonicalJson(left)
			.ShouldNotBe(
				ErasureCertificateCanonicalizer.ToCanonicalJson(right),
				"these two certificates claim different sets of erased data categories, and a canonical form "
				+ "that concatenates without marking boundaries would render them as one document.");
	}

	// Walks the payload's claim graph: a property whose type is one of the certificate's own records is
	// descended into rather than counted, and so is the element type of a list of them. Everything else is
	// a leaf. Reflected rather than listed, so a new field cannot enter the type unnoticed.
	private static IEnumerable<string> LeavesOf(Type type)
	{
		foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
		{
			if (string.Equals(property.Name, "EqualityContract", StringComparison.Ordinal))
			{
				continue;
			}

			var nested = ClaimRecordIn(property.PropertyType);

			if (nested is null)
			{
				yield return $"{type.Name}.{property.Name}";

				continue;
			}

			// The composite itself is a claim too: replacing a whole summary must change the bytes.
			yield return $"{type.Name}.{property.Name}";

			foreach (var leaf in LeavesOf(nested))
			{
				yield return leaf;
			}
		}
	}

	// A claim record is a record declared alongside the certificate. A list of them descends to its element
	// type; a string is a leaf despite being enumerable.
	private static Type? ClaimRecordIn(Type type)
	{
		if (type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType)
		{
			type = type.GetGenericArguments()[0];
		}

		return type.IsClass
			&& type != typeof(string)
			&& type.Assembly == typeof(ErasureCertificatePayload).Assembly
			&& type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance) is not null
				? type
				: null;
	}

	private static ErasureException Exception(string dataCategory = "billing-records") =>
		new()
		{
			Basis = LegalHoldBasis.LegalObligation,
			DataCategory = dataCategory,
			Reason = "retained under a statutory accounting obligation",
		};

	private static ErasureSummary Summary() =>
		new()
		{
			KeysDeleted = 5,
			RecordsAffected = 9,
			DataCategories = ["personal", "contact"],
			TablesAffected = ["Users", "Contacts"],
			DataSizeBytes = 10240,
		};

	private static ErasureCertificatePayload Payload() =>
		new()
		{
			CertificateId = new Guid("22222222-2222-2222-2222-222222222222"),
			RequestId = new Guid("11111111-1111-1111-1111-111111111111"),
			DataSubjectReference = "subject-hash",
			RequestReceivedAt = Fixed.AddDays(-1),
			CompletedAt = Fixed,
			Method = ErasureMethod.CryptographicErasure,
			Summary = Summary(),
			Verification = new VerificationSummary
			{
				Verified = true,
				Methods = VerificationMethod.KeyManagementSystem,
				VerifiedAt = Fixed,
				ReportHash = "report-hash",
				DeletedKeyIds = ["key-1", "key-2"],
				Warnings = ["one warning"],
			},
			LegalBasis = ErasureLegalBasis.DataNoLongerNecessary,
			Exceptions = [Exception()],
			GeneratedAt = Fixed,
			RetainUntil = Fixed.AddYears(7),
		};
}
