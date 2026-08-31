// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Excalibur.Dispatch;
using Excalibur.EventSourcing.AwsS3;
using Excalibur.EventSourcing.AzureBlob;
using Excalibur.EventSourcing.Gcs;

namespace Excalibur.EventSourcing.Tests.ColdStorage;

/// <summary>
/// Pins the cold-archive wire format for the three object-storage archive stores, and ties that format to
/// the single canonical event serializer contract rather than to three hand-copied attribute bags.
/// </summary>
/// <remarks>
/// <para>
/// These stores hold archived events that consumers have already written. The archive is read back by the
/// same source-generated contract that wrote it, so any drift in property naming, null handling or enum
/// representation silently orphans existing archives -- a fault that surfaces on a restore, long after the
/// change that caused it.
/// </para>
/// <para>
/// The golden below is the literal byte sequence the archive path produces. It is deliberately a frozen
/// string and not a re-derivation: a test that recomputes the expected value from the same code under test
/// would agree with any change, including a breaking one.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class ColdArchiveWireFormatShould
{
    /// <summary>
    /// The frozen cold-archive wire format. Exercises every shape the archive carries: a byte[] payload
    /// (base64), a present and an absent nullable byte[], a long, and a DateTimeOffset.
    /// </summary>
    private const string GoldenArchiveJson =
        """[{"eventId":"e-1","aggregateId":"a-1","aggregateType":"Order","eventType":"OrderPlaced","eventData":"AQID","metadata":"BAU=","version":7,"timestamp":"2026-01-02T03:04:05+00:00","globalPosition":42},{"eventId":"e-2","aggregateId":"a-1","aggregateType":"Order","eventType":"OrderShipped","eventData":"","version":8,"timestamp":"2026-01-02T03:04:06+00:00","globalPosition":0}]""";

    private static List<StoredEvent> Fixture() =>
    [
        new StoredEvent(
            "e-1", "a-1", "Order", "OrderPlaced",
            [1, 2, 3], [4, 5], 7,
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero))
        { GlobalPosition = 42 },
        new StoredEvent(
            "e-2", "a-1", "Order", "OrderShipped",
            [], null, 8,
            new DateTimeOffset(2026, 1, 2, 3, 4, 6, TimeSpan.Zero)),
    ];

    private static string Serialize(JsonTypeInfo<List<StoredEvent>> typeInfo) =>
        Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(Fixture(), typeInfo));

    public static TheoryData<string, JsonTypeInfo<List<StoredEvent>>> ArchiveContexts() => new()
    {
        { "AwsS3", AwsS3ColdEventStore.ArchiveTypeInfo },
        { "AzureBlob", AzureBlobColdEventStore.ArchiveTypeInfo },
        { "Gcs", GcsColdEventStore.ArchiveTypeInfo },
    };

    [Theory]
    [MemberData(nameof(ArchiveContexts))]
    public void WriteTheFrozenArchiveFormat(string provider, JsonTypeInfo<List<StoredEvent>> typeInfo) =>
        Serialize(typeInfo).ShouldBe(
            GoldenArchiveJson,
            $"the {provider} cold archive holds events consumers have already written; its wire format is frozen");

    /// <summary>
    /// The archive format must BE the canonical event contract, not a coincidental match to it. Each store
    /// serializes through options sourced from <c>EventSerializationDefaults.CreateCanonicalOptions()</c>,
    /// so a change to that one contract moves the archive format -- and goes RED here -- rather than
    /// silently orphaning archives written under the old one.
    /// </summary>
    [Theory]
    [MemberData(nameof(ArchiveContexts))]
    public void SourceTheCanonicalEventContract(string provider, JsonTypeInfo<List<StoredEvent>> typeInfo)
    {
        var canonical = EventSerializationDefaults.CreateCanonicalOptions();

        typeInfo.Options.PropertyNamingPolicy.ShouldBe(
            canonical.PropertyNamingPolicy,
            $"the {provider} archive must take its naming policy from the canonical event contract");
        typeInfo.Options.DefaultIgnoreCondition.ShouldBe(
            canonical.DefaultIgnoreCondition,
            $"the {provider} archive must take its null handling from the canonical event contract");
        typeInfo.Options.IsReadOnly.ShouldBeTrue(
            $"the {provider} archive options are frozen once the type-info resolver is attached");
    }

    /// <summary>
    /// Non-vacuity: the golden must actually discriminate. A non-canonical contract (PascalCase, nulls
    /// written) must NOT match it.
    /// </summary>
    [Fact]
    public void RejectANonCanonicalContract()
    {
        var divergent = new JsonSerializerOptions
        {
            TypeInfoResolver = AwsS3ColdEventStore.ArchiveTypeInfo.Options.TypeInfoResolver,
        };

        var written = Encoding.UTF8.GetString(
            JsonSerializer.SerializeToUtf8Bytes(
                Fixture(),
                (JsonTypeInfo<List<StoredEvent>>)divergent.GetTypeInfo(typeof(List<StoredEvent>))));

        written.ShouldNotBe(GoldenArchiveJson, "the golden must discriminate a non-canonical contract");
    }
}
