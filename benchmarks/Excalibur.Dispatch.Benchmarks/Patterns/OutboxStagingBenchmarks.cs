// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch;

using Excalibur.Outbox.InMemory;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Benchmarks.Patterns;

/// <summary>
/// Benchmarks for outbox message staging operations.
/// Measures performance of single, batch, and concurrent staging scenarios.
/// </summary>
/// <remarks>
/// Performance Targets (from testing-and-benchmarking-strategy-spec.md):
/// - Single message staging: &lt; 5ms (P50), &lt; 10ms (P95)
/// - Batch staging (100 messages): &lt; 50ms (P50), &lt; 100ms (P95)
/// - Concurrent staging: no degradation with up to 10 concurrent transactions
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
[InvocationCount(1, unrollFactor: 1)]
public class OutboxStagingBenchmarks
{
	private InMemoryOutboxStore? _outboxStore;

	/// <summary>
	/// Create an isolated store outside each timed invocation.
	/// </summary>
	[IterationSetup]
	public void IterationSetup()
	{
		if (_outboxStore is not null)
		{
			throw new InvalidOperationException("The previous outbox iteration was not cleaned up.");
		}

		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryOutboxOptions
		{
			MaxMessages = 0 // No eviction: one invocation is bounded by the workload itself.
		});
		var logger = NullLoggerFactory.Instance.CreateLogger<InMemoryOutboxStore>();
		_outboxStore = new InMemoryOutboxStore(options, logger);
	}

	/// <summary>
	/// Release all staged and sent rows outside the timed invocation.
	/// </summary>
	[IterationCleanup]
	public void IterationCleanup()
	{
		_outboxStore?.Dispose();
		_outboxStore = null;
	}

	/// <summary>
	/// Benchmark: Stage a single small message (1KB payload).
	/// </summary>
	[Benchmark(Baseline = true)]
	public ValueTask StageSingleSmall()
	{
		var message = CreateTestOutboxMessage(payloadSizeKb: 1);
		return _outboxStore!.StageMessageAsync(message, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Stage a medium message (10KB payload).
	/// </summary>
	[Benchmark]
	public ValueTask StageSingleMedium()
	{
		var message = CreateTestOutboxMessage(payloadSizeKb: 10);
		return _outboxStore!.StageMessageAsync(message, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Stage a large message (100KB payload).
	/// </summary>
	[Benchmark]
	public ValueTask StageSingleLarge()
	{
		var message = CreateTestOutboxMessage(payloadSizeKb: 100);
		return _outboxStore!.StageMessageAsync(message, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Stage a batch of 10 messages.
	/// </summary>
	[Benchmark]
	public async Task StageBatch10()
	{
		for (var i = 0; i < 10; i++)
		{
			var message = CreateTestOutboxMessage(payloadSizeKb: 1);
			await _outboxStore!.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Benchmark: Stage a batch of 100 messages.
	/// </summary>
	[Benchmark]
	public async Task StageBatch100()
	{
		for (var i = 0; i < 100; i++)
		{
			var message = CreateTestOutboxMessage(payloadSizeKb: 1);
			await _outboxStore!.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Benchmark: Stage a very large payload (1MB).
	/// </summary>
	[Benchmark]
	public ValueTask StageLargePayload()
	{
		var message = CreateTestOutboxMessage(payloadSizeKb: 1024); // 1MB
		return _outboxStore!.StageMessageAsync(message, CancellationToken.None);
	}

	private static OutboundMessage CreateTestOutboxMessage(int payloadSizeKb = 1)
	{
		var payloadData = new byte[payloadSizeKb * 1024];
		Random.Shared.NextBytes(payloadData);

		return new OutboundMessage(
			"TestEvent",
			payloadData,
			"test-destination",
			new Dictionary<string, object>
			{
				["UserId"] = "benchmark-user",
				["TenantId"] = "benchmark-tenant",
				["CorrelationId"] = Guid.NewGuid().ToString(),
			});
	}
}
