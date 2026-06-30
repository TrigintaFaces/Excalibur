// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CloudNative.CloudEvents;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Tests.Conformance.Transport;

/// <summary>
/// Base class for transport conformance tests.
/// All transport implementations MUST pass this test suite to ensure consistent behavior.
/// Validates requirements: R2.1-R2.8, R2.15-R2.18, R4.5, R15.2, R15.7, T10.27, T10.34.
/// </summary>
/// <typeparam name="TSender">The transport sender type.</typeparam>
/// <typeparam name="TReceiver">The transport receiver type.</typeparam>
public abstract class TransportConformanceTestBase<TSender, TReceiver> : IAsyncLifetime
	where TSender : IChannelSender
	where TReceiver : IChannelReceiver
{
	/// <summary>
	/// Default timeout for receive operations to prevent tests from hanging indefinitely.
	/// </summary>
	// Generous, CI-scaled window so transport round-trips complete deterministically under heavy
	// TestContainers load (notably Kafka consumer-group rebalance / partition assignment, which can take
	// well over 30s when many containers contend on a CI runner). Receivers block until the message
	// arrives, so the happy path returns immediately; only a genuine delivery failure waits the full window.
	private static readonly TimeSpan ReceiveTimeout = global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(60));

	/// <summary>
	/// Caches Docker availability per closed generic type (e.g., Kafka, RabbitMQ).
	/// Once init fails for a transport, all remaining tests in that class skip immediately
	/// instead of each waiting for a 30-second timeout. null = not yet checked.
	/// </summary>
	private static bool? s_dockerAvailabilityChecked;

	private bool _dockerAvailable;

	protected TSender? Sender { get; private set; }
	protected TReceiver? Receiver { get; private set; }
	protected IDeadLetterQueueManager? DlqManager { get; private set; }

	/// <summary>
	/// Optional advanced conformance capabilities (header-surfacing, CloudEvents binding, ack/nack
	/// redelivery, filtering) the transport supports beyond the body-only send/receive surface. A deriver
	/// returns a provider to opt in; the capability-gated facts then make real, RED-able assertions against
	/// it (bd-urttf7). Returns null when the transport supports only body-only send/receive — capability-gated
	/// facts then no-op for that transport rather than asserting falsely.
	/// </summary>
	protected virtual ITransportConformanceCapabilities? AdvancedCapabilities => null;

	public async ValueTask InitializeAsync()
	{
		// Fast-path: if a previous test already determined Docker is unavailable, skip immediately
		if (s_dockerAvailabilityChecked == false)
		{
			_dockerAvailable = false;
			return;
		}

		try
		{
			// Timeout initialization to prevent indefinite hangs when Docker is unavailable
			var initTask = InitializeTransportAsync();
			var completedTask = await Task.WhenAny(initTask, Task.Delay(TimeSpan.FromSeconds(30))).ConfigureAwait(false);

			if (completedTask != initTask)
			{
				Console.WriteLine("Docker/transport initialization timed out after 30 seconds");
				s_dockerAvailabilityChecked = false;
				_dockerAvailable = false;
				return;
			}

			// Propagate any exception from the init task
			await initTask.ConfigureAwait(false);
			s_dockerAvailabilityChecked = true;
			_dockerAvailable = true;
		}
		catch (Exception ex) when (ex is not OutOfMemoryException)
		{
			Console.WriteLine($"Docker/transport initialization failed: {ex.Message}");
			s_dockerAvailabilityChecked = false;
			_dockerAvailable = false;
		}
	}

	private async Task InitializeTransportAsync()
	{
		Sender = await CreateSenderAsync().ConfigureAwait(false);
		Receiver = await CreateReceiverAsync().ConfigureAwait(false);
		DlqManager = await CreateDlqManagerAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Returns true if the transport infrastructure (Docker) is available.
	/// Tests should call this at the start and return early if false.
	/// </summary>
	protected bool IsTransportAvailable() => _dockerAvailable;

	public async ValueTask DisposeAsync()
	{
		if (_dockerAvailable)
		{
			await DisposeTransportAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Creates and initializes the transport sender.
	/// </summary>
	protected abstract Task<TSender> CreateSenderAsync();

	/// <summary>
	/// Creates and initializes the transport receiver.
	/// </summary>
	protected abstract Task<TReceiver> CreateReceiverAsync();

	/// <summary>
	/// Creates and initializes the dead-letter queue manager.
	/// Optional - return null if DLQ is not supported by this transport.
	/// </summary>
	protected abstract Task<IDeadLetterQueueManager?> CreateDlqManagerAsync();

	/// <summary>
	/// Disposes transport resources.
	/// </summary>
	protected abstract Task DisposeTransportAsync();

	#region Core Conformance Tests

	/// <summary>
	/// R2.1: Transport MUST support basic send and receive round-trip.
	/// </summary>
	[Fact]
	public virtual async Task Should_Send_And_Receive_Message_RoundTrip()
	{
		if (!IsTransportAvailable()) { return; }

		// Arrange
		var testMessage = new TestMessage
		{
			Id = Guid.NewGuid().ToString(),
			Content = "Test message content",
			Timestamp = DateTimeOffset.UtcNow
		};

		// Act
		using var cts = new CancellationTokenSource(ReceiveTimeout);
		await Sender.SendAsync(testMessage, cts.Token).ConfigureAwait(false);
		var received = await Receiver.ReceiveAsync<TestMessage>(cts.Token).ConfigureAwait(false);

		// Assert
		_ = received.ShouldNotBeNull();
		received.Id.ShouldBe(testMessage.Id);
		received.Content.ShouldBe(testMessage.Content);
		received.Timestamp.ShouldBe(testMessage.Timestamp, TimeSpan.FromMilliseconds(100));
	}

	/// <summary>
	/// R2.2: Transport MUST preserve message metadata (correlation IDs, custom headers).
	/// </summary>
	/// <remarks>
	/// This asserts metadata survives at the <b>body</b> level (the fields round-trip through serialization),
	/// which the harness can express. Asserting metadata on the <b>transport carrier</b> (Kafka
	/// <c>Headers</c> / RabbitMQ <c>BasicProperties</c>) requires a header-surfacing receive context the
	/// IChannelReceiver harness does not expose — tracked bd-liyait (umbrella Excalibur.Dispatch-urttf7).
	/// </remarks>
	[Fact]
	public virtual async Task Should_Preserve_Message_Metadata()
	{
		if (!IsTransportAvailable()) { return; }

		// Arrange
		var testMessage = new TestMessageWithMetadata
		{
			Id = Guid.NewGuid().ToString(),
			MessageId = Guid.NewGuid().ToString(),
			CorrelationId = Guid.NewGuid().ToString(),
			UserId = "test-user-123",
			TenantId = "tenant-456",
			Content = "Test content"
		};

		// Act
		using var cts = new CancellationTokenSource(ReceiveTimeout);
		await Sender.SendAsync(testMessage, cts.Token).ConfigureAwait(false);
		var received = await Receiver.ReceiveAsync<TestMessageWithMetadata>(cts.Token).ConfigureAwait(false);

		// Assert
		_ = received.ShouldNotBeNull();
		received.MessageId.ShouldBe(testMessage.MessageId);
		received.CorrelationId.ShouldBe(testMessage.CorrelationId);
		received.UserId.ShouldBe(testMessage.UserId);
		received.TenantId.ShouldBe(testMessage.TenantId);
	}

	/// <summary>
	/// R2.3, R9.1: Transport MUST handle concurrent messages without loss.
	/// </summary>
	[Fact]
	public virtual async Task Should_Handle_Concurrent_Messages()
	{
		if (!IsTransportAvailable()) { return; }

		// Arrange
		const int messageCount = 100;
		var sentMessages = new List<TestMessage>();
		var receivedMessages = new List<TestMessage>();

		for (int i = 0; i < messageCount; i++)
		{
			sentMessages.Add(new TestMessage
			{
				Id = Guid.NewGuid().ToString(),
				Content = $"Message {i}",
				Timestamp = DateTimeOffset.UtcNow
			});
		}

		// Act - Send concurrently
		var sendTasks = sentMessages.Select(msg =>
			Sender.SendAsync(msg, CancellationToken.None)).ToList();
		await Task.WhenAll(sendTasks).ConfigureAwait(false);

		// Act - Receive all messages (use the shared, CI-scaled receive window so a slow rebalance under
		// heavy TestContainers load does not truncate the loop -> deterministic).
		using var cts = new CancellationTokenSource(ReceiveTimeout);
		for (int i = 0; i < messageCount; i++)
		{
			var received = await Receiver.ReceiveAsync<TestMessage>(cts.Token).ConfigureAwait(false);
			if (received != null)
			{
				receivedMessages.Add(received);
			}
		}

		// Assert
		receivedMessages.Count.ShouldBe(messageCount);
		var receivedIds = receivedMessages.Select(m => m.Id).ToHashSet();
		var sentIds = sentMessages.Select(m => m.Id).ToHashSet();
		receivedIds.SetEquals(sentIds).ShouldBeTrue("All messages should be received with no duplicates");
	}

	/// <summary>
	/// R2.15: Transport MUST support message filtering capabilities.
	/// </summary>
	[Fact(Skip = "Conformance harness exposes no filtering API; a real filtering assertion is not expressible here. Transports that support filtering (e.g. Azure Service Bus, AWS SQS) should override with a real assertion once the harness exposes it — tracked bd-1rbj0a (umbrella Excalibur.Dispatch-urttf7).")]
	public virtual Task Should_Support_Message_Filtering() => Task.CompletedTask;

	/// <summary>
	/// R4.5: Transport MUST route poison messages to DLQ after retry exhaustion.
	/// </summary>
	[Fact]
	public virtual async Task Should_Handle_Poison_Messages()
	{
		if (!IsTransportAvailable()) { return; }

		if (DlqManager == null)
		{
			// Skip if DLQ not supported by this transport
			return;
		}

		// Arrange
		var poisonMessage = new TestMessage
		{
			Id = Guid.NewGuid().ToString(),
			Content = "Poison message",
			Timestamp = DateTimeOffset.UtcNow
		};

		// Act
		using var cts = new CancellationTokenSource(ReceiveTimeout);
		await Sender.SendAsync(poisonMessage, cts.Token).ConfigureAwait(false);
		var received = await Receiver.ReceiveAsync<TestMessage>(cts.Token).ConfigureAwait(false);

		// Simulate failure and DLQ routing
		_ = received.ShouldNotBeNull();
		var dlqId = await DlqManager.MoveToDeadLetterAsync(
			new TransportMessage { Id = received.Id },
			"MaxRetries",
			new InvalidOperationException("Simulated processing failure"),
			cts.Token).ConfigureAwait(false);

		// Assert
		dlqId.ShouldNotBeNullOrEmpty();

		var dlqMessages = await DlqManager.GetDeadLetterMessagesAsync(10, cts.Token).ConfigureAwait(false);
		dlqMessages.ShouldContain(m => m.OriginalMessage.Id == received.Id || m.OriginalMessage.Id == dlqId);
	}

	/// <summary>
	/// R15.2: Transport MUST support graceful shutdown and restart without errors.
	/// Verifies that the transport can cleanly shut down and resume operations.
	/// </summary>
	[Fact]
	public virtual async Task Should_Support_Graceful_Shutdown()
	{
		if (!IsTransportAvailable()) { return; }

		// Act - Trigger graceful shutdown
		await DisposeTransportAsync().ConfigureAwait(false);

		// Re-initialize transport
		await InitializeAsync().ConfigureAwait(false);

		if (!IsTransportAvailable()) { return; }

		// Verify transport is functional after restart by sending and receiving a new message
		var testMessage = new TestMessage
		{
			Id = Guid.NewGuid().ToString(),
			Content = "Post-restart verification",
			Timestamp = DateTimeOffset.UtcNow
		};

		using var cts = new CancellationTokenSource(ReceiveTimeout);
		await Sender.SendAsync(testMessage, cts.Token).ConfigureAwait(false);
		var received = await Receiver.ReceiveAsync<TestMessage>(cts.Token).ConfigureAwait(false);

		// Assert
		_ = received.ShouldNotBeNull();
		received.Id.ShouldBe(testMessage.Id);
	}

	/// <summary>
	/// R2.1, R4.3: Transport MUST guarantee at-least-once delivery semantics.
	/// </summary>
	/// <remarks>
	/// Skipped: proving at-least-once requires forcing a nack / crash-before-ack and asserting the message is
	/// redelivered, but the IChannelReceiver harness exposes no ack/nack — a single send/receive does NOT
	/// prove the guarantee (a transport with broken redelivery would still pass). Tracked: bd-5dox7c.
	/// </remarks>
	[Fact(Skip = "At-least-once redelivery needs ack/nack the conformance harness does not expose; a single send/receive is not an at-least-once proof — tracked bd-5dox7c (umbrella Excalibur.Dispatch-urttf7).")]
	public virtual async Task Should_Guarantee_At_Least_Once_Delivery()
	{
		if (!IsTransportAvailable()) { return; }

		// Arrange
		var testMessage = new TestMessage
		{
			Id = Guid.NewGuid().ToString(),
			Content = "At-least-once test",
			Timestamp = DateTimeOffset.UtcNow
		};

		// Act
		using var cts = new CancellationTokenSource(ReceiveTimeout);
		await Sender.SendAsync(testMessage, cts.Token).ConfigureAwait(false);

		// Receive first time
		var received1 = await Receiver.ReceiveAsync<TestMessage>(cts.Token).ConfigureAwait(false);
		_ = received1.ShouldNotBeNull();
		received1.Id.ShouldBe(testMessage.Id);

		// Simulate crash before ACK - message should be redelivered
		// (Implementation-specific behavior)
	}

	#endregion Core Conformance Tests

	#region CloudEvents Conformance Tests (T10.34, R15.7)

	/// <summary>
	/// T10.34: Transport MUST support CloudEvents structured format (application/cloudevents+json).
	/// </summary>
	[Fact(Skip = "Conformance harness has no CloudEvents protocol binding (no structured content-mode mapping); a POCO round-trip would pass even for a transport with zero CloudEvents support (false conformance) — tracked bd-jj4hx4 (umbrella Excalibur.Dispatch-urttf7).")]
	public virtual async Task Should_Support_CloudEvents_Structured_Format()
	{
		// Arrange
		var cloudEvent = new CloudEvent
		{
			Id = Guid.NewGuid().ToString(),
			Source = new Uri("https://example.com/test"),
			Type = "com.example.test.structured",
			DataContentType = "application/json",
			Data = new { Message = "Hello CloudEvents Structured" }
		};

		// Act & Assert
		// Note: Actual CloudEvents serialization depends on transport-specific mapper
		// Derived classes MUST override this test to implement CloudEvents support validation
		await Task.CompletedTask;
	}

	/// <summary>
	/// T10.34: Transport MUST support CloudEvents binary format (CE headers in transport metadata).
	/// </summary>
	[Fact(Skip = "Conformance harness has no CloudEvents binary binding (no ce- header mapping on the receiver); a real binary-format assertion is not expressible — tracked bd-jj4hx4 (umbrella Excalibur.Dispatch-urttf7).")]
	public virtual async Task Should_Support_CloudEvents_Binary_Format()
	{
		// Arrange
		var cloudEvent = new CloudEvent
		{
			Id = Guid.NewGuid().ToString(),
			Source = new Uri("https://example.com/test"),
			Type = "com.example.test.binary",
			DataContentType = "application/json",
			Data = new { Message = "Hello CloudEvents Binary" }
		};

		// Act & Assert
		// Note: Binary format maps CloudEvents attributes to transport headers (ce-id, ce-source, etc.)
		// Derived classes MUST override this test to implement CloudEvents support validation
		await Task.CompletedTask;
	}

	/// <summary>
	/// T10.34: Transport MUST preserve all CloudEvents required attributes.
	/// </summary>
	[Fact(Skip = "Conformance harness has no CloudEvents binding to surface CE attributes on receive; a real attribute-preservation assertion is not expressible — tracked bd-jj4hx4 (umbrella Excalibur.Dispatch-urttf7).")]
	public virtual async Task Should_Preserve_CloudEvents_Attributes()
	{
		// Required: id, source, specversion, type
		// Optional: datacontenttype, dataschema, subject, time

		var cloudEvent = new CloudEvent
		{
			Id = Guid.NewGuid().ToString(),
			Source = new Uri("https://example.com/test"),
			Type = "com.example.test.attributes",
			DataContentType = "application/json",
			Subject = "test/subject",
			Time = DateTimeOffset.UtcNow,
			Data = new { Message = "Test data" }
		};

		// Derived classes MUST implement CloudEvents round-trip validation
		await Task.CompletedTask;
	}

	/// <summary>
	/// T10.34: Transport MUST support CloudEvents round-trip without loss.
	/// </summary>
	[Fact(Skip = "Conformance harness has no CloudEvents binding; a semantic-equality round-trip assertion would pass for a transport with zero CE support (false conformance) — tracked bd-jj4hx4 (umbrella Excalibur.Dispatch-urttf7).")]
	public virtual async Task Should_RoundTrip_CloudEvents_Without_Loss()
	{
		// Arrange
		var cloudEvent = new CloudEvent
		{
			Id = Guid.NewGuid().ToString(),
			Source = new Uri("https://example.com/test"),
			Type = "com.example.test.roundtrip",
			DataContentType = "application/json",
			Data = new { Value = 42, Name = "Test" }
		};

		// CloudEvent → Transport → CloudEvent
		// Assert: semantic equality
		await Task.CompletedTask;
	}

	#endregion CloudEvents Conformance Tests (T10.34, R15.7)

	#region Performance Tests (R9.*)

	/// <summary>
	/// R9.1-R9.18: Transport SHOULD handle high throughput efficiently.
	/// </summary>
	[Fact(Skip = "Conformance harness has no throughput instrumentation; high-throughput SLO is a transport-specific SHOULD, not assertable from the body-only send/receive harness — tracked bd-lpkwjr (umbrella Excalibur.Dispatch-urttf7).")]
	public virtual async Task Should_Handle_High_Throughput()
	{
		// Note: Performance characteristics vary by transport
		// Derived classes MAY override to validate specific throughput SLOs
		await Task.CompletedTask;
	}

	/// <summary>
	/// R9.1-R9.18: Transport SHOULD maintain low latency under load.
	/// </summary>
	[Fact(Skip = "Conformance harness has no latency instrumentation; p95/p99 latency SLO is a transport-specific SHOULD, not assertable from the body-only send/receive harness — tracked bd-lpkwjr (umbrella Excalibur.Dispatch-urttf7).")]
	public virtual async Task Should_Maintain_Low_Latency()
	{
		// Note: Latency characteristics vary by transport
		// Derived classes MAY override to validate p95/p99 latency SLOs
		await Task.CompletedTask;
	}

	#endregion Performance Tests (R9.*)
}