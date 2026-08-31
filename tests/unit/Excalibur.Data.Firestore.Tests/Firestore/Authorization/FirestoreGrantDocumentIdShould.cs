// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore.Authorization;

namespace Excalibur.Data.Tests.Firestore.Authorization;

/// <summary>
/// Binds the document-id composition of <see cref="FirestoreGrantDocument"/> and
/// <see cref="FirestoreActivityGroupDocument"/>.
/// </summary>
/// <remarks>
/// <para>
/// The id addresses the record: a grant is written, read and revoked by composing it from
/// <c>(tenant, user, grantType, qualifier)</c>. So the composition has to be injective. If two distinct
/// tuples render one id, the grant written second overwrites a different grant written first, and a read
/// for either returns whichever survived — silently, on the success path, to records that are access
/// grants.
/// </para>
/// <para>
/// Both directions are asserted, because either alone is satisfied by a broken composition. Safety alone
/// — "distinct tuples differ" — is satisfied perfectly by an id that is random per call, which would make
/// every grant unreadable. Liveness alone — "the same tuple repeats" — is satisfied by the ambiguous join
/// this replaces.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "A3")]
public sealed class FirestoreGrantDocumentIdShould
{
	// Each row is two DISTINCT tuples that the previous separator-joined composition rendered identically.
	public static TheoryData<string, string, string, string, string, string, string, string> PreviouslyCollidingTuples => new()
	{
		// Two tenants, ordinary slugs: "acme_corp_bob_read_docs". A cross-tenant overwrite — tenant
		// "acme_corp" replaces tenant "acme".
		{ "acme", "corp_bob", "read", "docs", "acme_corp", "bob", "read", "docs" },

		// One tenant, the boundary between grant type and qualifier: "t_u_a_b_c".
		{ "t", "u", "a_b", "c", "t", "u", "a", "b_c" },

		// A qualifier that swallows the boundary: "t_u_read_orders_2024".
		{ "t", "u", "read", "orders_2024", "t", "u", "read_orders", "2024" },

		// The reserved untenanted partition against a real tenant named "__untenanted". The sentinel is
		// chosen so it cannot name a real tenant, but an ambiguous join defeats that guarantee — the two
		// partitions share a document anyway.
		{ "__untenanted__", "u", "read", "x", "__untenanted", "__u", "read", "x" },
	};

	// Terms containing the separator and the escape character, which is where an escaping scheme applied
	// in the wrong order stops being reversible.
	public static TheoryData<string, string, string, string> AdversarialTuples => new()
	{
		{ "a_b", "c", "read", "x" },
		{ "a", "b_c", "read", "x" },
		{ "a%5Fb", "c", "read", "x" },
		{ "a_b", "c%5Fd", "read%25", "x" },
		{ "t", "u", "read", "a/b" },
		{ "__untenanted__", "u", "read", "x__" },
		{ "", "", "", "" },
	};

	/// <summary>
	/// SAFETY. Tuples that the previous composition merged onto one id now render distinct ids, so the
	/// second grant is a separate document and does not overwrite the first.
	/// </summary>
	[Theory]
	[MemberData(nameof(PreviouslyCollidingTuples))]
	public void GiveDistinctIdsToTuplesTheOldJoinMerged(
		string leftTenant,
		string leftUser,
		string leftType,
		string leftQualifier,
		string rightTenant,
		string rightUser,
		string rightType,
		string rightQualifier)
	{
		// The composition being replaced, stated here so the collision is visible rather than asserted.
		var oldLeft = $"{leftTenant}_{leftUser}_{leftType}_{leftQualifier}";
		var oldRight = $"{rightTenant}_{rightUser}_{rightType}_{rightQualifier}";
		oldLeft.ShouldBe(oldRight, "the fixture is only meaningful if these two tuples used to collide");

		FirestoreGrantDocument.CreateDocumentId(leftTenant, leftUser, leftType, leftQualifier)
			.ShouldNotBe(FirestoreGrantDocument.CreateDocumentId(rightTenant, rightUser, rightType, rightQualifier));

		FirestoreActivityGroupDocument.CreateDocumentId(leftTenant, leftUser, leftType, leftQualifier)
			.ShouldNotBe(FirestoreActivityGroupDocument.CreateDocumentId(rightTenant, rightUser, rightType, rightQualifier));
	}

