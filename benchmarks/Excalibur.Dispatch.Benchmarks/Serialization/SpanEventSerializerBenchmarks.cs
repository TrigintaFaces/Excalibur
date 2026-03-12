// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Benchmarks.Patterns;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Serialization.MemoryPack;

namespace Excalibur.Dispatch.Benchmarks.Serialization;

/// <summary>
/// Benchmarks comparing SystemTextJsonSerializer vs MemoryPackSerializer for event serialization.
/// Measures serialization performance and allocation behavior for event sourcing scenarios.
/// </summary>
/// <remarks>
/// Sprint 409 - ZeroAlloc Serializer Benchmarks (T409.1-T409.4).
/// bd-4zwzp: Performance Benchmarks for ZeroAlloc Serialization.
///
/// Sprint 586 - Updated to use consolidated serializer classes:
/// - SystemTextJsonSerializer replaces JsonEventSerializer (ISerializer interface).
/// - MemoryPackSerializer replaces MemoryPackPluggableSerializer (ISerializer interface).
///
/// Performance Targets (from Sprint 408 design):
/// - Allocation per event: ~2-5 KB (JSON) vs 0 KB (MemoryPack with pooled buffers)
/// - Serialization time: 2-5x faster than JSON
/// - Deserialization time: 2-5x faster than JSON
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class SpanEventSerializerBenchmarks
{
	private SystemTextJsonSerializer _jsonSerializer = null!;
	private MemoryPackSerializer _memoryPackSerializer = null!;

	private TestDomainEvent _smallEvent = null!;
	private TestDomainEvent _largeEvent = null!;

	private byte[] _smallEventJsonBytes = null!;
	private byte[] _largeEventJsonBytes = null!;
	private byte[] _smallEventMemPackBytes = null!;
	private byte[] _largeEventMemPackBytes = null!;

	// Pre-allocated buffer for zero-alloc benchmarks
	private byte[] _pooledBuffer = null!;

	/// <summary>
	/// Initialize serializers and test events before benchmarks.
	/// </summary>
	[GlobalSetup]
	public void GlobalSetup()
	{
		// Create JSON serializer (baseline)
		_jsonSerializer = new SystemTextJsonSerializer();

		// Create MemoryPack serializer (high-performance path)
		_memoryPackSerializer = new MemoryPackSerializer();

		// Create test events
		_smallEvent = CreateSmallEvent();
		_largeEvent = CreateLargeEvent();

		// Pre-serialize for deserialization benchmarks using SerializeToBytes extension
		_smallEventJsonBytes = _jsonSerializer.SerializeToBytes(_smallEvent);
		_largeEventJsonBytes = _jsonSerializer.SerializeToBytes(_largeEvent);
		_smallEventMemPackBytes = _memoryPackSerializer.SerializeToBytes(_smallEvent);
		_largeEventMemPackBytes = _memoryPackSerializer.SerializeToBytes(_largeEvent);

		// Pre-allocate buffer for pooled benchmarks (large enough for any event)
		_pooledBuffer = ArrayPool<byte>.Shared.Rent(64 * 1024); // 64KB
	}

	/// <summary>
	/// Cleanup after benchmarks.
	/// </summary>
	[GlobalCleanup]
	public void GlobalCleanup()
	{
		ArrayPool<byte>.Shared.Return(_pooledBuffer);
	}

	#region JSON vs MemoryPack Comparison Benchmarks

	/// <summary>
	/// Benchmark: Serialize small event with JSON (baseline) using IBufferWriter.
	/// </summary>
	[Benchmark(Baseline = true, Description = "JSON Serialize Small (IBufferWriter)")]
	public int JsonSerializeSmall()
	{
		var bufferWriter = new ArrayBufferWriter<byte>(256);
		_jsonSerializer.Serialize(_smallEvent, bufferWriter);
		return bufferWriter.WrittenCount;
	}

	/// <summary>
	/// Benchmark: Serialize small event with MemoryPack (byte[] API).
	/// </summary>
	[Benchmark(Description = "MemoryPack Serialize Small (byte[])")]
	public byte[] MemPackSerializeSmallByteArray()
	{
		return _memoryPackSerializer.SerializeToBytes(_smallEvent);
	}

	/// <summary>
	/// Benchmark: Serialize large event with JSON (baseline) using IBufferWriter.
	/// </summary>
	[Benchmark(Description = "JSON Serialize Large (IBufferWriter)")]
	public int JsonSerializeLarge()
	{
		var bufferWriter = new ArrayBufferWriter<byte>(16384);
		_jsonSerializer.Serialize(_largeEvent, bufferWriter);
		return bufferWriter.WrittenCount;
	}

	/// <summary>
	/// Benchmark: Serialize large event with MemoryPack (byte[] API).
	/// </summary>
	[Benchmark(Description = "MemoryPack Serialize Large (byte[])")]
	public byte[] MemPackSerializeLargeByteArray()
	{
		return _memoryPackSerializer.SerializeToBytes(_largeEvent);
	}

	/// <summary>
	/// Benchmark: Deserialize small event with JSON (baseline).
	/// </summary>
	[Benchmark(Description = "JSON Deserialize Small")]
	public IDomainEvent JsonDeserializeSmall()
	{
		return _jsonSerializer.Deserialize<TestDomainEvent>(_smallEventJsonBytes.AsSpan());
	}

	/// <summary>
	/// Benchmark: Deserialize small event with MemoryPack (Span API).
	/// </summary>
	[Benchmark(Description = "MemoryPack Deserialize Small (Span)")]
	public IDomainEvent MemPackDeserializeSmallSpan()
	{
		return _memoryPackSerializer.Deserialize<TestDomainEvent>(_smallEventMemPackBytes.AsSpan());
	}

	/// <summary>
	/// Benchmark: Deserialize large event with JSON (baseline).
	/// </summary>
	[Benchmark(Description = "JSON Deserialize Large")]
	public IDomainEvent JsonDeserializeLarge()
	{
		return _jsonSerializer.Deserialize<TestDomainEvent>(_largeEventJsonBytes.AsSpan());
	}

	/// <summary>
	/// Benchmark: Deserialize large event with MemoryPack (Span API).
	/// </summary>
	[Benchmark(Description = "MemoryPack Deserialize Large (Span)")]
	public IDomainEvent MemPackDeserializeLargeSpan()
	{
		return _memoryPackSerializer.Deserialize<TestDomainEvent>(_largeEventMemPackBytes.AsSpan());
	}

	#endregion

	#region Buffer Pooling Allocation Benchmarks

	/// <summary>
	/// Benchmark: Serialize with IBufferWriter (zero-alloc primary path) using MemoryPack.
	/// </summary>
	[Benchmark(Description = "MemoryPack Serialize Small (IBufferWriter)")]
	public int MemPackSerializeSmallBufferWriter()
	{
		var bufferWriter = new ArrayBufferWriter<byte>(256);
		_memoryPackSerializer.Serialize(_smallEvent, bufferWriter);
		return bufferWriter.WrittenCount;
	}

	/// <summary>
	/// Benchmark: Serialize large event with IBufferWriter (zero-alloc primary path) using MemoryPack.
	/// </summary>
	[Benchmark(Description = "MemoryPack Serialize Large (IBufferWriter)")]
	public int MemPackSerializeLargeBufferWriter()
	{
		var bufferWriter = new ArrayBufferWriter<byte>(16384);
		_memoryPackSerializer.Serialize(_largeEvent, bufferWriter);
		return bufferWriter.WrittenCount;
	}

	#endregion

	#region Event Sourcing Scenario Benchmarks

	/// <summary>
	/// Benchmark: Event replay scenario with JSON (100 events).
	/// Simulates replaying aggregate from event store.
	/// </summary>
	[Benchmark(Description = "JSON Event Replay (100 events)")]
	public int JsonEventReplay100()
	{
		var count = 0;
		for (var i = 0; i < 100; i++)
		{
			var evt = _jsonSerializer.Deserialize<TestDomainEvent>(_smallEventJsonBytes.AsSpan());
			count += evt.Version > 0 ? 1 : 0;
		}

		return count;
	}

	/// <summary>
	/// Benchmark: Event replay scenario with MemoryPack (100 events).
	/// Simulates replaying aggregate from event store with zero-alloc deserialization.
	/// </summary>
	[Benchmark(Description = "MemoryPack Event Replay (100 events)")]
	public int MemPackEventReplay100()
	{
		var count = 0;
		var pluggable = (ISerializer)_memoryPackSerializer;
		for (var i = 0; i < 100; i++)
		{
			var evt = pluggable.Deserialize<TestDomainEvent>(_smallEventMemPackBytes.AsSpan());
			count += evt.Version > 0 ? 1 : 0;
		}

		return count;
	}

	/// <summary>
	/// Benchmark: Event append scenario with JSON (100 events).
	/// Simulates appending events to event store.
	/// </summary>
	[Benchmark(Description = "JSON Event Append (100 events)")]
	public int JsonEventAppend100()
	{
		var totalBytes = 0;
		for (var i = 0; i < 100; i++)
		{
			var bytes = _jsonSerializer.SerializeToBytes(_smallEvent);
			totalBytes += bytes.Length;
		}

		return totalBytes;
	}

	/// <summary>
	/// Benchmark: Event append scenario with MemoryPack (100 events).
	/// Simulates appending events with serialization using byte[] API.
	/// </summary>
	[Benchmark(Description = "MemoryPack Event Append (100 events)")]
	public int MemPackEventAppend100()
	{
		var totalBytes = 0;
		var pluggable = (ISerializer)_memoryPackSerializer;
		for (var i = 0; i < 100; i++)
		{
			var bytes = pluggable.SerializeToBytes(_smallEvent);
			totalBytes += bytes.Length;
		}

		return totalBytes;
	}

	/// <summary>
	/// Benchmark: Round-trip JSON (serialize + deserialize).
	/// </summary>
	[Benchmark(Description = "JSON Round-Trip Small")]
	public IDomainEvent JsonRoundTripSmall()
	{
		var bytes = _jsonSerializer.SerializeToBytes(_smallEvent);
		return _jsonSerializer.Deserialize<TestDomainEvent>(bytes.AsSpan());
	}

	/// <summary>
	/// Benchmark: Round-trip MemoryPack (serialize + deserialize).
	/// </summary>
	[Benchmark(Description = "MemoryPack Round-Trip Small")]
	public IDomainEvent MemPackRoundTripSmall()
	{
		var pluggable = (ISerializer)_memoryPackSerializer;
		var bytes = pluggable.SerializeToBytes(_smallEvent);
		return pluggable.Deserialize<TestDomainEvent>(bytes.AsSpan());
	}

	#endregion

	#region Test Event Factories

	private static TestDomainEvent CreateSmallEvent()
	{
		return new TestDomainEvent
		{
			EventId = Guid.NewGuid().ToString(),
			AggregateId = Guid.NewGuid().ToString(),
			Version = 1,
			OccurredAt = DateTimeOffset.UtcNow,
			EventType = "OrderCreated",
			Data = "Small event payload",
			Metadata = new Dictionary<string, object>
			{
				["CorrelationId"] = Guid.NewGuid().ToString(),
			},
		};
	}

	private static TestDomainEvent CreateLargeEvent()
	{
		// Create ~10KB event with larger data payload
		var largeData = new string('X', 8 * 1024); // 8KB of data
		return new TestDomainEvent
		{
			EventId = Guid.NewGuid().ToString(),
			AggregateId = Guid.NewGuid().ToString(),
			Version = 100,
			OccurredAt = DateTimeOffset.UtcNow,
			EventType = "LargeOrderProcessed",
			Data = largeData,
			Metadata = new Dictionary<string, object>
			{
				["CorrelationId"] = Guid.NewGuid().ToString(),
				["CausationId"] = Guid.NewGuid().ToString(),
				["UserId"] = "user-12345",
				["TenantId"] = "tenant-67890",
				["TraceId"] = Guid.NewGuid().ToString(),
			},
		};
	}

	#endregion
}
