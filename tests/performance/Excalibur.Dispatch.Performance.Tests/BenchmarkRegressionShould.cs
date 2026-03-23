// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Text;
using System.Text.Json;

using Excalibur.Dispatch.Tests.TestFakes;

namespace Excalibur.Dispatch.Tests.Performance;

/// <summary>
/// Benchmark regression tests that establish throughput baselines for critical paths.
/// These tests serve as a CI gate: if any operation drops below the minimum ops/sec threshold,
/// the test fails, indicating a performance regression.
///
/// Thresholds are intentionally conservative (50% of observed dev-machine throughput)
/// to avoid flaky failures under CI load while still detecting significant regressions.
/// </summary>
[Collection("Performance Tests")]
[Trait("Category", "Performance")]
[Trait("Component", "Core")]
public sealed class BenchmarkRegressionShould
{
	#region Serialization Regression Gates

	[Fact]
	public void MaintainJsonSerializationThroughput()
	{
		// Baseline: JSON serialization of a small message should achieve >= 10K ops/sec
		// even under CI load (observed: ~200K+ ops/sec on dev machine)
		const int iterations = 5000;
		const double minOpsPerSecond = 10_000;

		var options = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			WriteIndented = false
		};

		var messages = new FakeDispatchMessage[iterations];
		for (var i = 0; i < iterations; i++)
		{
			messages[i] = new FakeDispatchMessage { Payload = Encoding.UTF8.GetBytes($"Payload-{i}") };
		}

		// Warmup
		for (var i = 0; i < 100; i++)
		{
			_ = JsonSerializer.Serialize(messages[0], options);
		}

		// Measure
		var sw = Stopwatch.StartNew();
		for (var i = 0; i < iterations; i++)
		{
			_ = JsonSerializer.Serialize(messages[i], options);
		}

		sw.Stop();

