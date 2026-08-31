// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.CosmosDb;

namespace Excalibur.Data.CosmosDb.Tests.CosmosDb;

/// <summary>
/// Binds the requirement that the Cosmos inbox document id is INJECTIVE: two different
/// (tenant, message, handler) tuples must never produce the same id.
/// </summary>
/// <remarks>
/// <para>
/// The id is the dedup key. Joining the three terms with a bare ':' is not injective, because neither
/// the tenant term nor the message id is validated against any charset -- both are caller data. Tenant
/// "a:b" with message "c" and tenant "a" with message "b:c" both rendered "a:b:c:H" and became ONE
/// document. A dedup collision does not throw: the second message reads as already-processed and is
/// dropped, silently, across a tenant boundary.
/// </para>
/// <para>
/// The final arm is the MIGRATION arm and it is the reason this encoding was chosen over
/// length-prefixing. These ids are PERSISTED. An encoding that moved every stored id would orphan every
/// in-flight dedup record on upgrade, and previously-processed messages would read as new and be
/// re-delivered. Percent-escaping is deliberately the IDENTITY on any term containing neither '%' nor
/// ':', so the only ids whose bytes change are the ones that were ambiguous before -- which had no
/// single correct owner anyway.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.CosmosDb")]
public sealed class CosmosDbInboxDocumentIdInjectivityShould
{
	private const string Handler = "OrderPlacedHandler";

	/// <summary>
	/// SAFETY: the colliding pair. Two DIFFERENT tuples that the bare ':' join mapped onto one id.
	/// </summary>
	[Fact]
	public void DistinguishTuplesThatCollidedUnderTheBareColonJoin()
	{
		// Under the bare join both of these rendered "a:b:c:OrderPlacedHandler".
		var shiftedIntoTenant = CosmosDbInboxDocument.CreateId("c", Handler, "a:b");
		var shiftedIntoMessage = CosmosDbInboxDocument.CreateId("b:c", Handler, "a");

		shiftedIntoTenant.ShouldNotBe(
			shiftedIntoMessage,
			"tenant 'a:b' message 'c' and tenant 'a' message 'b:c' are DIFFERENT messages belonging to "
			+ "DIFFERENT tenants. If they share a document id, whichever arrives second is refused as an "
			+ "already-seen duplicate and is never processed and never retried -- silent loss, across a "
			+ "tenant boundary.");
	}

	/// <summary>
	/// SAFETY: the escape character itself must not open a second collision.
	/// </summary>
	/// <remarks>
	/// This is the arm that fails if '%' is not escaped FIRST. Escaping only ':' would map the distinct
	/// terms "a:b" and "a%3Ab" onto one id -- a collision introduced by the escaping itself, which is the
	/// classic way a hand-rolled escape stops being reversible.
	/// </remarks>
	[Fact]
	public void DistinguishATermFromItsOwnEscapedSpelling()
	{
		var literalColon = CosmosDbInboxDocument.CreateId("m", Handler, "a:b");
		var literalEscapeSequence = CosmosDbInboxDocument.CreateId("m", Handler, "a%3Ab");

		literalColon.ShouldNotBe(
			literalEscapeSequence,
			"'a:b' and the literal text 'a%3Ab' are different tenant ids. If the escaping is not itself "
			+ "escaped they collapse onto one id, and the escape has introduced the very collision it was "
			+ "added to remove.");
	}

	/// <summary>
	/// LIVENESS: an ordinary tuple still produces a stable id, and the SAME tuple still matches itself.
	/// </summary>
	/// <remarks>
	/// Without this arm the safety arms above are satisfied by an id builder that returns a fresh GUID
	/// every call -- perfectly injective, and it deduplicates nothing at all, turning the inbox from
	/// at-most-once into at-least-once for every message.
	/// </remarks>
	[Fact]
	public void StillProduceTheSameIdForTheSameTuple()
	{
		var first = CosmosDbInboxDocument.CreateId("order-42", Handler, "tenant-7");
		var second = CosmosDbInboxDocument.CreateId("order-42", Handler, "tenant-7");

		second.ShouldBe(
			first,
			"the same message for the same handler in the same tenant must map to the same document, or "
			+ "nothing is ever recognised as a duplicate");
	}

	/// <summary>
	/// MIGRATION: for separator-free terms the id must be BYTE-IDENTICAL to the old bare join.
	/// </summary>
	/// <remarks>
	/// The load-bearing arm for upgrade safety. These ids are persisted, so if the encoding changed the
	/// bytes for ordinary inputs, every already-stored dedup record would become unreachable on upgrade
	/// and every message processed inside the retention window would be re-delivered. The expected values
	/// here are written as the literal OLD format on purpose -- they are the pre-change bytes, not a
	/// re-derivation of the new code.
	/// </remarks>
	[Theory]
	[InlineData("order-42", "OrderPlacedHandler", "tenant-7", "tenant-7:order-42:OrderPlacedHandler")]
	[InlineData("8f14e45f", "Ns.Sub.SomeHandler", "acme", "acme:8f14e45f:Ns.Sub.SomeHandler")]
	[InlineData("msg_1", "H", "__untenanted__", "__untenanted__:msg_1:H")]
	public void LeaveSeparatorFreeIdsByteIdenticalToTheOldFormat(
		string messageId, string handlerType, string tenantId, string expectedLegacyId)
	{
		CosmosDbInboxDocument.CreateId(messageId, handlerType, tenantId).ShouldBe(
			expectedLegacyId,
			"the id is the PERSISTED dedup key. An encoding that changed these bytes would orphan every "
			+ "stored dedup record on upgrade, and messages already processed would read as new and be "
			+ "re-delivered. Escaping must be the identity on terms containing neither '%' nor ':'.");
	}
}
