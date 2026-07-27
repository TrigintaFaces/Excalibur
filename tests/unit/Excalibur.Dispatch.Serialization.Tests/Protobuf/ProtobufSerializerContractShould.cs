// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Serialization.Protobuf;

namespace Excalibur.Dispatch.Serialization.Tests.Protobuf;

/// <summary>
/// Sprint 862 / Lane D (bead 0m2uua) — author≠impl regression lock for the
/// <see cref="ProtobufSerializer.DeserializeObject(System.ReadOnlySpan{byte}, System.Type)"/>
/// error-contract divergence.
/// </summary>
/// <remarks>
/// <para>
/// <b>Defect (pre-fix HEAD):</b> the non-generic <c>DeserializeObject(data, type)</c> parse body was
/// NOT wrapped in a try/catch, so a malformed/corrupt payload let the raw
/// <c>Google.Protobuf.InvalidProtocolBufferException</c> propagate instead of a diagnosable,
/// DLQ-routable <see cref="SerializationException"/> — diverging from the documented
/// <see cref="ISerializer"/> deserialize-failure contract that every sibling path honors.
/// </para>
/// <para>
/// <b>Fix:</b> wrap the parse body — re-throw an existing <see cref="SerializationException"/>
/// unchanged, otherwise <c>SerializationException.WrapObject(type, "deserialize", ex)</c>. The
/// <c>ArgumentNullException.ThrowIfNull(type)</c> guard stays BEFORE the try, so it surfaces unwrapped.
/// </para>
/// <para>
/// <b>Non-vacuity:</b> on the pre-fix HEAD, deserializing malformed bytes throws raw
/// <c>InvalidProtocolBufferException</c> (NOT a <see cref="SerializationException"/>) — so
/// <see cref="WrapMalformedDeserialize_AsSerializationException"/> is RED. Post-fix it is GREEN. The
/// null-type guard assertion locks the documented "unwrapped" contract for the preserved guard.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Serialization)]
[Trait("Feature", "Protobuf")]
public sealed class ProtobufSerializerContractShould
{
	private readonly ProtobufSerializer _serializer = new();

	// An overlong/never-terminated varint — not a valid Protobuf wire payload; ParseFrom throws.
	private static readonly byte[] MalformedProtobuf =
		[0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];

	[Fact]
	public void WrapMalformedDeserialize_AsSerializationException()
	{
		// 0m2uua — malformed binary payload. Pre-fix: raw InvalidProtocolBufferException escapes.
		// Post-fix: wrapped as SerializationException. RED on pre-fix HEAD, GREEN post-fix.
		_ = Should.Throw<SerializationException>(
			() => _serializer.DeserializeObject(MalformedProtobuf, typeof(TestMessage)));
	}

	[Fact]
	public void PreserveUnwrappedArgumentNullGuard_OnDeserializeObject_WhenTypeIsNull()
	{
		// The ThrowIfNull(type) guard sits before the try, so it must surface unwrapped
		// (an ArgumentNullException, NOT re-wrapped as SerializationException).
		_ = Should.Throw<ArgumentNullException>(
			() => _serializer.DeserializeObject(MalformedProtobuf, null!));
	}
}