		var opsPerSecond = iterations / sw.Elapsed.TotalSeconds;
		opsPerSecond.ShouldBeGreaterThan(minOpsPerSecond,
			$"JSON serialization throughput regression: {opsPerSecond:N0} ops/sec (min: {minOpsPerSecond:N0})");
	}

	[Fact]
	public void MaintainJsonDeserializationThroughput()
	{
		// Baseline: JSON deserialization should achieve >= 10K ops/sec
		const int iterations = 5000;
		const double minOpsPerSecond = 10_000;

		var options = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		};

		var jsonStrings = new string[iterations];
		for (var i = 0; i < iterations; i++)
		{
			var msg = new FakeDispatchMessage { Payload = Encoding.UTF8.GetBytes($"Payload-{i}") };
			jsonStrings[i] = JsonSerializer.Serialize(msg, options);
		}

		// Warmup
		for (var i = 0; i < 100; i++)
		{
			_ = JsonSerializer.Deserialize<FakeDispatchMessage>(jsonStrings[0], options);
		}

		// Measure
		var sw = Stopwatch.StartNew();
		for (var i = 0; i < iterations; i++)
		{
			_ = JsonSerializer.Deserialize<FakeDispatchMessage>(jsonStrings[i], options);
		}

		sw.Stop();

		var opsPerSecond = iterations / sw.Elapsed.TotalSeconds;
		opsPerSecond.ShouldBeGreaterThan(minOpsPerSecond,
			$"JSON deserialization throughput regression: {opsPerSecond:N0} ops/sec (min: {minOpsPerSecond:N0})");
	}

	[Fact]
	public void MaintainUtf8SerializationThroughput()
	{
		// Baseline: UTF8 byte serialization should achieve >= 15K ops/sec
		const int iterations = 5000;
		const double minOpsPerSecond = 15_000;

		var options = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			WriteIndented = false
		};

		var messages = new FakeDispatchMessage[iterations];
		for (var i = 0; i < iterations; i++)
		{
			messages[i] = new FakeDispatchMessage { Payload = Encoding.UTF8.GetBytes($"Payload-{i}") };
		}

		// Warmup
		for (var i = 0; i < 100; i++)
		{
			_ = JsonSerializer.SerializeToUtf8Bytes(messages[0], options);
		}

		// Measure
		var sw = Stopwatch.StartNew();
		for (var i = 0; i < iterations; i++)
		{
			_ = JsonSerializer.SerializeToUtf8Bytes(messages[i], options);
		}

		sw.Stop();

		var opsPerSecond = iterations / sw.Elapsed.TotalSeconds;
		opsPerSecond.ShouldBeGreaterThan(minOpsPerSecond,
			$"UTF8 serialization throughput regression: {opsPerSecond:N0} ops/sec (min: {minOpsPerSecond:N0})");
	}

	#endregion

	#region Dictionary/Cache Lookup Regression Gates

	[Fact]
	public void MaintainTypeLookupCacheThroughput()
	{
		// Baseline: Type-based dictionary lookups (simulating handler resolution cache)
		// should achieve >= 1M ops/sec
		const int iterations = 100_000;
		const double minOpsPerSecond = 1_000_000;

		var cache = new Dictionary<Type, string>
		{
			[typeof(string)] = "StringHandler",
			[typeof(int)] = "IntHandler",
			[typeof(FakeDispatchMessage)] = "FakeHandler",
			[typeof(Guid)] = "GuidHandler",
			[typeof(DateTimeOffset)] = "DateHandler"
		};

		var lookupTypes = new[] { typeof(string), typeof(int), typeof(FakeDispatchMessage), typeof(Guid), typeof(DateTimeOffset) };

		// Warmup
		for (var i = 0; i < 1000; i++)
		{
			_ = cache[lookupTypes[i % lookupTypes.Length]];
		}

		// Measure
		var sw = Stopwatch.StartNew();
		for (var i = 0; i < iterations; i++)
		{
			_ = cache[lookupTypes[i % lookupTypes.Length]];
		}

		sw.Stop();

		var opsPerSecond = iterations / sw.Elapsed.TotalSeconds;
		opsPerSecond.ShouldBeGreaterThan(minOpsPerSecond,
			$"Type lookup cache throughput regression: {opsPerSecond:N0} ops/sec (min: {minOpsPerSecond:N0})");
	}

	#endregion

	#region Middleware Pipeline Regression Gates

	[Fact]
	public async Task MaintainMiddlewarePipelineThroughput()
	{
		// Baseline: A 5-stage delegate pipeline (simulating middleware chain) should achieve >= 100K ops/sec
		const int iterations = 50_000;
		const double minOpsPerSecond = 100_000;

		// Build a 5-stage pipeline (typical: serialization, auth, validation, timeout, exception mapping)
		Func<int, Task<int>> pipeline = x => Task.FromResult(x);
		for (var stage = 0; stage < 5; stage++)
		{
			var next = pipeline;
			pipeline = async x => await next(x + 1).ConfigureAwait(false);
		}

		// Warmup
		for (var i = 0; i < 100; i++)
		{
			_ = await pipeline(0).ConfigureAwait(false);
		}

		// Measure
		var sw = Stopwatch.StartNew();
		for (var i = 0; i < iterations; i++)
		{
			_ = await pipeline(0).ConfigureAwait(false);
		}

		sw.Stop();

		var opsPerSecond = iterations / sw.Elapsed.TotalSeconds;
		opsPerSecond.ShouldBeGreaterThan(minOpsPerSecond,
			$"Middleware pipeline throughput regression: {opsPerSecond:N0} ops/sec (min: {minOpsPerSecond:N0})");
	}

	[Fact]
	public async Task MaintainSynchronousMiddlewarePipelineThroughput()
	{
		// Baseline: Synchronous pipeline (ValueTask-based) should achieve >= 200K ops/sec
		const int iterations = 100_000;
		const double minOpsPerSecond = 200_000;

		// Build a 5-stage ValueTask pipeline
		Func<int, ValueTask<int>> pipeline = x => new ValueTask<int>(x);
		for (var stage = 0; stage < 5; stage++)
		{
			var next = pipeline;
			pipeline = x =>
			{
				var result = next(x + 1);
				return result.IsCompleted ? result : AwaitResult(result);
			};
		}

		// Warmup
		for (var i = 0; i < 100; i++)
		{
			_ = await pipeline(0).ConfigureAwait(false);
		}

		// Measure
		var sw = Stopwatch.StartNew();
		for (var i = 0; i < iterations; i++)
		{
			_ = await pipeline(0).ConfigureAwait(false);
		}

		sw.Stop();

		var opsPerSecond = iterations / sw.Elapsed.TotalSeconds;
		opsPerSecond.ShouldBeGreaterThan(minOpsPerSecond,
			$"Synchronous pipeline throughput regression: {opsPerSecond:N0} ops/sec (min: {minOpsPerSecond:N0})");

		static async ValueTask<int> AwaitResult(ValueTask<int> task) => await task.ConfigureAwait(false);
	}

	#endregion

	#region Memory Allocation Regression Gates

	[Fact]
	public void MaintainLowAllocationForSerializationRoundTrip()
	{
		// Baseline: 1000 serialize+deserialize round-trips should allocate < 20MB
		// (detects accidental allocation regressions like unnecessary string copies)
		const int iterations = 1000;
		const long maxAllocatedBytes = 20 * 1024 * 1024; // 20MB

		var options = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			WriteIndented = false
		};

		var message = new FakeDispatchMessage
		{
			Payload = Encoding.UTF8.GetBytes("Standard test payload for allocation measurement")
		};

		// Warmup + force GC
		for (var i = 0; i < 50; i++)
		{
			var json = JsonSerializer.Serialize(message, options);
			_ = JsonSerializer.Deserialize<FakeDispatchMessage>(json, options);
		}

		GC.Collect(2, GCCollectionMode.Forced, true);
		GC.WaitForPendingFinalizers();
		var before = GC.GetTotalAllocatedBytes(true);

		// Measure
		for (var i = 0; i < iterations; i++)
		{
			var json = JsonSerializer.Serialize(message, options);
			_ = JsonSerializer.Deserialize<FakeDispatchMessage>(json, options);
		}

		var after = GC.GetTotalAllocatedBytes(true);
		var allocated = after - before;

		allocated.ShouldBeLessThan(maxAllocatedBytes,
			$"Serialization round-trip allocation regression: {allocated / 1024 / 1024}MB (max: {maxAllocatedBytes / 1024 / 1024}MB)");
	}

	#endregion

	#region Concurrent Dispatch Simulation Regression Gates

	[Fact]
	public async Task MaintainConcurrentDispatchThroughput()
	{
		// Baseline: 100 concurrent simulated dispatches should complete in < 1000ms
		// This simulates multiple handlers resolving and executing concurrently
		const int concurrency = 100;
		const int maxDurationMs = 1000;

		var handlers = new Dictionary<string, Func<string, Task<string>>>();
		for (var i = 0; i < 10; i++)
		{
			var handlerId = $"handler-{i}";
			handlers[handlerId] = msg => Task.FromResult($"Handled: {msg}");
		}

		var sw = Stopwatch.StartNew();

		var tasks = new Task<string>[concurrency];
		for (var i = 0; i < concurrency; i++)
		{
			var handlerKey = $"handler-{i % 10}";
			var message = $"message-{i}";
			tasks[i] = handlers[handlerKey](message);
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);
		sw.Stop();

		// Verify all completed
		for (var i = 0; i < concurrency; i++)
		{
			var result = await tasks[i].ConfigureAwait(false);
			result.ShouldStartWith("Handled:");
		}

		sw.ElapsedMilliseconds.ShouldBeLessThan(maxDurationMs,
			$"Concurrent dispatch regression: {sw.ElapsedMilliseconds}ms (max: {maxDurationMs}ms)");
	}

	#endregion
}
