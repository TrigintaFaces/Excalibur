using Excalibur.Dispatch.Serialization.MemoryPack;
using Wire = MemoryPack.MemoryPackSerializer;

namespace Excalibur.Dispatch.Serialization.Tests.MemoryPack;

[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
[Trait("Pattern", "Conformance")]
public sealed class EventEnvelopeWireCompatibilityShould
{
    // Produced by the unmodified MemoryPack 1.21.4 generator in a separate process.
    // Do not generate this during reader tests: that would initialize shared formatters first.
    private const string Fixture = "CQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA+v///wUAAABPcmRlcvr///8FAAAAQWRkZWQAAAAAAAAAAAIAAAABAgAAAAAAAAAAAIC19/V/nwgBAAAA/P///wMAAABrZXn7////AgAAAMOpzrsBAAAA";

    [Fact]
    public void InitializeDirectReaderBeforeAnySerializerCall()
    {
        using var state = global::MemoryPack.MemoryPackReaderOptionalStatePool.Rent(null);
        var reader = new global::MemoryPack.MemoryPackReader(Convert.FromBase64String(Fixture), state);
        try { VerifyFixture(reader.ReadPackable<EventEnvelope>()); }
        finally { reader.Dispose(); }
    }

    [Fact]
    public void InitializeGenericReaderBeforeAnySerializerCall() =>
        VerifyFixture(Wire.Deserialize<EventEnvelope>(Convert.FromBase64String(Fixture)));

    [Fact]
    public void InitializeNonGenericReaderBeforeAnySerializerCall() =>
        VerifyFixture((EventEnvelope?)ReadRuntimeType(typeof(EventEnvelope), Convert.FromBase64String(Fixture)));

    private static object? ReadRuntimeType(Type type, byte[] payload) => Wire.Deserialize(type, payload);

    private static void VerifyFixture(EventEnvelope? envelope)
    {
        envelope.ShouldNotBeNull();
        envelope.Metadata!["key"].ShouldBe("éλ");
        envelope.OccurredAt.ShouldBe(DateTimeOffset.UnixEpoch);
        Wire.Serialize(envelope).ShouldBe(Convert.FromBase64String(Fixture));
    }

    [Fact]
    public void InitializeDirectWriterBeforeAnySerializerCall()
    {
        var envelope = new EventEnvelope
        {
            EventId = Guid.Empty, AggregateId = Guid.Empty, Version = 0, OccurredAt = DateTimeOffset.UnixEpoch,
            AggregateType = "Order", EventType = "Added", Payload = [1, 2],
            Metadata = new Dictionary<string, string> { ["key"] = "éλ" }, SchemaVersion = 1,
        };
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using var state = global::MemoryPack.MemoryPackWriterOptionalStatePool.Rent(null);
        var writer = new global::MemoryPack.MemoryPackWriter<System.Buffers.ArrayBufferWriter<byte>>(ref buffer, state);
        writer.WritePackable(envelope);
        writer.Flush();
        var baseline = new LegacyGeneratedEventEnvelope
        {
            EventId = envelope.EventId, AggregateId = envelope.AggregateId,
            AggregateType = envelope.AggregateType, EventType = envelope.EventType,
            Payload = envelope.Payload, Metadata = envelope.Metadata,
            Version = envelope.Version, OccurredAt = envelope.OccurredAt, SchemaVersion = envelope.SchemaVersion,
        };
        buffer.WrittenSpan.ToArray().ShouldBe(Wire.Serialize(baseline));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreserveGeneratedWireBytesAndOlderSchema(bool hasMetadata)
    {
        var original = new LegacyGeneratedEventEnvelope
        {
            EventId = Guid.NewGuid(), AggregateId = Guid.NewGuid(),
            AggregateType = "Order", EventType = "OrderAdded", Version = long.MaxValue,
            Payload = [0, 1, 127, 255],
            OccurredAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(5)),
            Metadata = hasMetadata ? new Dictionary<string, string> { ["unicode"] = "éλ", ["empty"] = "" } : null,
            SchemaVersion = 123,
        };
        var bytes = Wire.Serialize(original);
        var current = Wire.Deserialize<EventEnvelope>(bytes);
        Wire.Serialize(current).ShouldBe(bytes);
        var restored = Wire.Deserialize<LegacyGeneratedEventEnvelope>(Wire.Serialize(current));
        restored!.OccurredAt.Offset.ShouldBe(original.OccurredAt.Offset);
        restored.SchemaVersion.ShouldBe(original.SchemaVersion);
        restored.Payload.ShouldBe(original.Payload);

        // The ninth field is the final int32. Existing eight-field records must remain readable.
        var older = bytes[..^sizeof(int)];
        older[0] = 8;
        var oldExpected = Wire.Deserialize<LegacyGeneratedEventEnvelope>(older);
        var oldActual = Wire.Deserialize<EventEnvelope>(older);
        Wire.Serialize(oldActual).ShouldBe(Wire.Serialize(oldExpected));
    }

    [Fact]
    public void PreserveNullAndRejectFutureFieldCounts()
    {
        var nullBytes = Wire.Serialize<LegacyGeneratedEventEnvelope?>(null);
        Wire.Deserialize<EventEnvelope>(nullBytes).ShouldBeNull();
        Wire.Serialize<EventEnvelope?>(null).ShouldBe(nullBytes);
        Should.Throw<global::MemoryPack.MemoryPackSerializationException>(() => Wire.Deserialize<EventEnvelope>(new byte[] { 10 }));
        Should.Throw<global::MemoryPack.MemoryPackSerializationException>(() => Wire.Deserialize<LegacyGeneratedEventEnvelope>(new byte[] { 10 }));
    }
}
