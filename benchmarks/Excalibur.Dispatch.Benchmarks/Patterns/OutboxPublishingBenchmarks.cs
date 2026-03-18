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
/// Benchmarks for outbox message publishing operations.
/// Measures performance of staging, marking sent, full publish cycles, and batch processing.
/// </summary>
/// <remarks>
/// Performance Targets (from testing-and-benchmarking-strategy-spec.md):
/// - Single message publish: &lt; 5ms (P50), &lt; 10ms (P95)
/// - Batch publish (100 messages): &lt; 50ms (P50), &lt; 100ms (P95)
/// - Publishing with retries: &lt; 10ms per retry (P50), &lt; 20ms (P95)
/// - Dead letter operations: &lt; 10ms (P50), &lt; 20ms (P95)
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class OutboxPublishingBenchmarks
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
	/// Benchmark: Stage a single message (outbox write).
	/// </summary>
	[Benchmark(Baseline = true)]
	public ValueTask StageMessage()
	{
		var message = new OutboundMessage(
			"TestEvent",
			new byte[1024],
			"test-destination",
			new Dictionary<string, object> { ["CorrelationId"] = Guid.NewGuid().ToString() });
		return _outboxStore!.StageMessageAsync(message, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Full publishing cycle (stage + mark sent).
	/// Each iteration stages a fresh message to avoid already-sent errors.
	/// </summary>
	[Benchmark]
	public async Task FullPublishCycle()
	{
		var message = new OutboundMessage(
			"TestEvent",
			new byte[1024],
			"test-destination",
			new Dictionary<string, object> { ["CorrelationId"] = Guid.NewGuid().ToString() });
		await _outboxStore!.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await _outboxStore.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);
	}

	/// <summary>
	/// Benchmark: Stage batch then poll and mark sent.
	/// Each iteration stages fresh messages to ensure unsent messages are available.
	/// </summary>
	[Benchmark]
	public async Task<int> StageThenProcessBatch100()
	{
		// Stage 100 messages
		for (var i = 0; i < 100; i++)
		{
			var msg = new OutboundMessage(
				"TestEvent",
				new byte[1024],
				"test-destination",
				new Dictionary<string, object> { ["CorrelationId"] = Guid.NewGuid().ToString() });
			await _outboxStore!.StageMessageAsync(msg, CancellationToken.None).ConfigureAwait(false);
		}

		// Poll and mark sent
		var messages = await _outboxStore!.GetUnsentMessagesAsync(batchSize: 100, CancellationToken.None).ConfigureAwait(false);
		var count = 0;
		foreach (var message in messages)
		{
			await _outboxStore.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);
			count++;
		}
		return count;
	}

	/// <summary>
	/// Benchmark: Get outbox statistics.
	/// </summary>
	[Benchmark]
	public async Task<long> GetStatistics()
	{
		var stats = await _outboxStore!.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		return stats.StagedMessageCount;
	}
}
