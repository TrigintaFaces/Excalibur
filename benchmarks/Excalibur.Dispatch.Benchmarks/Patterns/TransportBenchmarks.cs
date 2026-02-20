// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;
using System.Text.Json;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Diagnostics;

namespace Excalibur.Dispatch.Benchmarks.Patterns;

/// <summary>
/// Benchmarks for transport layer operations including message construction,
/// serialization, and batch processing patterns.
/// </summary>
/// <remarks>
/// Sprint 510 - Transport Layer Benchmarks (bd-jkmbp).
/// These benchmarks measure abstraction layer performance without requiring
/// actual transport infrastructure (RabbitMQ, Kafka, etc.).
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class TransportBenchmarks
{
	private static readonly string[] SampleItems = ["item1", "item2", "item3"];

	private byte[] _smallPayload = null!;
	private byte[] _mediumPayload = null!;
	private byte[] _largePayload = null!;
	private TransportMessage _prebuiltMessage = null!;
	private TransportMessage[] _batchMessages = null!;
	private string _jsonPayload = null!;

	/// <summary>
	/// Initialize test data before benchmarks.
	/// </summary>
	[GlobalSetup]
	public void GlobalSetup()
	{
		// Create payloads of different sizes
		_smallPayload = Encoding.UTF8.GetBytes("{\"orderId\":\"12345\",\"amount\":99.99}");
		_mediumPayload = new byte[4096]; // 4KB
		Random.Shared.NextBytes(_mediumPayload);
		_largePayload = new byte[65536]; // 64KB
		Random.Shared.NextBytes(_largePayload);

		// Pre-built message for read benchmarks
		_prebuiltMessage = CreateMessage(_smallPayload);
		_prebuiltMessage.Properties["CustomHeader1"] = "value1";
		_prebuiltMessage.Properties["CustomHeader2"] = "value2";
		_prebuiltMessage.Properties["Timestamp"] = DateTimeOffset.UtcNow.ToString("O");

		// Batch of messages
		_batchMessages = new TransportMessage[100];
		for (var i = 0; i < 100; i++)
		{
			_batchMessages[i] = CreateMessage(_smallPayload);
		}

		// JSON payload for serialization tests
		_jsonPayload = JsonSerializer.Serialize(new
		{
			OrderId = Guid.NewGuid(),
			Amount = 99.99m,
			Items = SampleItems,
			Customer = new { Name = "Test", Email = "test@example.com" },
		});
	}

	#region Message Construction Benchmarks

	/// <summary>
	/// Benchmark: Create TransportMessage with small payload (typical event size).
	/// </summary>
	[Benchmark(Baseline = true, Description = "Create TransportMessage (Small)")]
	public TransportMessage CreateMessage_Small()
	{
		return CreateMessage(_smallPayload);
	}

	/// <summary>
	/// Benchmark: Create TransportMessage with medium payload (4KB).
	/// </summary>
	[Benchmark(Description = "Create TransportMessage (4KB)")]
	public TransportMessage CreateMessage_Medium()
	{
		return CreateMessage(_mediumPayload);
	}

	/// <summary>
	/// Benchmark: Create TransportMessage with large payload (64KB).
	/// </summary>
	[Benchmark(Description = "Create TransportMessage (64KB)")]
	public TransportMessage CreateMessage_Large()
	{
		return CreateMessage(_largePayload);
	}

	/// <summary>
	/// Benchmark: Create TransportMessage using FromBytes factory.
	/// </summary>
	[Benchmark(Description = "TransportMessage.FromBytes")]
	public TransportMessage CreateMessage_FromBytes()
	{
		return TransportMessage.FromBytes(_smallPayload);
	}

	/// <summary>
	/// Benchmark: Create TransportMessage using FromString factory.
	/// </summary>
	[Benchmark(Description = "TransportMessage.FromString")]
	public TransportMessage CreateMessage_FromString()
	{
		return TransportMessage.FromString(_jsonPayload);
	}

	/// <summary>
	/// Benchmark: Copy an existing message by constructing a new one with same properties.
	/// </summary>
	[Benchmark(Description = "TransportMessage Copy")]
	public TransportMessage CopyMessage()
	{
		return new TransportMessage
		{
			Id = _prebuiltMessage.Id,
			Body = _prebuiltMessage.Body,
			ContentType = _prebuiltMessage.ContentType,
			MessageType = _prebuiltMessage.MessageType,
			CorrelationId = _prebuiltMessage.CorrelationId,
			Subject = _prebuiltMessage.Subject,
			TimeToLive = _prebuiltMessage.TimeToLive,
			CreatedAt = _prebuiltMessage.CreatedAt,
			Properties = new Dictionary<string, object>(_prebuiltMessage.Properties),
		};
	}

	#endregion

	#region Message Property Access Benchmarks

	/// <summary>
	/// Benchmark: Read all common properties from a message.
	/// </summary>
	[Benchmark(Description = "Read All Properties")]
	public (string?, string?, string?, string?) ReadAllProperties()
	{
		return (
			_prebuiltMessage.Id,
			_prebuiltMessage.CorrelationId,
			_prebuiltMessage.ContentType,
			_prebuiltMessage.Subject);
	}

	/// <summary>
	/// Benchmark: Access custom Properties dictionary.
	/// </summary>
	[Benchmark(Description = "Access Custom Properties")]
	public object? AccessCustomProperties()
	{
		return _prebuiltMessage.Properties.TryGetValue("CustomHeader1", out var value) ? value : null;
	}

	#endregion

	#region Batch Processing Benchmarks

	/// <summary>
	/// Benchmark: Create a batch of 10 messages.
	/// </summary>
	[Benchmark(Description = "Create Batch (10 messages)")]
	public TransportMessage[] CreateBatch_10()
	{
		var batch = new TransportMessage[10];
		for (var i = 0; i < 10; i++)
		{
			batch[i] = CreateMessage(_smallPayload);
		}

		return batch;
	}

	/// <summary>
	/// Benchmark: Create a batch of 100 messages.
	/// </summary>
	[Benchmark(Description = "Create Batch (100 messages)")]
	public TransportMessage[] CreateBatch_100()
	{
		var batch = new TransportMessage[100];
		for (var i = 0; i < 100; i++)
		{
			batch[i] = CreateMessage(_smallPayload);
		}

		return batch;
	}

	/// <summary>
	/// Benchmark: Iterate through a batch and access body.
	/// </summary>
	[Benchmark(Description = "Iterate Batch (100 messages)")]
	public int IterateBatch_100()
	{
		var totalSize = 0;
		foreach (var msg in _batchMessages)
		{
			totalSize += msg.Body.Length;
		}

		return totalSize;
	}

	/// <summary>
	/// Benchmark: Copy a batch of messages.
	/// </summary>
	[Benchmark(Description = "Copy Batch (100 messages)")]
	public TransportMessage[] CopyBatch_100()
	{
		var copied = new TransportMessage[100];
		for (var i = 0; i < 100; i++)
		{
			var src = _batchMessages[i];
			copied[i] = new TransportMessage
			{
				Id = src.Id,
				Body = src.Body,
				ContentType = src.ContentType,
				MessageType = src.MessageType,
				CorrelationId = src.CorrelationId,
				Subject = src.Subject,
				TimeToLive = src.TimeToLive,
				CreatedAt = src.CreatedAt,
				Properties = new Dictionary<string, object>(src.Properties),
			};
		}

		return copied;
	}

	#endregion

	#region Serialization Pattern Benchmarks

	/// <summary>
	/// Benchmark: Serialize message body to JSON.
	/// </summary>
	[Benchmark(Description = "Serialize Body (JSON)")]
	public byte[] SerializeBody_Json()
	{
		var payload = new
		{
			OrderId = Guid.NewGuid(),
			Amount = 99.99m,
			Timestamp = DateTimeOffset.UtcNow,
		};
		return JsonSerializer.SerializeToUtf8Bytes(payload);
	}

	/// <summary>
	/// Benchmark: Deserialize message body from JSON.
	/// </summary>
	[Benchmark(Description = "Deserialize Body (JSON)")]
	public JsonDocument DeserializeBody_Json()
	{
		return JsonDocument.Parse(_prebuiltMessage.Body);
	}

	/// <summary>
	/// Benchmark: Full message roundtrip (create + serialize body + set properties).
	/// </summary>
	[Benchmark(Description = "Full Message Roundtrip")]
	public TransportMessage FullMessageRoundtrip()
	{
		var payload = JsonSerializer.SerializeToUtf8Bytes(new { OrderId = Guid.NewGuid(), Amount = 99.99m });
		var message = new TransportMessage
		{
			Body = payload,
			ContentType = "application/json",
			CorrelationId = Guid.NewGuid().ToString(),
			Subject = "order.created",
			MessageType = "OrderCreated",
		};
		message.Properties["Source"] = "OrderService";
		message.Properties["Version"] = "1.0";
		message.Properties[TransportTelemetryConstants.PropertyKeys.PartitionKey] = "customer-123";
		return message;
	}

	#endregion

	#region Transport-Specific Pattern Benchmarks

	/// <summary>
	/// Benchmark: Set ordering key via Properties (Kafka/Google Pub/Sub pattern).
	/// </summary>
	[Benchmark(Description = "Set OrderingKey (Kafka)")]
	public TransportMessage SetOrderingKey()
	{
		var message = CreateMessage(_smallPayload);
		message.Properties[TransportTelemetryConstants.PropertyKeys.OrderingKey] = "partition-key-123";
		return message;
	}

	/// <summary>
	/// Benchmark: Set message group ID via Properties (AWS SQS FIFO pattern).
	/// </summary>
	[Benchmark(Description = "Set MessageGroupId (SQS FIFO)")]
	public TransportMessage SetMessageGroupId()
	{
		var message = CreateMessage(_smallPayload);
		message.Properties[TransportTelemetryConstants.PropertyKeys.OrderingKey] = "group-123";
		message.Properties[TransportTelemetryConstants.PropertyKeys.DeduplicationId] = Guid.NewGuid().ToString();
		return message;
	}

	/// <summary>
	/// Benchmark: Set session ID via Properties (Azure Service Bus pattern).
	/// </summary>
	[Benchmark(Description = "Set SessionId (Azure SB)")]
	public TransportMessage SetSessionId()
	{
		var message = CreateMessage(_smallPayload);
		message.Properties[TransportTelemetryConstants.PropertyKeys.OrderingKey] = "session-123";
		message.Properties["ReplyTo"] = "response-queue";
		return message;
	}

	/// <summary>
	/// Benchmark: Schedule message delivery via Properties (delayed message pattern).
	/// </summary>
	[Benchmark(Description = "Schedule Delivery")]
	public TransportMessage ScheduleDelivery()
	{
		var message = CreateMessage(_smallPayload);
		message.Properties[TransportTelemetryConstants.PropertyKeys.ScheduledTime] =
			DateTimeOffset.UtcNow.AddMinutes(5).ToString("O");
		message.TimeToLive = TimeSpan.FromHours(1);
		return message;
	}

	#endregion

	private static TransportMessage CreateMessage(byte[] payload)
	{
		return new TransportMessage
		{
			Body = payload,
			ContentType = "application/json",
			CorrelationId = Guid.NewGuid().ToString(),
			Subject = "test.message",
		};
	}
}
