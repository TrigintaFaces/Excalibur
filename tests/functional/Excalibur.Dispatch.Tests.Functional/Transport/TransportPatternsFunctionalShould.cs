// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text;

namespace Excalibur.Dispatch.Tests.Functional.Transport;

/// <summary>
/// Functional tests for transport and messaging patterns.
/// </summary>
[Trait("Category", "Functional")]
[Trait("Component", "Transport")]
[Trait("Feature", "Patterns")]
public sealed class TransportPatternsFunctionalShould : FunctionalTestBase
{
	[Fact]
	public void ImplementRequestResponsePattern()
	{
		// Arrange
		var correlationId = Guid.NewGuid();
		var pendingRequests = new ConcurrentDictionary<Guid, TaskCompletionSource<string>>();

		// Simulate sending request
		var requestTcs = new TaskCompletionSource<string>();
		pendingRequests[correlationId] = requestTcs;

		// Simulate receiving response
		var response = new ResponseMessage
		{
			CorrelationId = correlationId,
			Result = "Success",
		};

		// Act - Match response to request
		if (pendingRequests.TryRemove(response.CorrelationId, out var tcs))
		{
			tcs.SetResult(response.Result);
		}

		// Assert
		requestTcs.Task.IsCompleted.ShouldBeTrue();
		requestTcs.Task.Result.ShouldBe("Success");
	}

	[Fact]
	public async Task ImplementPublishSubscribePattern()
	{
		// Arrange
		var topic = "order.created";
		var subscribers = new ConcurrentDictionary<string, ConcurrentQueue<string>>();
		subscribers["subscriber-1"] = new ConcurrentQueue<string>();
		subscribers["subscriber-2"] = new ConcurrentQueue<string>();
		subscribers["subscriber-3"] = new ConcurrentQueue<string>();

		// Act - Publish message to topic
		var message = "OrderId: 123";
		await PublishToTopicAsync(topic, message, subscribers).ConfigureAwait(false);

		// Assert - All subscribers should receive the message
		foreach (var (_, queue) in subscribers)
		{
			queue.TryDequeue(out var received).ShouldBeTrue();
			received.ShouldBe(message);
		}
	}

	[Fact]
	public void ImplementMessageBatching()
	{
		// Arrange
		var batchSize = 10;
		var messages = Enumerable.Range(1, 25).Select(i => $"Message-{i}").ToList();
		var batches = new List<List<string>>();

		// Act - Batch messages
		for (var i = 0; i < messages.Count; i += batchSize)
		{
			var batch = messages.Skip(i).Take(batchSize).ToList();
			batches.Add(batch);
		}

		// Assert
		batches.Count.ShouldBe(3);
		batches[0].Count.ShouldBe(10);
		batches[1].Count.ShouldBe(10);
		batches[2].Count.ShouldBe(5);
	}

	[Fact]
	public void CompressLargeMessages()
	{
		// Arrange
		var originalMessage = new string('A', 10000); // 10KB message
		var originalBytes = Encoding.UTF8.GetBytes(originalMessage);

		// Act - Compress
		byte[] compressedBytes;
		using (var outputStream = new MemoryStream())
		{
			using (var gzipStream = new GZipStream(outputStream, CompressionLevel.Optimal))
			{
				gzipStream.Write(originalBytes, 0, originalBytes.Length);
			}

			compressedBytes = outputStream.ToArray();
		}

		// Decompress
		string decompressedMessage;
		using (var inputStream = new MemoryStream(compressedBytes))
		using (var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress))
		using (var reader = new StreamReader(gzipStream))
		{
			decompressedMessage = reader.ReadToEnd();
		}

