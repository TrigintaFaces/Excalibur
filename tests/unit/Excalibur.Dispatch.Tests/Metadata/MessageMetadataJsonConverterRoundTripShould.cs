// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Claims;
using System.Text.Json;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Metadata;

namespace Excalibur.Dispatch.Tests.Metadata;

/// <summary>
/// Author≠impl regression lock for bead <c>az9u1e</c> (sprint 855): the
/// <see cref="MessageMetadataJsonConverter"/> MUST round-trip the typed <c>Attributes</c>,
/// <c>Items</c> and <c>Claims</c> surfaces through JSON. Closes the coverage gap the
/// <c>lh6wcw</c> keystone lock left open (it round-trips the seven facade groups + roles, but
/// never exercised Attributes/Items/Claims — the gap that let the matrix mark lh6wcw FULL).
/// </summary>
/// <remarks>
/// <para>
/// SA wire-contract ruling (REVIEW_ARCH 16887, FR-C2 / EC-6): <c>Attributes</c> + <c>Items</c>
/// were on the wire pre-split (public, no <c>[JsonIgnore]</c>); the converter's <c>Write</c>
/// silently drops them (emits only <c>headers</c>/<c>properties</c>), so a JSON round-trip loses
/// them — a regression against the keystone's own "wire-preserving" AC. <c>Claims</c> gets an
/// explicit lossless array contract (<c>{type,value,valueType?,issuer?}</c>), no silent drop.
/// </para>
/// <para>
/// Surface: the production <see cref="MessageMetadataBuilder"/> populates attributes/items into the
/// <c>Properties</c> bag (surfaced by the <see cref="MetadataCollectionExtensions.GetAttributes"/>
/// / <see cref="MetadataCollectionExtensions.GetItems"/> accessors) and claims into the
/// <see cref="MessageSecurity.Claims"/> group — these are the buildable, symmetric round-trip
/// surfaces this lock asserts. (Seam pinned with SA/Backend on the az9u1e thread.)
/// </para>
/// <para>
/// Non-vacuity (each populated fact RED on today's pre-fix converter):
/// <list type="bullet">
/// <item>Attributes/Items: <c>Write</c> emits them only as a stringified-dictionary inside
/// <c>properties</c>; <c>GetAttributes()</c>/<c>GetItems()</c> come back EMPTY after a round-trip.</item>
/// <item>Claims: <c>Write</c> emits no <c>claims</c> at all; <c>Security.Claims</c> comes back EMPTY.</item>
/// </list>
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class MessageMetadataJsonConverterRoundTripShould
{
	private static MessageMetadataBuilder BaseBuilder() =>
		(MessageMetadataBuilder)new MessageMetadataBuilder()
			.WithMessageId("msg-az9u1e")
			.WithCorrelationId("corr-az9u1e")
			.WithMessageType("OrderPlaced")
			.WithContentType("application/json");

	private static MessageMetadata RoundTrip(MessageMetadata original)
	{
		// MessageMetadata carries [JsonConverter(typeof(MessageMetadataJsonConverter))], so the
		// default serializer path exercises the real production converter (no test-only options).
		var json = JsonSerializer.Serialize(original);
		var restored = JsonSerializer.Deserialize<MessageMetadata>(json);
		restored.ShouldNotBeNull();
		return restored;
	}

	// ===== Fact 1: typed Attributes survive the JSON round-trip (RED pre-fix) =====

	[Fact]
	public void RoundTripTypedAttributes()
	{
		// Populate via the CONCRETE builder, NOT a fluent chain: the concrete AddAttribute returns
		// IMessageMetadataBuilder, so chaining a 2nd .AddAttribute would bind to the extension method
		// (MetadataBuilderCollectionExtensions) which REPLACES + stores a raw array GetAttributes() can't
		// read (builder-API trap surfaced via az9u1e, tracked separately). Calling on the concrete-typed
		// variable keeps both on the GetAttributes()-readable accumulation path.
		var builder = BaseBuilder();
		_ = builder.AddAttribute("attr-1", "attr-value-1");
		_ = builder.AddAttribute("attr-2", "attr-value-2");
		var original = (MessageMetadata)builder.Build();

		var json = JsonSerializer.Serialize(original);
		using (var doc = JsonDocument.Parse(json))
		{
			// SA-ruled wire contract: attributes are restored as a top-level object (not buried as a
			// stringified entry inside `properties`). Absent on the pre-fix converter -> RED.
			doc.RootElement.TryGetProperty("attributes", out _)
				.ShouldBeTrue("the wire must carry a top-level 'attributes' object (SA ruling 16887)");
		}

		var restored = RoundTrip(original);

		var attributes = restored.GetAttributes();
		attributes.Count.ShouldBe(2);
		attributes.ShouldContainKey("attr-1");
		attributes.ShouldContainKey("attr-2");
		attributes["attr-1"].ToString().ShouldBe("attr-value-1");
		attributes["attr-2"].ToString().ShouldBe("attr-value-2");
	}

	// ===== Fact 2: typed Items survive the JSON round-trip (RED pre-fix) =====

	[Fact]
	public void RoundTripTypedItems()
	{
		// Concrete (non-chained) population — see RoundTripTypedAttributes for why chaining flips to the
		// broken extension AddItem. Both calls bind the concrete builder's GetItems()-readable path.
		var builder = BaseBuilder();
		_ = builder.AddItem("item-1", "item-value-1");
		_ = builder.AddItem("item-2", "item-value-2");
		var original = (MessageMetadata)builder.Build();

		var json = JsonSerializer.Serialize(original);
		using (var doc = JsonDocument.Parse(json))
		{
			doc.RootElement.TryGetProperty("items", out _)
				.ShouldBeTrue("the wire must carry a top-level 'items' object (SA ruling 16887)");
		}

		var restored = RoundTrip(original);

		var items = restored.GetItems();
		items.Count.ShouldBe(2);
		items.ShouldContainKey("item-1");
		items.ShouldContainKey("item-2");
		items["item-1"].ToString().ShouldBe("item-value-1");
		items["item-2"].ToString().ShouldBe("item-value-2");
	}

	// ===== Fact 3: Claims survive as an explicit lossless array (RED pre-fix) =====

	[Fact]
	public void RoundTripClaimsAsExplicitLosslessArray()
	{
		// Non-default ValueType + Issuer so they MUST be emitted and reconstructed (not defaulted).
		var claim = new Claim(
			type: "permission",
			value: "orders.read",
			valueType: "http://example.com/customType",
			issuer: "https://issuer.example.com");

		var original = (MessageMetadata)BaseBuilder()
			.AddClaim(claim)
			.Build();

		var json = JsonSerializer.Serialize(original);
		using (var doc = JsonDocument.Parse(json))
		{
			doc.RootElement.TryGetProperty("claims", out var claimsElement).ShouldBeTrue(
				"the wire must carry a top-level 'claims' array (SA ruling 16887)");
			claimsElement.ValueKind.ShouldBe(JsonValueKind.Array);
		}

		var restored = RoundTrip(original);

		var restoredClaim = restored.Security.Claims.ShouldHaveSingleItem();
		restoredClaim.Type.ShouldBe("permission");
		restoredClaim.Value.ShouldBe("orders.read");
		restoredClaim.ValueType.ShouldBe("http://example.com/customType");
		restoredClaim.Issuer.ShouldBe("https://issuer.example.com");
	}

	// ===== Fact 4: empty Attributes/Items/Claims round-trip empty, no spurious wire keys (EC-6 guard) =====

	[Fact]
	public void RoundTripEmptyCollectionsWithoutSpuriousWireKeys()
	{
		var original = (MessageMetadata)BaseBuilder().Build();

		var json = JsonSerializer.Serialize(original);
		using (var doc = JsonDocument.Parse(json))
		{
			var root = doc.RootElement;
			root.TryGetProperty("attributes", out _)
				.ShouldBeFalse("empty attributes must not emit a spurious top-level key");
			root.TryGetProperty("items", out _)
				.ShouldBeFalse("empty items must not emit a spurious top-level key");
			root.TryGetProperty("claims", out _)
				.ShouldBeFalse("empty claims must not emit a spurious top-level key");
		}

		var restored = RoundTrip(original);

		restored.GetAttributes().ShouldBeEmpty();
		restored.GetItems().ShouldBeEmpty();
		restored.Security.Claims.ShouldBeEmpty();
	}
}
