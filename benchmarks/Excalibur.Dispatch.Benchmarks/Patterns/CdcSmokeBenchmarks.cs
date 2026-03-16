// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Threading.Channels;

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.Delivery.BatchProcessing;

namespace Excalibur.Dispatch.Benchmarks.Patterns;

/// <summary>
/// Minimal CDC smoke benchmark used by CI performance gates.
/// Keeps the shape deterministic while still exercising the critical dequeue path.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(PatternsBenchmarkConfig))]
public class CdcSmokeBenchmarks
{
	private static readonly TimeSpan BenchmarkTimeout = TimeSpan.FromSeconds(15);

	private Channel<TestChangeEvent>? _channel;
	private TestChangeEvent[]? _preAllocatedEvents;

	[Params(10, 100, 500)]
	public int BatchSize { get; set; }

	[GlobalSetup]
	public void GlobalSetup()
	{
		_channel = Channel.CreateBounded<TestChangeEvent>(new BoundedChannelOptions(10000)
		{
			FullMode = BoundedChannelFullMode.Wait,
			SingleReader = false,
			SingleWriter = false,
			AllowSynchronousContinuations = false,
		});

		_preAllocatedEvents = new TestChangeEvent[10000];
		for (var i = 0; i < _preAllocatedEvents.Length; i++)
		{
			_preAllocatedEvents[i] = new TestChangeEvent
			{
				Lsn = BitConverter.GetBytes((long)i),
				SeqVal = BitConverter.GetBytes((long)(i * 10)),
				TableName = $"Table_{i % 10}",
			};
		}
	}

	[IterationSetup]
	public void IterationSetup()
	{
		while (_channel!.Reader.TryRead(out _))
		{ }

		var eventsToWrite = Math.Min(BatchSize * 2, _preAllocatedEvents!.Length);
		for (var i = 0; i < eventsToWrite; i++)
		{
			if (!_channel.Writer.TryWrite(_preAllocatedEvents[i]))
			{
				_channel.Writer.WriteAsync(_preAllocatedEvents[i]).AsTask().GetAwaiter().GetResult();
			}
		}

		Debug.Assert(_channel.Reader.Count >= BatchSize,
			$"Channel has {_channel.Reader.Count} items, expected >= {BatchSize}");
	}

	[Benchmark(Baseline = true, Description = "DequeueBatch")]
	public async Task<int> DequeueBatch()
	{
		using var cts = new CancellationTokenSource(BenchmarkTimeout);
		var batch = await ChannelBatchUtilities.DequeueBatchAsync(
			_channel!.Reader,
			BatchSize,
			cts.Token).ConfigureAwait(false);

		return batch.Length;
	}

	private sealed class TestChangeEvent
	{
		public required byte[] Lsn { get; init; }
		public required byte[] SeqVal { get; init; }
		public required string TableName { get; init; }
	}
}
