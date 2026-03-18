// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Inbox.InMemory;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Benchmarks.Patterns;

/// <summary>
/// Benchmarks for InboxProcessor throughput operations.
/// Measures message processing performance for the inbox pattern.
/// </summary>
/// <remarks>
/// Performance Targets (from sprint-34-plan.md):
/// - InboxProcessor Throughput: &gt;10,000 msg/sec (MUST PASS)
/// - Memory Allocation: &lt;1KB per batch (SHOULD PASS)
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class InboxThroughputBenchmarks
{
	private InMemoryInboxStore? _inboxStore;

	/// <summary>
	/// Number of messages to pre-populate for throughput tests.
	/// </summary>
	[Params(100, 1000, 10000)]
	public int MessageCount { get; set; }

	/// <summary>
	/// Initialize in-memory inbox store and pre-populate before benchmarks.
	/// </summary>
	[GlobalSetup]
	public void GlobalSetup()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryInboxOptions
		{
			MaxEntries = 0 // unlimited
		});
		var logger = NullLoggerFactory.Instance.CreateLogger<InMemoryInboxStore>();
		_inboxStore = new InMemoryInboxStore(options, logger);

		// Pre-populate inbox with messages
		for (var i = 0; i < MessageCount; i++)
		{
			_inboxStore.CreateEntryAsync(
				$"msg-{i}",
				"TestHandler",
				"TestEvent",
				new byte[1024],
				new Dictionary<string, object>
				{
					["UserId"] = "benchmark-user",
					["TenantId"] = "benchmark-tenant",
					["CorrelationId"] = Guid.NewGuid().ToString(),
				},
				CancellationToken.None).AsTask().GetAwaiter().GetResult();
		}
	}

	/// <summary>
	/// Cleanup inbox store after benchmarks.
	/// </summary>
	[GlobalCleanup]
	public void GlobalCleanup()
	{
		_inboxStore?.Dispose();
	}

	/// <summary>
	/// Benchmark: Get all entries from inbox (batch retrieval).
	/// Target: &gt;10,000 msg/sec throughput.
	/// </summary>
	[Benchmark(Baseline = true)]
	public async Task<int> GetAllEntries()
	{
		var entries = await _inboxStore!.GetAllEntriesAsync(CancellationToken.None).ConfigureAwait(false);
		return entries.Count();
	}

	/// <summary>
	/// Benchmark: Get inbox statistics.
	/// </summary>
	[Benchmark]
	public async Task<long> GetStatistics()
	{
		var stats = await _inboxStore!.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		return stats.TotalEntries;
	}

	/// <summary>
	/// Benchmark: Create inbox entry (deduplication check).
	/// </summary>
	[Benchmark]
	public async Task CreateEntry()
	{
		var messageId = Guid.NewGuid().ToString();
		_ = await _inboxStore!.CreateEntryAsync(
			messageId,
			"TestHandler",
			"TestEvent",
			new byte[1024],
			new Dictionary<string, object> { ["CorrelationId"] = Guid.NewGuid().ToString() },
			CancellationToken.None).ConfigureAwait(false);
	}

	/// <summary>
	/// Benchmark: Check if message is processed (idempotency lookup).
	/// </summary>
	[Benchmark]
	public async Task<bool> IsAlreadyProcessed()
	{
		// Use a known message ID from pre-populated data
		return await _inboxStore!.IsProcessedAsync("msg-0", "TestHandler", CancellationToken.None).ConfigureAwait(false);
	}

	/// <summary>
	/// Benchmark: Full message processing cycle (create + mark processed).
	/// </summary>
	[Benchmark]
	public async Task FullProcessingCycle()
	{
		var messageId = Guid.NewGuid().ToString();

		// Create entry
		_ = await _inboxStore!.CreateEntryAsync(
			messageId,
			"TestHandler",
			"TestEvent",
			new byte[1024],
			new Dictionary<string, object> { ["CorrelationId"] = Guid.NewGuid().ToString() },
			CancellationToken.None).ConfigureAwait(false);

		// Mark as processed
		await _inboxStore.MarkProcessedAsync(messageId, "TestHandler", CancellationToken.None).ConfigureAwait(false);
	}
}
