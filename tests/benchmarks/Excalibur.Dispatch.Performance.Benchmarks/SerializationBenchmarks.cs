// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;
using System.Text.Json;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Performance.Benchmarks;

/// <summary>
/// Benchmarks for message serialization performance.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class SerializationBenchmarks
{
	private TestMessage _smallMessage = null!;
	private TestMessage _mediumMessage = null!;
	private TestMessage _largeMessage = null!;
	private NestedMessage _nestedMessage = null!;

	private string _smallJson = null!;
	private string _mediumJson = null!;
	private string _largeJson = null!;
	private string _nestedJson = null!;

	private byte[] _smallUtf8 = null!;
	private byte[] _mediumUtf8 = null!;
	private byte[] _largeUtf8 = null!;

	private static readonly JsonSerializerOptions CachedOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
	};

	[GlobalSetup]
	public void Setup()
	{
		// Small message (~100 bytes)
		_smallMessage = new TestMessage
		{
			Id = Guid.NewGuid(),
			Type = "OrderCreated",
			Timestamp = DateTimeOffset.UtcNow,
			Data = "Small payload",
		};

		// Medium message (~1KB)
		_mediumMessage = new TestMessage
		{
			Id = Guid.NewGuid(),
			Type = "OrderCreated",
			Timestamp = DateTimeOffset.UtcNow,
			Data = new string('X', 900),
		};

		// Large message (~10KB)
		_largeMessage = new TestMessage
		{
			Id = Guid.NewGuid(),
			Type = "BulkOrderCreated",
			Timestamp = DateTimeOffset.UtcNow,
			Data = new string('Y', 9000),
		};

		// Nested message
		_nestedMessage = new NestedMessage
		{
			Id = Guid.NewGuid(),
			Items =
			[
				new NestedItem { Name = "Item1", Value = 100, Tags = ["tag1", "tag2"] },
				new NestedItem { Name = "Item2", Value = 200, Tags = ["tag3", "tag4"] },
				new NestedItem { Name = "Item3", Value = 300, Tags = ["tag5", "tag6"] },
			],
		};

		// Pre-serialize for deserialization benchmarks
		_smallJson = JsonSerializer.Serialize(_smallMessage, CachedOptions);
		_mediumJson = JsonSerializer.Serialize(_mediumMessage, CachedOptions);
		_largeJson = JsonSerializer.Serialize(_largeMessage, CachedOptions);
		_nestedJson = JsonSerializer.Serialize(_nestedMessage, CachedOptions);

		_smallUtf8 = Encoding.UTF8.GetBytes(_smallJson);
		_mediumUtf8 = Encoding.UTF8.GetBytes(_mediumJson);
		_largeUtf8 = Encoding.UTF8.GetBytes(_largeJson);
	}

	[Benchmark(Baseline = true)]
	public string Serialize_SmallMessage()
	{
		return JsonSerializer.Serialize(_smallMessage, CachedOptions);
	}

	[Benchmark]
	public string Serialize_MediumMessage()
	{
		return JsonSerializer.Serialize(_mediumMessage, CachedOptions);
	}

	[Benchmark]
	public string Serialize_LargeMessage()
	{
		return JsonSerializer.Serialize(_largeMessage, CachedOptions);
	}

	[Benchmark]
	public string Serialize_NestedMessage()
	{
		return JsonSerializer.Serialize(_nestedMessage, CachedOptions);
	}

	[Benchmark]
	public TestMessage? Deserialize_SmallMessage()
	{
		return JsonSerializer.Deserialize<TestMessage>(_smallJson, CachedOptions);
	}

	[Benchmark]
	public TestMessage? Deserialize_MediumMessage()
	{
		return JsonSerializer.Deserialize<TestMessage>(_mediumJson, CachedOptions);
	}

	[Benchmark]
	public TestMessage? Deserialize_LargeMessage()
	{
		return JsonSerializer.Deserialize<TestMessage>(_largeJson, CachedOptions);
	}

	[Benchmark]
	public NestedMessage? Deserialize_NestedMessage()
	{
		return JsonSerializer.Deserialize<NestedMessage>(_nestedJson, CachedOptions);
	}

	[Benchmark]
	public byte[] SerializeToUtf8_SmallMessage()
	{
		return JsonSerializer.SerializeToUtf8Bytes(_smallMessage, CachedOptions);
	}

	[Benchmark]
	public TestMessage? DeserializeFromUtf8_SmallMessage()
	{
		return JsonSerializer.Deserialize<TestMessage>(_smallUtf8, CachedOptions);
	}

	[Benchmark]
	public TestMessage? DeserializeFromUtf8_MediumMessage()
	{
		return JsonSerializer.Deserialize<TestMessage>(_mediumUtf8, CachedOptions);
	}

	[Benchmark]
	public TestMessage? DeserializeFromUtf8_LargeMessage()
	{
		return JsonSerializer.Deserialize<TestMessage>(_largeUtf8, CachedOptions);
	}

	public sealed class TestMessage : IDispatchMessage
	{
		public Guid Id { get; init; }
		public string Type { get; init; } = string.Empty;
		public DateTimeOffset Timestamp { get; init; }
		public string Data { get; init; } = string.Empty;
	}

	public sealed class NestedMessage : IDispatchMessage
	{
		public Guid Id { get; init; }
		public List<NestedItem> Items { get; init; } = [];
	}

	public sealed class NestedItem
	{
		public string Name { get; init; } = string.Empty;
		public int Value { get; init; }
		public List<string> Tags { get; init; } = [];
	}
}
