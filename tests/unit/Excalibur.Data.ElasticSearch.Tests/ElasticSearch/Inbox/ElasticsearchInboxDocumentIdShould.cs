// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Inbox.ElasticSearch;

namespace Excalibur.Data.Tests.ElasticSearch.Inbox;

/// <summary>
/// Binds the document-id composition of <see cref="ElasticsearchInboxStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// The id is the deduplication key: the store decides "have I already seen this?" by whether a document
/// with that id exists. So the composition has to be injective. If two distinct
/// <c>(tenant, message, handler)</c> triples can render one id, the second message is read as an
/// already-seen duplicate and dropped — no error, no retry, nothing to investigate, on the success path.
/// </para>
/// <para>
/// Both directions are asserted, because either one alone is satisfied by a broken store. Safety alone —
/// "distinct triples differ" — is satisfied perfectly by an id that is random per call, which would
/// disable deduplication entirely and deliver every message forever. Liveness alone — "the same triple
/// repeats" — is satisfied by the ambiguous join this replaces.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Inbox)]
public sealed class ElasticsearchInboxDocumentIdShould
{
	// Each pair is two DISTINCT triples that the previous separator-joined composition rendered
	// identically. The comment on each row is the single id both sides used to produce.
	public static TheoryData<string, string, string, string, string, string> PreviouslyCollidingTriples => new()
	{
		// Two tenants, ordinary slugs: "acme_corp_42_H". A cross-tenant drop -- tenant acme_corp's
		// message is discarded because tenant acme already has one that renders the same.
		{ "acme", "corp_42", "H", "acme_corp", "42", "H" },

		// One tenant, the boundary between message id and handler: "t_a_b_c".
		{ "t", "a_b", "c", "t", "a", "b_c" },

		// The reserved untenanted partition against a real tenant named "__untenanted":
		// "__untenanted___a_H". The sentinel is chosen so it cannot name a real tenant, but an
		// ambiguous join defeats that guarantee -- the two partitions share a document anyway.
		{ "__untenanted__", "a", "H", "__untenanted", "__a", "H" },
	};

	// Terms containing the separator and the escape character, which is where an escaping scheme that is
	// applied in the wrong order stops being reversible.
	public static TheoryData<string, string, string> AdversarialTriples => new()
	{
		{ "a:b", "c", "H" },
		{ "a", "b:c", "H" },
		{ "a%3Ab", "c", "H" },
		{ "a:b", "c%3Ad", "H%25" },
		{ "a_b", "c_d", "e_f" },
		{ "t", "m", "Some.Name.Space.Handler`1[[System.String]]" },
	};

	/// <summary>
	/// SAFETY. Triples that the previous composition merged onto one id now render distinct ids, so the
	/// second entry is a separate document and is not mistaken for a duplicate of the first.
	/// </summary>
	[Theory]
	[MemberData(nameof(PreviouslyCollidingTriples))]
	public void GiveDistinctIdsToTriplesTheOldJoinMerged(
		string leftTenant, string leftMessage, string leftHandler,
		string rightTenant, string rightMessage, string rightHandler)
	{
		// The composition being replaced, stated here so the collision is visible rather than asserted.
		var oldLeft = $"{leftTenant}_{leftMessage}_{leftHandler}";
		var oldRight = $"{rightTenant}_{rightMessage}_{rightHandler}";
		oldLeft.ShouldBe(oldRight, "the fixture is only meaningful if these two triples used to collide");

		var left = ElasticsearchInboxStore.ComposeDocumentId(leftTenant, leftMessage, leftHandler);
		var right = ElasticsearchInboxStore.ComposeDocumentId(rightTenant, rightMessage, rightHandler);

		left.ShouldNotBe(right);
	}

	/// <summary>
	/// SAFETY, stated as the mechanism rather than as a list of cases: the id decodes back to the exact
	/// terms it was built from. A composition that is decodable cannot be ambiguous, so this covers every
	/// input rather than the three the fixture happens to name.
	/// </summary>
	[Theory]
	[MemberData(nameof(AdversarialTriples))]
	public void ComposeAnIdThatDecodesBackToItsTerms(string tenantId, string messageId, string handlerType)
	{
		var id = ElasticsearchInboxStore.ComposeDocumentId(tenantId, messageId, handlerType);

		var segments = id.Split(':');

		segments.Length.ShouldBe(3, "the separator must not survive inside an encoded term");
		Uri.UnescapeDataString(segments[0]).ShouldBe(tenantId);
		Uri.UnescapeDataString(segments[1]).ShouldBe(messageId);
		Uri.UnescapeDataString(segments[2]).ShouldBe(handlerType);
	}

	/// <summary>
	/// LIVENESS. The same triple renders the same id, so a genuine redelivery still lands on the existing
	/// document and is still detected as a duplicate. Without this arm, an id that varied per call would
	/// satisfy every safety assertion above while deduplicating nothing.
	/// </summary>
	[Theory]
	[MemberData(nameof(AdversarialTriples))]
	public void GiveOneIdToOneTriple(string tenantId, string messageId, string handlerType)
	{
		var first = ElasticsearchInboxStore.ComposeDocumentId(tenantId, messageId, handlerType);
		var second = ElasticsearchInboxStore.ComposeDocumentId(tenantId, messageId, handlerType);

		second.ShouldBe(first);
	}

