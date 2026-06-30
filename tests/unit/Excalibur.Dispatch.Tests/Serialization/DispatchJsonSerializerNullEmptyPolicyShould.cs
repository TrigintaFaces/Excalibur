// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Tests.Serialization;

/// <summary>
/// Independent regression lock for <c>bd-ihv7fe</c> (S856): <see cref="DispatchJsonSerializer"/>'s read
/// path must align to the <c>ISerializer</c> family's single null/empty policy — an empty payload or a
/// null deserialization result is a <b>poison signal</b> that throws <see cref="SerializationException"/>,
/// never a silent <see langword="null"/>.
/// </summary>
/// <remarks>
/// <para>
/// Author≠implementer: BackendDeveloper owns the source fix (<c>DispatchJsonSerializer.cs</c>); this is the
/// independent lock.
/// </para>
/// <para>
/// <b>Pre-fix behaviour (RED):</b> <c>DeserializeFromBytes</c> returned <see langword="null"/> on an empty
/// span and on a null result; the stream overloads returned <see langword="null"/> on a null result —
/// silently swallowing a poison/empty payload. <c>hxoyaq</c> fixed the sibling serializers
/// (<c>SystemTextJsonSerializer</c>/<c>PayloadSerializer</c>) but not this one. The fix throws
/// <see cref="SerializationException"/> (<c>EmptyPayload</c> / <c>NullResult</c>), restoring poison-detection
/// symmetry. These tests assert the throw and are RED against the pre-fix silent-null.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class DispatchJsonSerializerNullEmptyPolicyShould
{
    [Fact]
    public void ThrowOnEmptyPayload_ForDeserializeFromBytes()
    {
        using var serializer = new DispatchJsonSerializer();

        // Empty input is a poison signal (RED pre-fix: returned null silently).
        _ = Should.Throw<SerializationException>(
            () => serializer.DeserializeFromBytes(ReadOnlySpan<byte>.Empty, typeof(Payload)));
    }

    [Fact]
    public void ThrowOnNullResult_ForDeserializeFromBytes()
    {
        using var serializer = new DispatchJsonSerializer();

        // A literal JSON null deserializes to a null reference → poison, not a silent null (RED pre-fix).
        _ = Should.Throw<SerializationException>(
            () => serializer.DeserializeFromBytes("null"u8, typeof(Payload)));
    }

    [Fact]
    public async Task ThrowOnNullResult_ForDeserializeFromStreamGeneric()
    {
        using var serializer = new DispatchJsonSerializer();
        using var stream = new MemoryStream("null"u8.ToArray());

        // A null deserialization result throws NullResult, not a silent null (RED pre-fix).
        _ = await Should.ThrowAsync<SerializationException>(
            async () => await serializer.DeserializeFromStreamAsync<Payload>(stream, CancellationToken.None));
    }

    private sealed class Payload
    {
        public int Value { get; init; }
    }
}
