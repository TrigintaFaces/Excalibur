// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

using Excalibur.Dispatch;

namespace Excalibur.Data.DynamoDb.Tests.Snapshots;

/// <summary>
/// Pins the stored shape of snapshot metadata across the move from the default serializer options to the
/// canonical event-serialization options.
/// </summary>
/// <remarks>
/// <para>
/// The snapshot document previously wrote metadata with <c>JsonSerializer.Serialize(metadata)</c> — no
/// options at all, so <see cref="JsonSerializerOptions.Default"/>. It now writes through the canonical
/// options so a host-supplied type-info resolver can reach the call, which is what lets the store fail
/// closed instead of suppressing a trim warning it could not honour.
/// </para>
/// <para>
/// <b>Snapshot metadata is persisted, so that is a stored-format change and it is recorded here rather
/// than assumed.</b> The canonical options differ from the default in three ways, and only ONE of them
/// reaches this data:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <c>Converters = { JsonStringEnumConverter }</c> — <b>REACHES IT.</b> An enum metadata value was written
/// as its numeric value and is now written as its name. This is the deliberate change; the arm below pins
/// both spellings so the move is visible rather than silent.
/// </description></item>
/// <item><description>
/// <c>PropertyNamingPolicy = CamelCase</c> — does NOT reach it. That governs PROPERTY names; dictionary
/// KEYS are governed by <c>DictionaryKeyPolicy</c>, which is not set.
/// </description></item>
/// <item><description>
/// <c>DefaultIgnoreCondition = WhenWritingNull</c> — does NOT reach it. That governs object properties, not
/// dictionary values, so a null metadata value is still written as <c>null</c>.
/// </description></item>
/// </list>
/// <para>
/// The second and third were predicted to change and measured not to. They are pinned as SAFETY arms
/// precisely because the prediction was wrong: if a later change sets <c>DictionaryKeyPolicy</c> or moves
/// metadata onto a typed object, every stored snapshot's metadata silently stops matching, and only a
/// standing arm turns that into a failing build rather than unreadable data.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class DynamoDbSnapshotMetadataSerializationShould
{
	private enum Flavour
	{
		Vanilla = 0,
		Chocolate = 1,
	}

	/// <summary>The options the document used before the canonical options replaced them: none.</summary>
	private static string SerializeAsLegacy(Dictionary<string, object> metadata) =>
		JsonSerializer.Serialize(metadata);

	private static string SerializeAsCanonical(Dictionary<string, object> metadata) =>
		JsonSerializer.Serialize(metadata, EventSerializationDefaults.CreateCanonicalOptions());

	/// <summary>
	/// Metadata keys chosen to be hostile to a naming policy, with no enum value, so that the ONE intended
	/// difference is excluded and any OTHER difference shows up alone.
	/// </summary>
	private static Dictionary<string, object> NonEnumMetadata() => new(StringComparer.Ordinal)
	{
		["Content-Type"] = "application/json",
		["UPPERCASE"] = "shouted",
		["already_snake"] = "unchanged",
		["X-Correlation-ID"] = "corr-42",
		["A"] = "single-letter key",
		[""] = "empty key is legal in a dictionary",
		["anInt"] = 42,
		["aBool"] = true,
		["aNull"] = null!,
	};

	/// <summary>
	/// SAFETY. For metadata carrying no enum, the canonical options write byte-for-byte what the default
	/// options wrote. This is the arm that would fail if a naming or null-handling policy ever started
	/// reaching dictionary entries.
	/// </summary>
	[Fact]
	public void WriteMetadataWithoutEnumsExactlyAsTheDefaultOptionsDid()
	{
		var legacy = SerializeAsLegacy(NonEnumMetadata());
		var canonical = SerializeAsCanonical(NonEnumMetadata());

		canonical.ShouldBe(legacy);
	}

	/// <summary>
	/// SAFETY, stated as the concrete spellings rather than as an equality, so the arm names what it
	/// protects: keys verbatim, null present. The negative arms are Case.Sensitive deliberately —
	/// Shouldly compares case-insensitively by default, which would make a camelCase-versus-PascalCase
	/// assertion incapable of ever failing.
	/// </summary>
	[Fact]
	public void LeaveDictionaryKeysVerbatimAndKeepNullValues()
	{
		var canonical = SerializeAsCanonical(NonEnumMetadata());

		canonical.ShouldContain("\"Content-Type\":");
		canonical.ShouldContain("\"UPPERCASE\":");
		canonical.ShouldContain("\"already_snake\":");
		canonical.ShouldNotContain("\"contentType\":", Case.Sensitive);
		canonical.ShouldNotContain("\"uPPERCASE\":", Case.Sensitive);
		canonical.ShouldContain("\"aNull\":null");
	}

	/// <summary>
	/// THE DELIBERATE CHANGE, pinned in both spellings. An enum metadata value used to persist as its
	/// number and now persists as its name. Recorded as a difference, not smuggled through an equality
	/// assertion that would have had to be written backwards to pass.
	/// </summary>
	[Fact]
	public void WriteEnumMetadataValuesAsNamesWhereTheDefaultOptionsWroteNumbers()
	{
		var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["flavour"] = Flavour.Chocolate,
		};

		SerializeAsLegacy(metadata).ShouldBe("{\"flavour\":1}");
		SerializeAsCanonical(metadata).ShouldBe("{\"flavour\":\"Chocolate\"}");
	}

	/// <summary>
	/// SAFETY, and the arm that decides whether the format change is breaking for stored data: metadata
	/// written by an earlier version still reads back under the canonical options, with the same runtime
	/// shape it always had. Values deserialize into <see cref="JsonElement"/> because the destination is
	/// <c>object</c>, so the enum converter is never consulted on the read path.
	/// </summary>
	[Fact]
	public void StillReadMetadataWrittenByTheDefaultOptions()
	{
		var storedByAnEarlierVersion = SerializeAsLegacy(new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["flavour"] = Flavour.Chocolate,
			["Content-Type"] = "application/json",
			["aNull"] = null!,
		});

		var readBack = JsonSerializer.Deserialize<Dictionary<string, object>>(
			storedByAnEarlierVersion,
			EventSerializationDefaults.CreateCanonicalOptions());

		_ = readBack.ShouldNotBeNull();
		readBack.Count.ShouldBe(3);
		readBack["flavour"].ShouldBeOfType<JsonElement>().GetInt32().ShouldBe(1);
		readBack["Content-Type"].ShouldBeOfType<JsonElement>().GetString().ShouldBe("application/json");
		readBack["aNull"].ShouldBeNull();
	}

	/// <summary>
	/// LIVENESS. The metadata helper used on the resolver branch refuses when the options carry no
	/// resolver, rather than falling back to reflection. Without this the fail-closed guard the store now
	/// depends on could be removed and every other arm here would still pass.
	/// </summary>
	[Fact]
	public void RefuseToWriteMetadataThroughTheResolverHelperWhenNoResolverIsAttached()
	{
		var withoutResolver = EventSerializationDefaults.CreateCanonicalOptions();

		_ = Should.Throw<NotSupportedException>(() =>
			EventSerializationDefaults.SerializeMetadataWithResolver(NonEnumMetadata(), withoutResolver));
	}
}
