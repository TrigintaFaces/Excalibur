// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Text.Json;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization.MemoryPack;

using MessagePack;

namespace Excalibur.Dispatch.Benchmarks.Serialization;

/// <summary>
/// Benchmarks for MemoryPack internal serialization per ADR-058.
/// </summary>
/// <remarks>
/// Performance targets (ADR-058):
/// - Serialize OutboxEnvelope: &lt;500ns
/// - Deserialize OutboxEnvelope: &lt;300ns
/// - Memory per operation: &lt;100 bytes
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class MemoryPackSerializationBenchmarks
{
	private IInternalSerializer? _serializer;
	private OutboxEnvelope? _smallEnvelope;
	private OutboxEnvelope? _mediumEnvelope;
	private OutboxEnvelope? _largeEnvelope;
	private EventEnvelope? _eventEnvelope;
	private SnapshotEnvelope? _snapshotEnvelope;

	private byte[]? _smallEnvelopeSerialized;
	private byte[]? _mediumEnvelopeSerialized;
	private byte[]? _largeEnvelopeSerialized;
	private byte[]? _eventEnvelopeSerialized;
	private byte[]? _snapshotEnvelopeSerialized;

	// For comparison with System.Text.Json
	private byte[]? _smallEnvelopeJson;

	private MemoryPackJsonOutboxEnvelope? _jsonEnvelope;

	// For comparison with MessagePack
	private byte[]? _smallEnvelopeMessagePack;

	private MemoryPackMessagePackOutboxEnvelope? _messagePackEnvelope;

	/// <summary>
	/// Initialize serializer and test envelopes before benchmarks.
	/// </summary>
	[GlobalSetup]
	public void GlobalSetup()
	{
		_serializer = new MemoryPackInternalSerializer();

		// Small envelope (~100 bytes payload)
		_smallEnvelope = new OutboxEnvelope
		{
			MessageId = Guid.NewGuid(),
			MessageType = "OrderCreated",
			Payload = new byte[100],
			CreatedAt = DateTimeOffset.UtcNow,
			Headers = new Dictionary<string, string>
			{
				["tenant"] = "acme",
				["source"] = "api",
			},
			CorrelationId = "corr-123",
			CausationId = "cause-456",
			SchemaVersion = 1,
		};
		Random.Shared.NextBytes(_smallEnvelope.Payload);

		// Medium envelope (~1KB payload)
		_mediumEnvelope = new OutboxEnvelope
		{
			MessageId = Guid.NewGuid(),
			MessageType = "LargeOrderCreated",
			Payload = new byte[1024],
			CreatedAt = DateTimeOffset.UtcNow,
			Headers = new Dictionary<string, string>
			{
				["tenant"] = "acme",
				["source"] = "api",
			},
			CorrelationId = "corr-789",
			SchemaVersion = 1,
		};
		Random.Shared.NextBytes(_mediumEnvelope.Payload);

		// Large envelope (~64KB payload)
		_largeEnvelope = new OutboxEnvelope
		{
			MessageId = Guid.NewGuid(),
			MessageType = "BulkDataSync",
			Payload = new byte[64 * 1024],
			CreatedAt = DateTimeOffset.UtcNow,
			SchemaVersion = 1,
		};
		Random.Shared.NextBytes(_largeEnvelope.Payload);

		// Event envelope for event sourcing benchmarks
		_eventEnvelope = new EventEnvelope
		{
			EventId = Guid.NewGuid(),
			AggregateId = Guid.NewGuid(),
			AggregateType = "Order",
			EventType = "OrderCreatedEvent",
			Version = 1,
			Payload = new byte[256],
			OccurredAt = DateTimeOffset.UtcNow,
			Metadata = new Dictionary<string, string>
			{
				["user-id"] = "user-123",
				["ip-address"] = "192.168.1.1",
			},
			SchemaVersion = 1,
		};
		Random.Shared.NextBytes(_eventEnvelope.Payload);

		// Snapshot envelope for snapshot benchmarks (~4KB state)
		_snapshotEnvelope = new SnapshotEnvelope
		{
			AggregateId = Guid.NewGuid(),
			AggregateType = "ShoppingCart",
			Version = 100,
			State = new byte[4 * 1024],
			CreatedAt = DateTimeOffset.UtcNow,
			Metadata = new Dictionary<string, string>
			{
				["snapshot-reason"] = "periodic",
			},
			SchemaVersion = 1,
		};
		Random.Shared.NextBytes(_snapshotEnvelope.State);

		// For JSON comparison
		_jsonEnvelope = new MemoryPackJsonOutboxEnvelope
		{
			MessageId = _smallEnvelope.MessageId,
			MessageType = _smallEnvelope.MessageType,
			Payload = _smallEnvelope.Payload,
			CreatedAt = _smallEnvelope.CreatedAt,
			Headers = _smallEnvelope.Headers,
			CorrelationId = _smallEnvelope.CorrelationId,
			CausationId = _smallEnvelope.CausationId,
			SchemaVersion = 1,
		};

		// For MessagePack comparison
		_messagePackEnvelope = new MemoryPackMessagePackOutboxEnvelope
		{
			MessageId = _smallEnvelope.MessageId,
			MessageType = _smallEnvelope.MessageType,
			Payload = _smallEnvelope.Payload,
			CreatedAt = _smallEnvelope.CreatedAt,
			Headers = _smallEnvelope.Headers,
			CorrelationId = _smallEnvelope.CorrelationId,
			CausationId = _smallEnvelope.CausationId,
			SchemaVersion = 1,
		};

		// Pre-serialize for deserialization benchmarks
		_smallEnvelopeSerialized = _serializer.Serialize(_smallEnvelope);
		_mediumEnvelopeSerialized = _serializer.Serialize(_mediumEnvelope);
		_largeEnvelopeSerialized = _serializer.Serialize(_largeEnvelope);
		_eventEnvelopeSerialized = _serializer.Serialize(_eventEnvelope);
		_snapshotEnvelopeSerialized = _serializer.Serialize(_snapshotEnvelope);

		// Pre-serialize for comparison benchmarks
		_smallEnvelopeJson = JsonSerializer.SerializeToUtf8Bytes(_jsonEnvelope);
		_smallEnvelopeMessagePack = MessagePackSerializer.Serialize(_messagePackEnvelope);
	}

	#region OutboxEnvelope Serialization Benchmarks

	/// <summary>
	/// Benchmark: Serialize small OutboxEnvelope (~100B payload).
	/// </summary>
	[Benchmark(Baseline = true, Description = "MemoryPack Serialize Small (100B payload)")]
	public byte[] SerializeOutboxEnvelopeSmall()
	{
		return _serializer.Serialize(_smallEnvelope);
	}

	/// <summary>
	/// Benchmark: Serialize medium OutboxEnvelope (~1KB payload).
	/// </summary>
	[Benchmark(Description = "MemoryPack Serialize Medium (1KB payload)")]
	public byte[] SerializeOutboxEnvelopeMedium()
	{
		return _serializer.Serialize(_mediumEnvelope);
	}

	/// <summary>
	/// Benchmark: Serialize large OutboxEnvelope (~64KB payload).
	/// </summary>
	[Benchmark(Description = "MemoryPack Serialize Large (64KB payload)")]
	public byte[] SerializeOutboxEnvelopeLarge()
	{
		return _serializer.Serialize(_largeEnvelope);
	}

	#endregion OutboxEnvelope Serialization Benchmarks

	#region OutboxEnvelope Deserialization Benchmarks

	/// <summary>
	/// Benchmark: Deserialize small OutboxEnvelope (~100B payload).
	/// </summary>
	[Benchmark(Description = "MemoryPack Deserialize Small (100B payload)")]
	public OutboxEnvelope DeserializeOutboxEnvelopeSmall()
	{
		return _serializer.Deserialize<OutboxEnvelope>(_smallEnvelopeSerialized.AsSpan());
	}

	/// <summary>
	/// Benchmark: Deserialize medium OutboxEnvelope (~1KB payload).
	/// </summary>
	[Benchmark(Description = "MemoryPack Deserialize Medium (1KB payload)")]
	public OutboxEnvelope DeserializeOutboxEnvelopeMedium()
	{
		return _serializer.Deserialize<OutboxEnvelope>(_mediumEnvelopeSerialized.AsSpan());
	}

	/// <summary>
	/// Benchmark: Deserialize large OutboxEnvelope (~64KB payload).
	/// </summary>
	[Benchmark(Description = "MemoryPack Deserialize Large (64KB payload)")]
	public OutboxEnvelope DeserializeOutboxEnvelopeLarge()
	{
		return _serializer.Deserialize<OutboxEnvelope>(_largeEnvelopeSerialized.AsSpan());
	}

	#endregion OutboxEnvelope Deserialization Benchmarks

	#region EventEnvelope Benchmarks

	/// <summary>
	/// Benchmark: Serialize EventEnvelope.
	/// </summary>
	[Benchmark(Description = "MemoryPack Serialize EventEnvelope")]
	public byte[] SerializeEventEnvelope()
	{
		return _serializer.Serialize(_eventEnvelope);
	}

	/// <summary>
	/// Benchmark: Deserialize EventEnvelope.
	/// </summary>
	[Benchmark(Description = "MemoryPack Deserialize EventEnvelope")]
	public EventEnvelope DeserializeEventEnvelope()
	{
		return _serializer.Deserialize<EventEnvelope>(_eventEnvelopeSerialized.AsSpan());
	}

	#endregion EventEnvelope Benchmarks

	#region SnapshotEnvelope Benchmarks

	/// <summary>
	/// Benchmark: Serialize SnapshotEnvelope (~4KB state).
	/// </summary>
	[Benchmark(Description = "MemoryPack Serialize SnapshotEnvelope (4KB)")]
	public byte[] SerializeSnapshotEnvelope()
	{
		return _serializer.Serialize(_snapshotEnvelope);
	}

	/// <summary>
	/// Benchmark: Deserialize SnapshotEnvelope (~4KB state).
	/// </summary>
	[Benchmark(Description = "MemoryPack Deserialize SnapshotEnvelope (4KB)")]
	public SnapshotEnvelope DeserializeSnapshotEnvelope()
	{
		return _serializer.Deserialize<SnapshotEnvelope>(_snapshotEnvelopeSerialized.AsSpan());
	}

	#endregion SnapshotEnvelope Benchmarks

	#region Zero-Copy BufferWriter Benchmarks

	/// <summary>
	/// Benchmark: Serialize to BufferWriter (zero-copy).
	/// </summary>
	[Benchmark(Description = "MemoryPack Serialize to BufferWriter (zero-copy)")]
	public int SerializeToBufferWriter()
	{
		var bufferWriter = new ArrayBufferWriter<byte>(256);
		_serializer.Serialize(_smallEnvelope, bufferWriter);
		return bufferWriter.WrittenCount;
	}

	#endregion Zero-Copy BufferWriter Benchmarks

	#region Comparison Benchmarks (vs System.Text.Json and MessagePack)

	/// <summary>
	/// Benchmark: System.Text.Json serialize (comparison).
	/// </summary>
	[Benchmark(Description = "System.Text.Json Serialize (comparison)")]
	public byte[] SerializeJsonComparison()
	{
		return JsonSerializer.SerializeToUtf8Bytes(_jsonEnvelope);
	}

	/// <summary>
	/// Benchmark: System.Text.Json deserialize (comparison).
	/// </summary>
	[Benchmark(Description = "System.Text.Json Deserialize (comparison)")]
	public MemoryPackJsonOutboxEnvelope DeserializeJsonComparison()
	{
		return JsonSerializer.Deserialize<MemoryPackJsonOutboxEnvelope>(_smallEnvelopeJson!)!;
	}

	/// <summary>
	/// Benchmark: MessagePack serialize (comparison).
	/// </summary>
	[Benchmark(Description = "MessagePack Serialize (comparison)")]
	public byte[] SerializeMessagePackComparison()
	{
		return MessagePackSerializer.Serialize(_messagePackEnvelope);
	}

	/// <summary>
	/// Benchmark: MessagePack deserialize (comparison).
	/// </summary>
	[Benchmark(Description = "MessagePack Deserialize (comparison)")]
	public MemoryPackMessagePackOutboxEnvelope DeserializeMessagePackComparison()
	{
		return MessagePackSerializer.Deserialize<MemoryPackMessagePackOutboxEnvelope>(_smallEnvelopeMessagePack);
	}

	#endregion Comparison Benchmarks (vs System.Text.Json and MessagePack)

	#region ReadOnlySequence Deserialization (Pipeline Support)

	/// <summary>
	/// Benchmark: Deserialize from ReadOnlySequence (pipeline support).
	/// </summary>
	[Benchmark(Description = "MemoryPack Deserialize from ReadOnlySequence")]
	public OutboxEnvelope DeserializeFromReadOnlySequence()
	{
		var sequence = new ReadOnlySequence<byte>(_smallEnvelopeSerialized);
		return _serializer.Deserialize<OutboxEnvelope>(sequence);
	}

	#endregion ReadOnlySequence Deserialization (Pipeline Support)
}

