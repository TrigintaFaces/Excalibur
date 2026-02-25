// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Text;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Benchmarks.LoadTests;

/// <summary>
/// Sustained throughput load tests for the transport layer using in-memory implementations.
/// Measures send throughput, batch throughput, and send-receive roundtrip under sustained load.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
[BenchmarkCategory("LoadTest")]
public class TransportThroughputLoadTest
{
	private InMemoryTransportSender _sender = null!;
	private InMemoryTransportReceiver _receiver = null!;
	private TransportMessage[] _messages1K = null!;
	private TransportMessage[] _messages10K = null!;
	private byte[] _payload = null!;

	/// <summary>
	/// Number of messages for the current benchmark scenario.
	/// </summary>
	[Params(1_000, 10_000)]
	public int MessageCount { get; set; }

	/// <summary>
	/// Initialize sender, receiver, and pre-built messages.
	/// </summary>
	[GlobalSetup]
	public void GlobalSetup()
	{
		_sender = new InMemoryTransportSender("load-test-topic");
		_receiver = new InMemoryTransportReceiver("load-test-queue");
		_payload = Encoding.UTF8.GetBytes("{\"orderId\":\"12345\",\"amount\":99.99,\"timestamp\":\"2026-01-01T00:00:00Z\"}");

		_messages1K = CreateMessages(1_000);
		_messages10K = CreateMessages(10_000);
	}

	/// <summary>
	/// Clear sender/receiver state between iterations.
	/// </summary>
	[IterationCleanup]
	public void IterationCleanup()
	{
		_sender.Clear();
		_receiver.Clear();
	}

	/// <summary>
	/// Benchmark: Sequential send throughput — sends messages one at a time.
	/// </summary>
	[Benchmark(Baseline = true, Description = "Sequential Send")]
	public async Task SequentialSendThroughput()
	{
		var messages = MessageCount == 1_000 ? _messages1K : _messages10K;

		for (var i = 0; i < messages.Length; i++)
		{
			_ = await _sender.SendAsync(messages[i], CancellationToken.None).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Benchmark: Batch send throughput — sends all messages in a single batch call.
	/// </summary>
	[Benchmark(Description = "Batch Send")]
	public async Task BatchSendThroughput()
	{
		var messages = MessageCount == 1_000 ? _messages1K : _messages10K;
		_ = await _sender.SendBatchAsync(messages, CancellationToken.None).ConfigureAwait(false);
	}

	/// <summary>
	/// Benchmark: Concurrent send throughput — sends messages across 10 concurrent tasks.
	/// </summary>
	[Benchmark(Description = "Concurrent Send (10 tasks)")]
	public async Task ConcurrentSendThroughput()
	{
		var messages = MessageCount == 1_000 ? _messages1K : _messages10K;
		var chunkSize = messages.Length / 10;
		var tasks = new Task[10];

		for (var t = 0; t < 10; t++)
		{
			var start = t * chunkSize;
			var end = (t == 9) ? messages.Length : start + chunkSize;
			var chunk = messages.AsMemory(start, end - start);

			tasks[t] = Task.Run(async () =>
			{
				for (var i = 0; i < chunk.Length; i++)
				{
					_ = await _sender.SendAsync(chunk.Span[i], CancellationToken.None).ConfigureAwait(false);
				}
			});
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	/// <summary>
	/// Benchmark: Send-then-receive roundtrip — measures full message lifecycle.
	/// </summary>
	[Benchmark(Description = "Send + Receive Roundtrip")]
	public async Task SendReceiveRoundtrip()
	{
		var messages = MessageCount == 1_000 ? _messages1K : _messages10K;

		// Enqueue messages for the receiver
		foreach (var msg in messages)
		{
			_receiver.Enqueue(new TransportReceivedMessage
			{
				Id = msg.Id,
				Body = msg.Body,
				ContentType = msg.ContentType,
				Source = "load-test-queue",
			});
		}

		// Receive in batches
		var totalReceived = 0;
		while (totalReceived < messages.Length)
		{
			var batch = await _receiver.ReceiveAsync(100, CancellationToken.None).ConfigureAwait(false);
			totalReceived += batch.Count;

			foreach (var received in batch)
			{
				await _receiver.AcknowledgeAsync(received, CancellationToken.None).ConfigureAwait(false);
			}
		}
	}

	private TransportMessage[] CreateMessages(int count)
	{
		var messages = new TransportMessage[count];
		for (var i = 0; i < count; i++)
		{
			messages[i] = new TransportMessage
			{
				Body = _payload,
				ContentType = "application/json",
				CorrelationId = Guid.NewGuid().ToString(),
				Subject = "load-test.message",
				MessageType = "LoadTestEvent",
			};
		}

		return messages;
	}
}