	/// <summary>
	/// LIVENESS. An everyday triple is carried through unchanged: the encoding touches only characters
	/// that would make the id ambiguous. This pins the stored key shape -- it is persisted data, so a
	/// change here is a migration for every consumer -- and it fails for a composition that digests or
	/// otherwise obscures the terms.
	/// </summary>
	[Fact]
	public void LeaveAnOrdinaryTripleLegible()
	{
		var id = ElasticsearchInboxStore.ComposeDocumentId("tenant-1", "9f8c3d7e-1a2b", "MyApp.Handlers.OrderHandler");

		id.ShouldBe("tenant-1:9f8c3d7e-1a2b:MyApp.Handlers.OrderHandler");
	}


	/// <summary>
	/// A missing term cannot be part of a key that has to identify one entry, so it is rejected rather
	/// than folded into an empty segment that another triple could also produce.
	/// </summary>
	[Theory]
	[InlineData(null, "m", "H")]
	[InlineData("t", null, "H")]
	[InlineData("t", "m", null)]
	public void RejectAnAbsentTerm(string? tenantId, string? messageId, string? handlerType)
		=> Should.Throw<ArgumentNullException>(
			() => ElasticsearchInboxStore.ComposeDocumentId(tenantId!, messageId!, handlerType!));

	/// <summary>
	/// A blank term is rejected for the same reason: it identifies nothing, and two triples differing
	/// only in which term is blank would render one key.
	/// </summary>
	[Theory]
	[InlineData("", "m", "H")]
	[InlineData("t", " ", "H")]
	[InlineData("t", "m", "	")]
	public void RejectABlankTerm(string tenantId, string messageId, string handlerType)
		=> Should.Throw<ArgumentException>(
			() => ElasticsearchInboxStore.ComposeDocumentId(tenantId, messageId, handlerType));

	/// <summary>
	/// SAFETY. <c>Uri.EscapeDataString</c> is not injective over all of <see cref="string"/>: it
	/// substitutes U+FFFD for an unpaired surrogate rather than rejecting or preserving it, so distinct
	/// values encode identically. Those inputs are refused rather than admitted, because admitting them
	/// would let two different messages share a deduplication key -- the same silent drop this
	/// composition exists to prevent, reappearing one layer down inside the encoder.
	/// </summary>
	// The surrogates are passed as code units and the string is built here, NOT as string InlineData:
	// xUnit serializes theory arguments, and an unpaired surrogate does not survive that round trip --
	// it arrives as several replacement characters, and the two single-surrogate rows collapse into one.
	// That is the same substitution this test is about, reaching the test from a different direction.
	[Theory]
	[InlineData(0xD800)]
	[InlineData(0xDBFF)]
	[InlineData(0xDC00)]
	[InlineData(0xDFFF)]
	public void RejectATermThatIsNotWellFormedText(int loneSurrogate)
	{
		var malformed = "a" + (char)loneSurrogate;

		// The fixture is only meaningful if the encoder really does merge these onto one value, so the
		// collision is demonstrated here rather than taken on faith, exactly as the id fixtures above do.
		Uri.EscapeDataString(malformed).ShouldBe(Uri.EscapeDataString("a\uFFFD"));

		Should.Throw<ArgumentException>(() => ElasticsearchInboxStore.ComposeDocumentId(malformed, "m", "H"));
		Should.Throw<ArgumentException>(() => ElasticsearchInboxStore.ComposeDocumentId("t", malformed, "H"));
		Should.Throw<ArgumentException>(() => ElasticsearchInboxStore.ComposeDocumentId("t", "m", malformed));
	}

	/// <summary>
	/// SAFETY. Elasticsearch refuses a document id longer than 512 bytes. Percent-encoding expands, so
	/// terms that would have fitted unencoded can exceed it -- a handler type name that is a nested closed
	/// generic measures roughly 1.24x its unencoded length. The id is rejected with the cause named, and
	/// never truncated: a truncated key is one that two different messages could share.
	/// </summary>
	[Fact]
	public void RejectAnIdThatExceedsTheStoreLimit()
	{
		var overLimit = new string('H', 509);

		var thrown = Should.Throw<ArgumentException>(
			() => ElasticsearchInboxStore.ComposeDocumentId("t", "m", overLimit));

		thrown.Message.ShouldContain("512");
	}

	/// <summary>
	/// LIVENESS. An id at exactly the limit is accepted. Without this arm, a guard that rejected
	/// everything would satisfy the safety assertion above while making the store unusable.
	/// </summary>
	[Fact]
	public void AcceptAnIdAtExactlyTheStoreLimit()
	{
		var atLimit = new string('H', 508);

		var id = ElasticsearchInboxStore.ComposeDocumentId("t", "m", atLimit);

		Encoding.UTF8.GetByteCount(id).ShouldBe(512);
	}
}
