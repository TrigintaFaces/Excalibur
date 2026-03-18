// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Abstractions;

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
public class OutboxStagingBenchmarks
{
	private InMemoryOutboxStore? _outboxStore;

	/// <summary>
	/// Initialize in-memory outbox store before benchmarks.
	/// </summary>
	[GlobalSetup]
	public void GlobalSetup()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryOutboxOptions
		{
			MaxMessages = 0 // unlimited
		});
		var logger = NullLoggerFactory.Instance.CreateLogger<InMemoryOutboxStore>();
		_outboxStore = new InMemoryOutboxStore(options, logger);
	}

	/// <summary>
	/// Cleanup outbox store after benchmarks.
	/// </summary>
	[GlobalCleanup]
	public void GlobalCleanup()
	{
		_outboxStore?.Dispose();
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