		// Assert
		compressedBytes.Length.ShouldBeLessThan(originalBytes.Length);
		decompressedMessage.ShouldBe(originalMessage);
	}

	[Fact]
	public void ImplementMessagePrioritization()
	{
		// Arrange
		var priorityQueue = new PriorityQueue<TestMessage, int>();

		priorityQueue.Enqueue(new TestMessage { Id = "low", Content = "Low priority" }, 3);
		priorityQueue.Enqueue(new TestMessage { Id = "high", Content = "High priority" }, 1);
		priorityQueue.Enqueue(new TestMessage { Id = "medium", Content = "Medium priority" }, 2);
		priorityQueue.Enqueue(new TestMessage { Id = "critical", Content = "Critical priority" }, 0);

		// Act - Dequeue in priority order
		var processingOrder = new List<string>();
		while (priorityQueue.Count > 0)
		{
			processingOrder.Add(priorityQueue.Dequeue().Id);
		}

		// Assert
		processingOrder[0].ShouldBe("critical");
		processingOrder[1].ShouldBe("high");
		processingOrder[2].ShouldBe("medium");
		processingOrder[3].ShouldBe("low");
	}

	[Fact]
	public void ImplementMessageFiltering()
	{
		// Arrange
		var messages = new List<TestMessage>
		{
			new() { Id = "1", Content = "Order created", Category = "order" },
			new() { Id = "2", Content = "Payment received", Category = "payment" },
			new() { Id = "3", Content = "Order shipped", Category = "order" },
			new() { Id = "4", Content = "User registered", Category = "user" },
		};

		var subscriptionFilter = "order"; // Only interested in order events

		// Act - Apply filter
		var filteredMessages = messages.Where(m => m.Category == subscriptionFilter).ToList();

		// Assert
		filteredMessages.Count.ShouldBe(2);
		filteredMessages.ShouldAllBe(m => m.Category == "order");
	}

	[Fact]
	public void ImplementMessageRouting()
	{
		// Arrange
		var routingTable = new Dictionary<string, string>
		{
			["order.*"] = "order-service-queue",
			["payment.*"] = "payment-service-queue",
			["user.*"] = "user-service-queue",
			["*"] = "default-queue",
		};

		var messages = new[]
		{
			new RoutedMessage { RoutingKey = "order.created" },
			new RoutedMessage { RoutingKey = "payment.received" },
			new RoutedMessage { RoutingKey = "user.registered" },
			new RoutedMessage { RoutingKey = "unknown.event" },
		};

		// Act - Route messages
		foreach (var msg in messages)
		{
			msg.Destination = ResolveDestination(msg.RoutingKey, routingTable);
		}

		// Assert
		messages[0].Destination.ShouldBe("order-service-queue");
		messages[1].Destination.ShouldBe("payment-service-queue");
		messages[2].Destination.ShouldBe("user-service-queue");
		messages[3].Destination.ShouldBe("default-queue");
	}

	[Fact]
	public void ImplementMessageTtl()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;
		var messages = new[]
		{
			new TtlMessage { CreatedAt = now.AddMinutes(-5), TtlMinutes = 10 },  // Valid
			new TtlMessage { CreatedAt = now.AddMinutes(-15), TtlMinutes = 10 }, // Expired
			new TtlMessage { CreatedAt = now.AddMinutes(-1), TtlMinutes = 5 },   // Valid
		};

		// Act - Filter expired messages
		var validMessages = messages.Where(m =>
			m.CreatedAt.AddMinutes(m.TtlMinutes) > now).ToList();

		// Assert
		validMessages.Count.ShouldBe(2);
	}

	[Fact]
	public void ImplementMessageAcknowledgment()
	{
		// Arrange
		var messageQueue = new ConcurrentQueue<AcknowledgeableMessage>();
		var message = new AcknowledgeableMessage
		{
			Id = Guid.NewGuid(),
			Content = "Test",
			AcknowledgedAt = null,
		};

		messageQueue.Enqueue(message);

		// Act - Process and acknowledge
		messageQueue.TryDequeue(out var processing).ShouldBeTrue();
		processing.AcknowledgedAt = DateTimeOffset.UtcNow;

		// Assert
		_ = processing.AcknowledgedAt.ShouldNotBeNull();
		processing.IsAcknowledged.ShouldBeTrue();
	}

	[Fact]
	public void ImplementConnectionPooling()
	{
		// Arrange
		var poolSize = 5;
		var connectionPool = new ConnectionPool(poolSize);

		// Act - Acquire and release connections
		var connections = new List<TestConnection>();
		for (var i = 0; i < poolSize; i++)
		{
			connections.Add(connectionPool.Acquire());
		}

		// All connections should be in use
		connectionPool.AvailableConnections.ShouldBe(0);

		// Release one
		connectionPool.Release(connections[0]);
		connectionPool.AvailableConnections.ShouldBe(1);

		// Release all
		foreach (var conn in connections.Skip(1))
		{
			connectionPool.Release(conn);
		}

		// Assert
		connectionPool.AvailableConnections.ShouldBe(poolSize);
	}

	[Fact]
	public void TrackTransportMetrics()
	{
		// Arrange
		var metrics = new TransportMetrics
		{
			MessagesSent = 10000,
			MessagesReceived = 9950,
			BytesSent = 5_000_000,
			BytesReceived = 4_900_000,
			ConnectionErrors = 5,
			Timeouts = 10,
			AverageLatencyMs = 15.5,
		};

		// Act
		var deliveryRate = (double)metrics.MessagesReceived / metrics.MessagesSent;
		var errorRate = (double)(metrics.ConnectionErrors + metrics.Timeouts) / metrics.MessagesSent;

		// Assert
		deliveryRate.ShouldBe(0.995);
		errorRate.ShouldBe(0.0015);
		metrics.AverageLatencyMs.ShouldBeLessThan(20);
	}

	[Fact]
	public void ImplementCircuitBreakerForTransport()
	{
		// Arrange
		var circuitBreaker = new TransportCircuitBreaker(failureThreshold: 3);

		// Act - Simulate failures
		circuitBreaker.RecordFailure();
		circuitBreaker.IsOpen.ShouldBeFalse();

		circuitBreaker.RecordFailure();
		circuitBreaker.IsOpen.ShouldBeFalse();

		circuitBreaker.RecordFailure();
		circuitBreaker.IsOpen.ShouldBeTrue(); // Should open after 3 failures

		// Assert
		circuitBreaker.CanSend.ShouldBeFalse();
	}

	private static Task PublishToTopicAsync(
		string topic,
		string message,
		ConcurrentDictionary<string, ConcurrentQueue<string>> subscribers)
	{
		foreach (var (_, queue) in subscribers)
		{
			queue.Enqueue(message);
		}

		return Task.CompletedTask;
	}

	private static string ResolveDestination(string routingKey, Dictionary<string, string> routingTable)
	{
		// Check for prefix matches
		foreach (var (pattern, destination) in routingTable)
		{
			if (pattern == "*")
			{
				continue;
			}

			var prefix = pattern.TrimEnd('*');
			if (routingKey.StartsWith(prefix, StringComparison.Ordinal))
			{
				return destination;
			}
		}

		// Fall back to default
		return routingTable.TryGetValue("*", out var defaultDest) ? defaultDest : string.Empty;
	}

	private sealed class ResponseMessage
	{
		public Guid CorrelationId { get; init; }
		public string Result { get; init; } = string.Empty;
	}

	private sealed class TestMessage
	{
		public string Id { get; init; } = string.Empty;
		public string Content { get; init; } = string.Empty;
		public string Category { get; init; } = string.Empty;
	}

	private sealed class RoutedMessage
	{
		public string RoutingKey { get; init; } = string.Empty;
		public string Destination { get; set; } = string.Empty;
	}

	private sealed class TtlMessage
	{
		public DateTimeOffset CreatedAt { get; init; }
		public int TtlMinutes { get; init; }
	}

	private sealed class AcknowledgeableMessage
	{
		public Guid Id { get; init; }
		public string Content { get; init; } = string.Empty;
		public DateTimeOffset? AcknowledgedAt { get; set; }
		public bool IsAcknowledged => AcknowledgedAt.HasValue;
	}

	private sealed class TestConnection
	{
		public Guid Id { get; } = Guid.NewGuid();
	}

	private sealed class ConnectionPool
	{
		private readonly ConcurrentBag<TestConnection> _available;
		private readonly int _maxSize;

		public ConnectionPool(int maxSize)
		{
			_maxSize = maxSize;
			_available = new ConcurrentBag<TestConnection>();

			for (var i = 0; i < maxSize; i++)
			{
				_available.Add(new TestConnection());
			}
		}

		public int AvailableConnections => _available.Count;

		public TestConnection Acquire()
		{
			if (_available.TryTake(out var connection))
			{
				return connection;
			}

			throw new InvalidOperationException("No connections available");
		}

		public void Release(TestConnection connection)
		{
			if (_available.Count < _maxSize)
			{
				_available.Add(connection);
			}
		}
	}

	private sealed class TransportMetrics
	{
		public int MessagesSent { get; init; }
		public int MessagesReceived { get; init; }
		public long BytesSent { get; init; }
		public long BytesReceived { get; init; }
		public int ConnectionErrors { get; init; }
		public int Timeouts { get; init; }
		public double AverageLatencyMs { get; init; }
	}

	private sealed class TransportCircuitBreaker(int failureThreshold)
	{
		private int _failureCount;

		public bool IsOpen => _failureCount >= failureThreshold;
		public bool CanSend => !IsOpen;

		public void RecordFailure()
		{
			_ = Interlocked.Increment(ref _failureCount);
		}

		public void Reset()
		{
			_ = Interlocked.Exchange(ref _failureCount, 0);
		}
	}
}
