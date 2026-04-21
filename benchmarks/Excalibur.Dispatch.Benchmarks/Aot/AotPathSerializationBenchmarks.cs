// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Benchmarks.Aot;

/// <summary>
/// AOT vs JIT JSON serialization benchmarks.
/// </summary>
/// <remarks>
/// <para>
/// Compares source-generated <see cref="JsonSerializerContext"/> (AOT-safe, zero reflection)
/// against reflection-based <see cref="SystemTextJsonSerializer"/> (JIT path).
/// </para>
/// <para>
/// Phase D1 requirement R-D1: AOT-specific benchmarks for serialization.
/// Target: AOT path serialization throughput >= 95% of JIT path.
/// </para>
/// </remarks>
[BenchmarkCategory("AOT")]
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class AotPathSerializationBenchmarks
{
    private AotJsonSerializer _aotSerializer = null!;
    private SystemTextJsonSerializer _jitSerializer = null!;
    private BenchmarkOrderEvent _smallMessage = null!;
    private BenchmarkBatchEvent _largeMessage = null!;
    private byte[] _smallMessageAotBytes = null!;
    private byte[] _smallMessageJitBytes = null!;
    private byte[] _largeMessageAotBytes = null!;
    private byte[] _largeMessageJitBytes = null!;

    [GlobalSetup]
    public void Setup()
    {
        // AOT path: source-generated context
        _aotSerializer = new AotJsonSerializer(AotBenchmarkJsonContext.Default);

        // JIT path: reflection-based serializer
        _jitSerializer = new SystemTextJsonSerializer(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        });

        // Small message (~200 bytes)
        _smallMessage = new BenchmarkOrderEvent
        {
            OrderId = Guid.NewGuid().ToString(),
            CustomerId = "customer-bench-001",
            Amount = 149.99m,
            Currency = "USD",
            OccurredAt = DateTimeOffset.UtcNow,
        };

        // Large message (~10KB)
        _largeMessage = new BenchmarkBatchEvent
        {
            BatchId = Guid.NewGuid().ToString(),
            Items = Enumerable.Range(0, 50).Select(i => new BenchmarkLineItem
            {
                ProductId = $"PROD-{i:D5}",
                ProductName = $"Product {i} with descriptive name for benchmark testing",
                Quantity = i + 1,
                UnitPrice = 19.99m + i,
            }).ToList(),
            Metadata = Enumerable.Range(0, 20).ToDictionary(
                i => $"meta-key-{i}",
                i => $"meta-value-{i}-with-additional-benchmark-data"),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        // Pre-serialize for deserialization benchmarks
        _smallMessageAotBytes = _aotSerializer.Serialize(_smallMessage);
        _largeMessageAotBytes = _aotSerializer.Serialize(_largeMessage);

        var jitBuffer = new ArrayBufferWriter<byte>(256);
        _jitSerializer.Serialize(_smallMessage, jitBuffer);
        _smallMessageJitBytes = jitBuffer.WrittenMemory.ToArray();

        jitBuffer = new ArrayBufferWriter<byte>(16384);
        _jitSerializer.Serialize(_largeMessage, jitBuffer);
        _largeMessageJitBytes = jitBuffer.WrittenMemory.ToArray();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _aotSerializer.Dispose();
    }

    // ========================================================================
    // Small Message Serialization
    // ========================================================================

    /// <summary>
    /// JIT path: Serialize small message with reflection (baseline).
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Serialize", "Small")]
    public int JitPath_SerializeSmall()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(256);
        _jitSerializer.Serialize(_smallMessage, bufferWriter);
        return bufferWriter.WrittenCount;
    }

    /// <summary>
    /// AOT path: Serialize small message with source-generated context.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Serialize", "Small")]
    public int AotPath_SerializeSmall()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(256);
        _aotSerializer.Serialize(_smallMessage, bufferWriter);
        return bufferWriter.WrittenCount;
    }

    // ========================================================================
    // Small Message Deserialization
    // ========================================================================

    /// <summary>
    /// JIT path: Deserialize small message with reflection.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Deserialize", "Small")]
    public BenchmarkOrderEvent JitPath_DeserializeSmall()
    {
        return _jitSerializer.Deserialize<BenchmarkOrderEvent>(_smallMessageJitBytes.AsSpan());
    }

    /// <summary>
    /// AOT path: Deserialize small message with source-generated context.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Deserialize", "Small")]
    public BenchmarkOrderEvent AotPath_DeserializeSmall()
    {
        return _aotSerializer.Deserialize<BenchmarkOrderEvent>(_smallMessageAotBytes.AsSpan());
    }

    // ========================================================================
    // Large Message Serialization
    // ========================================================================

    /// <summary>
    /// JIT path: Serialize large message with reflection.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Serialize", "Large")]
    public int JitPath_SerializeLarge()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(16384);
        _jitSerializer.Serialize(_largeMessage, bufferWriter);
        return bufferWriter.WrittenCount;
    }

    /// <summary>
    /// AOT path: Serialize large message with source-generated context.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Serialize", "Large")]
    public int AotPath_SerializeLarge()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(16384);
        _aotSerializer.Serialize(_largeMessage, bufferWriter);
        return bufferWriter.WrittenCount;
    }

    // ========================================================================
    // Large Message Deserialization
    // ========================================================================

    /// <summary>
    /// JIT path: Deserialize large message with reflection.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Deserialize", "Large")]
    public BenchmarkBatchEvent JitPath_DeserializeLarge()
    {
        return _jitSerializer.Deserialize<BenchmarkBatchEvent>(_largeMessageJitBytes.AsSpan());
    }

    /// <summary>
    /// AOT path: Deserialize large message with source-generated context.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Deserialize", "Large")]
    public BenchmarkBatchEvent AotPath_DeserializeLarge()
    {
        return _aotSerializer.Deserialize<BenchmarkBatchEvent>(_largeMessageAotBytes.AsSpan());
    }

    // ========================================================================
    // Round-Trip
    // ========================================================================

    /// <summary>
    /// JIT path: Round-trip (serialize + deserialize) small message.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("RoundTrip", "Small")]
    public BenchmarkOrderEvent JitPath_RoundTripSmall()
    {
        var bufferWriter = new ArrayBufferWriter<byte>(256);
        _jitSerializer.Serialize(_smallMessage, bufferWriter);
        return _jitSerializer.Deserialize<BenchmarkOrderEvent>(bufferWriter.WrittenSpan);
    }

    /// <summary>
    /// AOT path: Round-trip (serialize + deserialize) small message.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("RoundTrip", "Small")]
    public BenchmarkOrderEvent AotPath_RoundTripSmall()
    {
        var bytes = _aotSerializer.Serialize(_smallMessage);
        return _aotSerializer.Deserialize<BenchmarkOrderEvent>(bytes.AsSpan());
    }

    // ========================================================================
    // Batch Serialization (throughput)
    // ========================================================================

    /// <summary>
    /// JIT path: 100 sequential serialize operations.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Throughput")]
    public int JitPath_SerializeBatch100()
    {
        var total = 0;
        var bufferWriter = new ArrayBufferWriter<byte>(256);
        for (var i = 0; i < 100; i++)
        {
            bufferWriter.Clear();
            _jitSerializer.Serialize(_smallMessage, bufferWriter);
            total += bufferWriter.WrittenCount;
        }

        return total;
    }

    /// <summary>
    /// AOT path: 100 sequential serialize operations.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Throughput")]
    public int AotPath_SerializeBatch100()
    {
        var total = 0;
        var bufferWriter = new ArrayBufferWriter<byte>(256);
        for (var i = 0; i < 100; i++)
        {
            bufferWriter.Clear();
            _aotSerializer.Serialize(_smallMessage, bufferWriter);
            total += bufferWriter.WrittenCount;
        }

        return total;
    }
}

// ========================================================================
// Benchmark message types with source-generated JSON context
// ========================================================================

#pragma warning disable SA1402 // File may only contain a single type

/// <summary>
/// Source-generated JSON context for AOT benchmark types.
/// </summary>
[JsonSerializable(typeof(BenchmarkOrderEvent))]
[JsonSerializable(typeof(BenchmarkBatchEvent))]
[JsonSerializable(typeof(BenchmarkLineItem))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false)]
internal partial class AotBenchmarkJsonContext : JsonSerializerContext;

/// <summary>
/// Small benchmark event (~200 bytes serialized).
/// </summary>
public sealed class BenchmarkOrderEvent
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
}

/// <summary>
/// Large benchmark event (~10KB serialized) with nested collections.
/// </summary>
public sealed class BenchmarkBatchEvent
{
    public string BatchId { get; set; } = string.Empty;
    public List<BenchmarkLineItem> Items { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// Line item for batch event benchmarks.
/// </summary>
public sealed class BenchmarkLineItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

#pragma warning restore SA1402 // File may only contain a single type