#pragma warning disable SA1402 // File may only contain a single type

/// <summary>
/// JSON-serializable envelope for comparison benchmarks.
/// </summary>
public sealed class MemoryPackJsonOutboxEnvelope
{
	public Guid MessageId { get; set; }

	public string MessageType { get; set; } = string.Empty;

	public byte[] Payload { get; set; } = [];

	public DateTimeOffset CreatedAt { get; set; }

	public IDictionary<string, string>? Headers { get; set; }

	public string? CorrelationId { get; set; }

	public string? CausationId { get; set; }

	public int SchemaVersion { get; set; }
}

/// <summary>
/// MessagePack-serializable envelope for comparison benchmarks.
/// </summary>
[MessagePackObject]
public sealed class MemoryPackMessagePackOutboxEnvelope
{
	[Key(0)]
	public Guid MessageId { get; set; }

	[Key(1)]
	public string MessageType { get; set; } = string.Empty;

	[Key(2)]
	public byte[] Payload { get; set; } = [];

	[Key(3)]
	public DateTimeOffset CreatedAt { get; set; }

	[Key(4)]
	public IDictionary<string, string>? Headers { get; set; }

	[Key(5)]
	public string? CorrelationId { get; set; }

	[Key(6)]
	public string? CausationId { get; set; }

	[Key(7)]
	public int SchemaVersion { get; set; }
}

#pragma warning restore SA1402 // File may only contain a single type
