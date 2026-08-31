// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.MongoDB;

namespace Excalibur.Data.MongoDB.Tests.MongoDB;

/// <summary>
/// Binds the requirement that the Mongo inbox <c>_id</c> is INJECTIVE: two different
/// (tenant, message, handler) tuples must never produce the same document id.
/// </summary>
/// <remarks>
/// <para>
/// The <c>_id</c> is the dedup key and carries a unique index. Joining the three terms with a bare ':' is
/// not injective, because neither the tenant term nor the message id is validated against any charset --
/// both are caller data. Tenant "a:b" with message "c" and tenant "a" with message "b:c" both rendered
/// "a:b:c:H" and became ONE document. A dedup collision does not throw: the second message reads as
/// already-processed and is dropped, silently, across a tenant boundary.
/// </para>
/// <para>
/// The final arm is the MIGRATION arm. These ids are PERSISTED. An encoding that moved every stored id
/// would orphan every in-flight dedup record on upgrade, and previously-processed messages would read as
/// new and be re-delivered. Percent-escaping is deliberately the IDENTITY on any term containing neither
/// '%' nor ':', so the only ids whose bytes change are the ones that were ambiguous before -- which had
/// no single correct owner anyway.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Inbox")]
public sealed class MongoDbInboxDocumentIdInjectivityShould
{
	private const string Handler = "OrderPlacedHandler";

	/// <summary>
	/// SAFETY: the colliding pair. Two DIFFERENT tuples that the bare ':' join mapped onto one id.
	/// </summary>
	[Fact]
	public void DistinguishTuplesThatCollidedUnderTheBareColonJoin()
	{
		var shiftedIntoTenant = MongoDbInboxDocument.CreateId("c", Handler, "a:b");
		var shiftedIntoMessage = MongoDbInboxDocument.CreateId("b:c", Handler, "a");

		shiftedIntoTenant.ShouldNotBe(
			shiftedIntoMessage,
			"tenant 'a:b' message 'c' and tenant 'a' message 'b:c' are DIFFERENT messages belonging to "
			+ "DIFFERENT tenants. If they share an _id the unique index treats the second as a duplicate, "
			+ "so it is never processed and never retried -- silent loss, across a tenant boundary.");
	}

	/// <summary>
	/// SAFETY: the escape character itself must not open a second collision.
	/// </summary>
	/// <remarks>
	/// The arm that fails if '%' is not escaped FIRST. Escaping only ':' would map the distinct terms
	/// "a:b" and "a%3Ab" onto one id -- a collision introduced by the escaping itself.
	/// </remarks>
	[Fact]
	public void DistinguishATermFromItsOwnEscapedSpelling()
	{
		MongoDbInboxDocument.CreateId("m", Handler, "a:b").ShouldNotBe(
			MongoDbInboxDocument.CreateId("m", Handler, "a%3Ab"),
			"'a:b' and the literal text 'a%3Ab' are different tenant ids. If the escaping is not itself "
			+ "escaped they collapse onto one id, and the escape has introduced the very collision it was "
			+ "added to remove.");
	}

	/// <summary>
	/// LIVENESS: the same tuple must still produce the same id, or nothing is ever deduplicated.
	/// </summary>
	/// <remarks>
	/// Without this arm the safety arms are satisfied by an id builder returning a fresh value every call
	/// -- perfectly injective, deduplicating nothing, turning the inbox into at-least-once.
	/// </remarks>
	[Fact]
	public void StillProduceTheSameIdForTheSameTuple()
	{
		MongoDbInboxDocument.CreateId("order-42", Handler, "tenant-7").ShouldBe(
			MongoDbInboxDocument.CreateId("order-42", Handler, "tenant-7"),
			"the same message for the same handler in the same tenant must map to the same document, or "
			+ "no duplicate is ever recognised");
	}

	/// <summary>
	/// OVER-CORRECTION GUARD: a colon-bearing id is legal caller data and must still key.
	/// </summary>
	/// <remarks>
	/// A "fix" that rejected or stripped colons would pass the safety arms while breaking dedup for every
	/// consumer using a colon-bearing identifier -- a URN, for instance.
	/// </remarks>
	[Fact]
	public void StillKeyAMessageWhoseTermsContainAColon()
	{
		var id = MongoDbInboxDocument.CreateId("urn:uuid:9f8c", Handler, "a:b");

		id.ShouldNotBeNullOrWhiteSpace(
			"a colon is legal caller data; making the key injective must not cost dedup for the messages "
			+ "whose ids contain one");
		MongoDbInboxDocument.CreateId("urn:uuid:9f8c", Handler, "a:b").ShouldBe(
			id, "and it must still be stable across calls");
	}

	/// <summary>
	/// MIGRATION: for separator-free terms the id must be BYTE-IDENTICAL to the old bare join.
	/// </summary>
	/// <remarks>
	/// The load-bearing arm for upgrade safety. Expected values are written as the literal OLD format on
	/// purpose -- they are the pre-change bytes, not a re-derivation of the new code. The two-argument
	/// (tenant-less) form is included because this document's contract promises it stays byte-identical to
	/// the un-scoped form.
	/// </remarks>
	[Theory]
	[InlineData("order-42", "OrderPlacedHandler", "tenant-7", "tenant-7:order-42:OrderPlacedHandler")]
	[InlineData("8f14e45f", "Ns.Sub.SomeHandler", "acme", "acme:8f14e45f:Ns.Sub.SomeHandler")]
	[InlineData("msg_1", "H", "__untenanted__", "__untenanted__:msg_1:H")]
	public void LeaveSeparatorFreeIdsByteIdenticalToTheOldFormat(
		string messageId, string handlerType, string tenantId, string expectedLegacyId)
	{
		MongoDbInboxDocument.CreateId(messageId, handlerType, tenantId).ShouldBe(
			expectedLegacyId,
			"the _id is the PERSISTED dedup key. An encoding that changed these bytes would orphan every "
			+ "stored dedup record on upgrade, and messages already processed would read as new and be "
			+ "re-delivered. Escaping must be the identity on terms containing neither '%' nor ':'.");
	}

	/// <summary>
	/// MIGRATION: the tenant-less form stays byte-identical too.
	/// </summary>
	[Fact]
	public void LeaveTheTenantLessFormByteIdenticalToTheOldFormat()
	{
		MongoDbInboxDocument.CreateId("order-42", Handler).ShouldBe(
			"order-42:OrderPlacedHandler",
			"the document contract states the un-scoped form is byte-identical when no tenant is supplied");
	}
}
