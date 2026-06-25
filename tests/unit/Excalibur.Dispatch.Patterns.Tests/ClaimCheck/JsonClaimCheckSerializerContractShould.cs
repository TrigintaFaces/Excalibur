// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Text;

using Excalibur.Dispatch.Patterns.ClaimCheck;
using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

/// <summary>
/// Sprint 847 / Lane M (bead m8ecyl) — author≠impl regression lock for the
/// <see cref="JsonClaimCheckSerializer"/> error/null-contract divergence (MS-M).
/// </summary>
/// <remarks>
/// <para>
/// <b>Defect (true pre-fix HEAD <c>301b4aa62</c>):</b> <see cref="JsonClaimCheckSerializer"/> implements
/// <see cref="ISerializer"/> but violates the documented deserialize-failure contract that every sibling
/// impl honors. <c>Deserialize&lt;T&gt;</c> / <c>DeserializeObject</c> return <c>null!</c> (the <c>!</c>
/// suppresses the compiler, not the runtime null), and the serialize/deserialize methods let raw
/// <see cref="System.Text.Json.JsonException"/> propagate instead of wrapping it as
/// <see cref="SerializationException"/>. A poison/corrupt claim-check blob deserializing to JSON
/// <c>null</c> therefore produces an NRE far downstream instead of a diagnosable, DLQ-routable
/// <see cref="SerializationException"/>.
/// </para>
/// <para>
/// <b>Fix (FR-M1..M4):</b> mirror the canonical <c>SystemTextJsonSerializer</c> —
/// <c>?? throw SerializationException.NullResult&lt;T&gt;()</c> /
/// <c>NullResultForType(type)</c> on null results; wrap underlying failures via
/// <c>Wrap&lt;T&gt;</c>/<c>WrapObject</c>; re-throw an existing <see cref="SerializationException"/>
/// unchanged; preserve unwrapped <see cref="ArgumentNullException"/> guards.
/// </para>
/// <para>
/// <b>Non-vacuity:</b> on the pre-fix HEAD, deserializing the JSON <c>null</c> literal returns a real
/// null (no throw) and malformed JSON throws raw <see cref="System.Text.Json.JsonException"/> — so the
/// throw-assertions below are RED. Post-fix they are GREEN. The serializer is <c>internal sealed</c>;
/// the test project has <c>InternalsVisibleTo</c> (the sibling <c>JsonClaimCheckSerializerShould</c>
/// constructs it directly).
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Patterns)]
[Trait("Feature", "ClaimCheck")]
public sealed class JsonClaimCheckSerializerContractShould
{
	private readonly JsonClaimCheckSerializer _serializer = new();

	private static readonly byte[] JsonNullLiteral = Encoding.UTF8.GetBytes("null");
	private static readonly byte[] MalformedJson = Encoding.UTF8.GetBytes("{ this is not valid json");

	[Fact]
	public void ThrowSerializationException_WhenDeserializeProducesNull_NotReturnNull()
	{
		// AC-M1 — JSON `null` literal into a reference type. Pre-fix: returns null via `!`. Post-fix: throws.
		_ = Should.Throw<SerializationException>(
			() => _serializer.Deserialize<PoisonPayload>(JsonNullLiteral));
	}

	[Fact]
	public void ThrowSerializationException_WhenDeserializeObjectProducesNull()
	{
		// AC-M2 — same null-result contract via the non-generic object path.
		_ = Should.Throw<SerializationException>(
			() => _serializer.DeserializeObject(JsonNullLiteral, typeof(PoisonPayload)));
	}

	[Fact]
	public void ThrowSerializationException_NotRawJsonException_OnMalformedDeserialize()
	{
		// AC-M3 — malformed JSON. Pre-fix: raw System.Text.Json.JsonException escapes. Post-fix: wrapped.
		_ = Should.Throw<SerializationException>(
			() => _serializer.Deserialize<PoisonPayload>(MalformedJson));
	}

	[Fact]
	public void ThrowSerializationException_NotRawJsonException_OnMalformedDeserializeObject()
	{
		// AC-M3 (object path).
		_ = Should.Throw<SerializationException>(
			() => _serializer.DeserializeObject(MalformedJson, typeof(PoisonPayload)));
	}

	[Fact]
	public void PreserveUnwrappedArgumentNullGuards_OnSerializeObject()
	{
		// AC-M4 — ArgumentNullException guards must remain, unwrapped (not re-wrapped as SerializationException).
		_ = Should.Throw<ArgumentNullException>(
			() => _serializer.SerializeObject(null!, typeof(PoisonPayload)));
		_ = Should.Throw<ArgumentNullException>(
			() => _serializer.SerializeObject(new PoisonPayload(), null!));
	}

	[Fact]
	public void PreserveUnwrappedArgumentNullGuard_OnSerialize_WhenBufferWriterIsNull()
	{
		// AC-M4 — Serialize<T> ArgumentNull guard preserved, unwrapped.
		_ = Should.Throw<ArgumentNullException>(
			() => _serializer.Serialize(new PoisonPayload(), (IBufferWriter<byte>)null!));
	}

	private sealed class PoisonPayload
	{
		public int Id { get; set; }
		public string? Name { get; set; }
	}
}
