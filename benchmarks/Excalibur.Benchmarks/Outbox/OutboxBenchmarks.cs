// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Benchmarks.Outbox;

/// <summary>
/// Benchmarks for Outbox pattern message creation and operations.
/// </summary>
/// <remarks>
/// AD-221-9 Baseline Targets:
/// - Message enqueue: &lt; 500μs
/// - Batch read (100 msgs): &lt; 10ms
/// - Throughput: &gt; 10k msgs/sec
///
/// Note: These benchmarks test the in-memory operations of OutboundMessage.
/// Full SQL Server benchmarks require SqlServerOutboxStore with TestContainers
/// and are available in the integration test suite.
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class OutboxBenchmarks
{
	private OutboundMessage[] _messages = null!;
	private byte[] _testPayload = null!;
	private int _messageCounter;

	[Params(10, 100, 1000)]
	public int MessageCount { get; set; }

	[GlobalSetup]
	public void GlobalSetup()
	{
		_testPayload = new byte[1024];
		Random.Shared.NextBytes(_testPayload);

		_messages = new OutboundMessage[MessageCount];
		for (int i = 0; i < MessageCount; i++)
		{
			_messages[i] = CreateOutboundMessage();
		}
	}

	#region Message Creation Benchmarks

	/// <summary>
	/// Benchmark: Create single OutboundMessage.
	/// </summary>
	[Benchmark(Baseline = true)]
	public OutboundMessage CreateMessage()
	{
		return CreateOutboundMessage();
	}

	/// <summary>
	/// Benchmark: Create OutboundMessage with constructor.
	/// </summary>
	[Benchmark]
	public OutboundMessage CreateMessageWithConstructor()
	{
		return new OutboundMessage(
			"TestMessage",
			_testPayload,
			"test-queue",
			new Dictionary<string, object> { ["correlation-id"] = Guid.NewGuid().ToString() });
	}

	/// <summary>
	/// Benchmark: Create 10 messages.
	/// </summary>
	[Benchmark]
	public OutboundMessage[] CreateBatch_10()
	{
		var messages = new OutboundMessage[10];
		for (int i = 0; i < 10; i++)
		{
			messages[i] = CreateOutboundMessage();
		}
		return messages;
	}

	/// <summary>
	/// Benchmark: Create 100 messages.
	/// </summary>
	[Benchmark]
	public OutboundMessage[] CreateBatch_100()
	{
		var messages = new OutboundMessage[100];
		for (int i = 0; i < 100; i++)
		{
			messages[i] = CreateOutboundMessage();
		}
		return messages;
	}

	#endregion

	#region Message State Transitions

	/// <summary>
	/// Benchmark: Mark message as sending.
	/// </summary>
	[Benchmark]
	public void MarkSending()
	{
		var message = _messages[0];
		message.Status = OutboxStatus.Staged; // Reset
		message.MarkSending();
	}

	/// <summary>
	/// Benchmark: Mark message as sent.
	/// </summary>
	[Benchmark]
	public void MarkSent()
	{
		var message = _messages[0];
		message.Status = OutboxStatus.Sending; // Reset
		message.MarkSent();
	}

	/// <summary>
	/// Benchmark: Mark message as failed.
	/// </summary>
	[Benchmark]
	public void MarkFailed()
	{
		var message = _messages[0];
		message.Status = OutboxStatus.Sending; // Reset
		message.RetryCount = 0;
		message.MarkFailed("Test error");
	}

	/// <summary>
	/// Benchmark: Full message lifecycle.
	/// </summary>
	[Benchmark]
	public void FullMessageLifecycle()
	{
		var message = CreateOutboundMessage();
		message.MarkSending();
		message.MarkSent();
	}

	#endregion

	#region Message Query Methods

	/// <summary>
	/// Benchmark: Check if ready for delivery.
	/// </summary>
	[Benchmark]
	public bool CheckReadyForDelivery()
	{
		return _messages[0].IsReadyForDelivery();
	}

	/// <summary>
	/// Benchmark: Check if eligible for retry.
	/// </summary>
	[Benchmark]
	public bool CheckEligibleForRetry()
	{
		var message = _messages[0];
		message.Status = OutboxStatus.Failed;
		message.RetryCount = 1;
		return message.IsEligibleForRetry(3, 5);
	}

	/// <summary>
	/// Benchmark: Get message age.
	/// </summary>
	[Benchmark]
	public TimeSpan GetMessageAge()
	{
		return _messages[0].GetAge();
	}

	#endregion

	#region Multi-Transport Benchmarks

	/// <summary>
	/// Benchmark: Add transport to message.
	/// </summary>
	[Benchmark]
	public OutboundMessageTransport AddTransport()
	{
		var message = CreateOutboundMessage();
		return message.AddTransport("rabbitmq", "exchange/routing-key");
	}

	/// <summary>
	/// Benchmark: Add multiple transports.
	/// </summary>
	[Benchmark]
	public void AddMultipleTransports()
	{
		var message = CreateOutboundMessage();
		_ = message.AddTransport("rabbitmq", "exchange/routing-key");
		_ = message.AddTransport("kafka", "topic-name");
		_ = message.AddTransport("azure-servicebus", "queue-name");
	}

	/// <summary>
	/// Benchmark: Update aggregate status.
	/// </summary>
	[Benchmark]
	public void UpdateAggregateStatus()
	{
		var message = CreateOutboundMessage();
		_ = message.AddTransport("rabbitmq");
		_ = message.AddTransport("kafka");
		message.TransportDeliveries.First().Status = TransportDeliveryStatus.Sent;
		message.UpdateAggregateStatus();
	}

	/// <summary>
	/// Benchmark: Check if all transports complete.
	/// </summary>
	[Benchmark]
	public bool CheckAllTransportsComplete()
	{
		var message = _messages[0];
		return message.AreAllTransportsComplete();
	}

	#endregion

	#region Helpers

	private OutboundMessage CreateOutboundMessage()
	{
		_ = Interlocked.Increment(ref _messageCounter);
		return new OutboundMessage
		{
			Id = $"msg-{Guid.NewGuid():N}",
			MessageType = "TestMessage",
			Payload = _testPayload,
			Destination = "test-queue",
			CorrelationId = Guid.NewGuid().ToString(),
			CreatedAt = DateTimeOffset.UtcNow,
			Status = OutboxStatus.Staged
		};
	}

	#endregion
}
