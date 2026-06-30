// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Tests.EventSourcing;

/// <summary>
/// Independent regression lock for <c>bd-ifgj5w</c> (S856): <see cref="JsonEventSerializer"/>'s read path
/// must surface <b>all</b> deserialization failures as <see cref="SerializationException"/> (the canonical
/// serializer contract, matching <c>SpanEventSerializer</c>), so event-store read failures are uniformly
/// catchable / poison-routable across the repository, projection host, and rebuild.
/// </summary>
/// <remarks>
/// <para>
/// Author≠implementer: BackendDeveloper owns the source fix; this is the independent lock. (Backend's F-5
/// flips updated the pre-existing stale tests; this adds the dedicated author≠impl contract lock PM
/// requested.)
/// </para>
/// <para>
/// <b>Pre-fix behaviour (RED):</b> a malformed payload surfaced a raw <see cref="System.Text.Json.JsonException"/>
/// and a non-event result surfaced a raw <see cref="System.InvalidOperationException"/> — uncatchable via the
/// serializer contract. The fix wraps both as <see cref="SerializationException"/> (the inner exception is
/// preserved). These tests assert the canonical type, RED against the pre-fix raw exceptions.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class JsonEventSerializerExceptionContractShould : UnitTestBase
{
    [RequiresDynamicCode("Test requires dynamic code")]
    [RequiresUnreferencedCode("Test requires unreferenced code")]
    private static JsonEventSerializer CreateSerializer() => new();

    [Fact]
    [RequiresDynamicCode("Test requires dynamic code")]
    [RequiresUnreferencedCode("Test requires unreferenced code")]
    public void WrapMalformedJsonAsSerializationException_OnDeserialize()
    {
        var serializer = CreateSerializer();

        // Malformed JSON → the read path must surface SerializationException, not a raw JsonException.
        _ = Should.Throw<SerializationException>(
            () => serializer.DeserializeEvent("{ this is not valid json"u8.ToArray(), typeof(NotAnEvent)));
    }

    [Fact]
    [RequiresDynamicCode("Test requires dynamic code")]
    [RequiresUnreferencedCode("Test requires unreferenced code")]
    public void ThrowSerializationException_WhenDeserializedResultIsNotADomainEvent()
    {
        var serializer = CreateSerializer();

        // Valid JSON that deserializes to a non-IDomainEvent type → SerializationException (not a raw
        // InvalidOperationException / silent null).
        _ = Should.Throw<SerializationException>(
            () => serializer.DeserializeEvent("{}"u8.ToArray(), typeof(NotAnEvent)));
    }

    // A plain type that does NOT implement IDomainEvent.
    private sealed class NotAnEvent
    {
        public int Value { get; init; }
    }
}
