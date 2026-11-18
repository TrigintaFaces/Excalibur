// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Outbox.MongoDB;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace Excalibur.Data.Tests.MongoDB.Outbox;

[Trait("Category", "Unit")]
[Trait("Component", "MongoDB")]
[Trait("Pattern", "Outbox")]
public sealed class MongoDbOutboxSequenceRoundTripShould
{
    private static readonly byte[] Payload = [1];

    [Theory]
    [InlineData(0L)]
    [InlineData(7L)]
    [InlineData(9007199254740993L)]
    [InlineData(long.MaxValue)]
    public void PreserveExactInt64ThroughBothMappingsAndActualBsonSerialization(long sequence)
    {
        var message = new OutboundMessage("event", Payload, "destination")
        {
            Id = "sequence", PartitionKey = "partition", SequenceNumber = sequence,
        };
        var document = MongoDbOutboxDocument.FromOutboundMessage(message);
        var raw = document.ToBsonDocument();
        raw["sequenceNumber"].BsonType.ShouldBe(BsonType.Int64);
        raw["sequenceNumber"].AsInt64.ShouldBe(sequence);
        var restored = BsonSerializer.Deserialize<MongoDbOutboxDocument>(raw).ToOutboundMessage();
        restored.SequenceNumber.ShouldBe(sequence);
        restored.PartitionKey.ShouldBe("partition");
        restored.Id.ShouldBe(message.Id);
    }

    [Fact]
    public void ReadMissingSequenceAsUnsequencedZero()
    {
        var raw = new BsonDocument
        {
            { "_id", "legacy" }, { "messageType", "event" }, { "payload", new BsonBinaryData(Payload) },
            { "destination", "destination" },
        };
        var restored = BsonSerializer.Deserialize<MongoDbOutboxDocument>(raw).ToOutboundMessage();
        restored.SequenceNumber.ShouldBe(0L);
        restored.Id.ShouldBe("legacy");
    }
}
