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
/// Benchmarks for outbox message polling operations.
/// Measures performance of retrieving unsent messages for publishing.
/// </summary>
/// <remarks>
/// Performance Targets (from testing-and-benchmarking-strategy-spec.md):
/// - Poll empty outbox: &lt; 5ms (P50), &lt; 10ms (P95)
/// - Poll with 10 messages: &lt; 10ms (P50), &lt; 20ms (P95)
/// - Poll with 100 messages: &lt; 50ms (P50), &lt; 100ms (P95)
/// - Poll with 1000 messages: &lt; 200ms (P50), &lt; 400ms (P95)
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class OutboxPollingBenchmarks
{
	private InMemoryOutboxStore? _outboxStore;

	/// <summary>
	/// Number of messages to pre-populate for polling tests.
	/// </summary>
	[Params(0, 10, 100, 1000)]
	public int MessageCount { get; set; }

	/// <summary>
	/// Initialize in-memory outbox store and pre-populate before benchmarks.
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

		// Pre-populate with messages
		for (var i = 0; i < MessageCount; i++)
		{
			var message = new OutboundMessage(
				"TestEvent",
				new byte[1024],
				"test-destination",
				new Dictionary<string, object>
				{
					["CorrelationId"] = Guid.NewGuid().ToString(),
					["Index"] = i
				});
			_outboxStore.StageMessageAsync(message, CancellationToken.None).AsTask().GetAwaiter().GetResult();
		}
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
	/// Benchmark: Poll for unsent messages (batch of 10).
	/// </summary>
	[Benchmark(Baseline = true)]
	public async Task<int> PollBatch10()
	{
		var messages = await _outboxStore!.GetUnsentMessagesAsync(batchSize: 10, CancellationToken.None).ConfigureAwait(false);
		return messages.Count();
	}

	/// <summary>
	/// Benchmark: Poll for unsent messages (batch of 100).
	/// </summary>
	[Benchmark]
	public async Task<int> PollBatch100()
	{
		var messages = await _outboxStore!.GetUnsentMessagesAsync(batchSize: 100, CancellationToken.None).ConfigureAwait(false);
		return messages.Count();
	}

	/// <summary>
	/// Benchmark: Poll for unsent messages (batch of 500).
	/// </summary>
	[Benchmark]
	public async Task<int> PollBatch500()
	{
		var messages = await _outboxStore!.GetUnsentMessagesAsync(batchSize: 500, CancellationToken.None).ConfigureAwait(false);
		return messages.Count();
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