	/// <summary>
	/// SAFETY, stated as the mechanism rather than as a list of cases: the id decodes back to the exact
	/// terms it was built from. A composition that is decodable cannot be ambiguous, so this covers every
	/// input rather than the rows the fixture happens to name.
	/// </summary>
	[Theory]
	[MemberData(nameof(AdversarialTuples))]
	public void ComposeAnIdThatDecodesBackToItsTerms(string tenantId, string userId, string grantType, string qualifier)
	{
		var id = FirestoreGrantDocument.CreateDocumentId(tenantId, userId, grantType, qualifier);

		var segments = id.Split('_');

		segments.Length.ShouldBe(5, "the separator must not survive inside an encoded term");
		segments[0].ShouldBe(FirestoreGrantDocument.IdPrefix);
		Unescape(segments[1]).ShouldBe(tenantId);
		Unescape(segments[2]).ShouldBe(userId);
		Unescape(segments[3]).ShouldBe(grantType);
		Unescape(segments[4]).ShouldBe(qualifier);
	}

	/// <summary>
	/// LIVENESS. The same tuple renders the same id, so a repeated write still lands on the existing
	/// document and revocation still finds it. Without this arm, an id that varied per call would satisfy
	/// every safety assertion above while making grants unreadable.
	/// </summary>
	[Theory]
	[MemberData(nameof(AdversarialTuples))]
	public void GiveOneIdToOneTuple(string tenantId, string userId, string grantType, string qualifier)
	{
		var first = FirestoreGrantDocument.CreateDocumentId(tenantId, userId, grantType, qualifier);
		var second = FirestoreGrantDocument.CreateDocumentId(tenantId, userId, grantType, qualifier);

		second.ShouldBe(first);
	}

	/// <summary>
	/// LIVENESS. An everyday tuple is carried through unchanged: the encoding touches only characters that
	/// would make the id ambiguous. This pins the stored key shape — it is persisted data, so a change here
	/// is a migration for every consumer — and it fails for a composition that digests the terms.
	/// </summary>
	[Fact]
	public void LeaveAnOrdinaryTupleLegible()
	{
		FirestoreGrantDocument.CreateDocumentId("tenant-1", "user-42", "read", "orders")
			.ShouldBe("grant_tenant-1_user-42_read_orders");

		FirestoreActivityGroupDocument.CreateDocumentId("tenant-1", "user-42", "read", "orders")
			.ShouldBe("ag_tenant-1_user-42_read_orders");
	}

	/// <summary>
	/// Firestore rejects a write whose document id matches <c>__.*__</c>. The reserved untenanted tenant
	/// term is <c>__untenanted__</c>, so without a leading constant every id in a deployment that does not
	/// use multi-tenancy starts with <c>__</c>, and a qualifier ending in <c>__</c> completes the pattern.
	/// </summary>
	[Theory]
	[InlineData("__untenanted__", "u", "read", "x__")]
	[InlineData("__untenanted__", "u__", "__read__", "__x__")]
	[InlineData("__t__", "__u__", "__read__", "__x__")]
	public void NeverComposeAReservedDocumentId(string tenantId, string userId, string grantType, string qualifier)
	{
		string[] ids =
		[
			FirestoreGrantDocument.CreateDocumentId(tenantId, userId, grantType, qualifier),
			FirestoreActivityGroupDocument.CreateDocumentId(tenantId, userId, grantType, qualifier),
		];

		foreach (var id in ids)
		{
			(id.StartsWith("__", StringComparison.Ordinal) && id.EndsWith("__", StringComparison.Ordinal))
				.ShouldBeFalse($"Firestore rejects the reserved id shape, and '{id}' matches it");
		}
	}

	/// <summary>
	/// The two document kinds live in different collections, but they took identical terms and produced
	/// identical ids. Distinct leading constants keep them told apart by inspection.
	/// </summary>
	[Fact]
	public void DistinguishGrantsFromActivityGroups() =>
		FirestoreGrantDocument.CreateDocumentId("t", "u", "read", "x")
			.ShouldNotBe(FirestoreActivityGroupDocument.CreateDocumentId("t", "u", "read", "x"));

	// The inverse of the escaper, applied in the reverse order for the same reason the escaper applies its
	// steps in the order it does.
	private static string Unescape(string value) =>
		value.Replace("%5F", "_", StringComparison.Ordinal)
			.Replace("%2F", "/", StringComparison.Ordinal)
			.Replace("%25", "%", StringComparison.Ordinal);
}
