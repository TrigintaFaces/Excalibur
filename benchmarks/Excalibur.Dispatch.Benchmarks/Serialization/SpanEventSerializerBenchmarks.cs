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
/// Benchmarks comparing ZeroAlloc SpanEventSerializer vs JsonEventSerializer.
/// Measures serialization performance and allocation behavior for event sourcing scenarios.
/// </summary>
/// <remarks>
/// Sprint 409 - ZeroAlloc Serializer Benchmarks (T409.1-T409.4).
/// bd-4zwzp: Performance Benchmarks for ZeroAlloc Serialization.
///
/// Performance Targets (from Sprint 408 design):
/// - Allocation per event: ~2-5 KB (JSON) → 0 KB (ZeroAlloc with pooled buffers)
/// - Serialization time: 2-5x faster than JSON
/// - Deserialization time: 2-5x faster than JSON
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class SpanEventSerializerBenchmarks
{
	private JsonEventSerializer _jsonSerializer = null!;
	private SpanEventSerializer _spanSerializer = null!;
	private IPluggableSerializer _memoryPackSerializer = null!;

	private TestDomainEvent _smallEvent = null!;
	private TestDomainEvent _largeEvent = null!;

	private byte[] _smallEventJsonBytes = null!;
	private byte[] _largeEventJsonBytes = null!;
	private byte[] _smallEventSpanBytes = null!;
	private byte[] _largeEventSpanBytes = null!;

	// Pre-allocated buffer for zero-alloc benchmarks
	private byte[] _pooledBuffer = null!;

	/// <summary>
	/// Initialize serializers and test events before benchmarks.
	/// </summary>
	[GlobalSetup]
	public void GlobalSetup()
	{
		// Create JSON serializer (baseline)
		_jsonSerializer = new JsonEventSerializer();

		// Create SpanEventSerializer with MemoryPack
		_memoryPackSerializer = new MemoryPackPluggableSerializer();
		_spanSerializer = new SpanEventSerializer(_memoryPackSerializer);

		// Create test events
		_smallEvent = CreateSmallEvent();
		_largeEvent = CreateLargeEvent();

		// Pre-serialize for deserialization benchmarks
		_smallEventJsonBytes = _jsonSerializer.SerializeEvent(_smallEvent);
		_largeEventJsonBytes = _jsonSerializer.SerializeEvent(_largeEvent);
		_smallEventSpanBytes = _spanSerializer.SerializeEvent(_smallEvent);
		_largeEventSpanBytes = _spanSerializer.SerializeEvent(_largeEvent);

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

	#region T409.2: JSON vs ZeroAlloc Comparison Benchmarks

	/// <summary>
	/// Benchmark: Serialize small event with JSON (baseline).
	/// </summary>
	[Benchmark(Baseline = true, Description = "JSON Serialize Small")]
	public byte[] JsonSerializeSmall()
	{
		return _jsonSerializer.SerializeEvent(_smallEvent);
	}

	/// <summary>
	/// Benchmark: Serialize small event with ZeroAlloc (byte[] API).
	/// </summary>
	[Benchmark(Description = "ZeroAlloc Serialize Small (byte[])")]
	public byte[] ZeroAllocSerializeSmallByteArray()
	{
		return _spanSerializer.SerializeEvent(_smallEvent);
	}

	/// <summary>
	/// Benchmark: Serialize large event with JSON (baseline).
	/// </summary>
	[Benchmark(Description = "JSON Serialize Large")]
	public byte[] JsonSerializeLarge()
	{
		return _jsonSerializer.SerializeEvent(_largeEvent);
	}

	/// <summary>
	/// Benchmark: Serialize large event with ZeroAlloc (byte[] API).
	/// </summary>
	[Benchmark(Description = "ZeroAlloc Serialize Large (byte[])")]
	public byte[] ZeroAllocSerializeLargeByteArray()
	{
		return _spanSerializer.SerializeEvent(_largeEvent);
	}

	/// <summary>
	/// Benchmark: Deserialize small event with JSON (baseline).
	/// </summary>
	[Benchmark(Description = "JSON Deserialize Small")]
	public IDomainEvent JsonDeserializeSmall()
	{
		return _jsonSerializer.DeserializeEvent(_smallEventJsonBytes, typeof(TestDomainEvent));
	}

	/// <summary>
	/// Benchmark: Deserialize small event with ZeroAlloc (Span API).
	/// </summary>
	[Benchmark(Description = "ZeroAlloc Deserialize Small (Span)")]
	public IDomainEvent ZeroAllocDeserializeSmallSpan()
	{
		return _spanSerializer.DeserializeEvent(_smallEventSpanBytes.AsSpan(), typeof(TestDomainEvent));
	}

	/// <summary>
	/// Benchmark: Deserialize large event with JSON (baseline).
	/// </summary>
	[Benchmark(Description = "JSON Deserialize Large")]
	public IDomainEvent JsonDeserializeLarge()
	{
		return _jsonSerializer.DeserializeEvent(_largeEventJsonBytes, typeof(TestDomainEvent));
	}

	/// <summary>
	/// Benchmark: Deserialize large event with ZeroAlloc (Span API).
	/// </summary>
	[Benchmark(Description = "ZeroAlloc Deserialize Large (Span)")]
	public IDomainEvent ZeroAllocDeserializeLargeSpan()
	{
		return _spanSerializer.DeserializeEvent(_largeEventSpanBytes.AsSpan(), typeof(TestDomainEvent));
	}

	#endregion

	#region T409.3: Buffer Pooling Allocation Benchmarks

	/// <summary>
	/// Benchmark: Serialize with pooled buffer (zero allocation).
	/// Demonstrates the zero-alloc pattern with ArrayPool.
	/// </summary>
	[Benchmark(Description = "ZeroAlloc Serialize Small (Pooled Buffer)")]
	public int ZeroAllocSerializeSmallPooled()
	{
		return _spanSerializer.SerializeEvent(_smallEvent, _pooledBuffer);
	}

	/// <summary>
	/// Benchmark: Serialize large event with pooled buffer (zero allocation).
	/// </summary>
	[Benchmark(Description = "ZeroAlloc Serialize Large (Pooled Buffer)")]
	public int ZeroAllocSerializeLargePooled()
	{
		return _spanSerializer.SerializeEvent(_largeEvent, _pooledBuffer);
	}

	/// <summary>
	/// Benchmark: Full pooled workflow - rent, serialize, return.
	/// Demonstrates realistic zero-alloc usage pattern.
	/// </summary>
	[Benchmark(Description = "ZeroAlloc Full Pooled Workflow")]
	public int ZeroAllocFullPooledWorkflow()
	{
		var size = _spanSerializer.GetEventSize(_smallEvent);
		var buffer = ArrayPool<byte>.Shared.Rent(size);
		try
		{
			return _spanSerializer.SerializeEvent(_smallEvent, buffer);
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	#endregion

	#region T409.4: Event Sourcing Scenario Benchmarks

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
			var evt = _jsonSerializer.DeserializeEvent(_smallEventJsonBytes, typeof(TestDomainEvent));
			count += evt.Version > 0 ? 1 : 0;
		}

		return count;
	}

	/// <summary>
	/// Benchmark: Event replay scenario with ZeroAlloc (100 events).
	/// Simulates replaying aggregate from event store with zero-alloc deserialization.
	/// </summary>
	[Benchmark(Description = "ZeroAlloc Event Replay (100 events)")]
	public int ZeroAllocEventReplay100()
	{
		var count = 0;
		for (var i = 0; i < 100; i++)
		{
			var evt = _spanSerializer.DeserializeEvent(_smallEventSpanBytes.AsSpan(), typeof(TestDomainEvent));
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
			var bytes = _jsonSerializer.SerializeEvent(_smallEvent);
			totalBytes += bytes.Length;
		}

		return totalBytes;
	}

	/// <summary>
	/// Benchmark: Event append scenario with ZeroAlloc (100 events).
	/// Simulates appending events with zero-alloc serialization using pooled buffer.
	/// </summary>
	[Benchmark(Description = "ZeroAlloc Event Append (100 events)")]
	public int ZeroAllocEventAppend100()
	{
		var totalBytes = 0;
		for (var i = 0; i < 100; i++)
		{
			var written = _spanSerializer.SerializeEvent(_smallEvent, _pooledBuffer);
			totalBytes += written;
		}

		return totalBytes;
	}

	/// <summary>
	/// Benchmark: Round-trip JSON (serialize + deserialize).
	/// </summary>
	[Benchmark(Description = "JSON Round-Trip Small")]
	public IDomainEvent JsonRoundTripSmall()
	{
		var bytes = _jsonSerializer.SerializeEvent(_smallEvent);
		return _jsonSerializer.DeserializeEvent(bytes, typeof(TestDomainEvent));
	}

	/// <summary>
	/// Benchmark: Round-trip ZeroAlloc (serialize + deserialize) with pooled buffer.
	/// </summary>
	[Benchmark(Description = "ZeroAlloc Round-Trip Small (Pooled)")]
	public IDomainEvent ZeroAllocRoundTripSmallPooled()
	{
		var written = _spanSerializer.SerializeEvent(_smallEvent, _pooledBuffer);
		return _spanSerializer.DeserializeEvent(_pooledBuffer.AsSpan(0, written), typeof(TestDomainEvent));
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
