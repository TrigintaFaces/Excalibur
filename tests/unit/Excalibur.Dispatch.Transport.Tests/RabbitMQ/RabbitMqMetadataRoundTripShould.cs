// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly -- FakeItEasy .Returns() stores ValueTask

using System.Text;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.Logging.Abstractions;

using RabbitMQ.Client;

using RabbitMqBasicProperties = RabbitMQ.Client.BasicProperties;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ;

/// <summary>
/// Verifies that all metadata fields survive a RabbitMQ send-then-receive round trip.
/// Tests cover native property mapping on the sender side, reconstruction on the receiver side,
/// missing-metadata resilience, custom header preservation, byte-array UTF-8 decoding,
/// and per-message metadata isolation in batch sends.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Platform")]
[Trait("Category", "MetadataRoundTrip")]
public sealed class RabbitMqMetadataRoundTripShould : IAsyncDisposable
{
	private const string TestExchange = "test-exchange";
	private const string TestRoutingKey = "test-routing-key";
	private const string TestQueueName = "test-queue";
	private const string TestSource = "test-queue";

	private static readonly byte[] TestBody = Encoding.UTF8.GetBytes("test-body");

	private readonly IChannel _fakeChannel;
	private readonly RabbitMqTransportSender _sender;
	private readonly RabbitMqTransportReceiver _receiver;

	public RabbitMqMetadataRoundTripShould()
	{
		_fakeChannel = A.Fake<IChannel>();

		_sender = new RabbitMqTransportSender(
			_fakeChannel,
			destination: TestQueueName,
			exchange: TestExchange,
			defaultRoutingKey: TestRoutingKey,
			logger: NullLogger<RabbitMqTransportSender>.Instance);

		_receiver = new RabbitMqTransportReceiver(
			_fakeChannel,
			source: TestSource,
			queueName: TestQueueName,
			logger: NullLogger<RabbitMqTransportReceiver>.Instance);
	}

	#region Test 1: AllMetadataFields_MappedToNativeProperties

	[Fact]
	public async Task AllMetadataFields_MappedToNativeProperties()
	{
		// Arrange
		RabbitMqBasicProperties? capturedProps = null;

		A.CallTo(() => _fakeChannel.BasicPublishAsync(
				A<string>._, A<string>._, A<bool>._,
				A<RabbitMqBasicProperties>._, A<ReadOnlyMemory<byte>>._, A<CancellationToken>._))
			.Invokes((string _, string _, bool _, RabbitMqBasicProperties props, ReadOnlyMemory<byte> _, CancellationToken _) =>
				capturedProps = props)
			.Returns(ValueTask.CompletedTask);

		var message = new TransportMessage
		{
			Id = "msg-001",
			Body = TestBody,
			ContentType = "application/json",
			MessageType = "OrderCreated",
			CorrelationId = "corr-abc",
			CausationId = "cause-xyz",
			Subject = "orders.created",
		};

		// Act
		await _sender.SendAsync(message, CancellationToken.None);

		// Assert
		capturedProps.ShouldNotBeNull();

		// Native RabbitMQ property mappings
		capturedProps.MessageId.ShouldBe("msg-001");
		capturedProps.ContentType.ShouldBe("application/json");
		capturedProps.Type.ShouldBe("OrderCreated");
		capturedProps.CorrelationId.ShouldBe("corr-abc");

		// Header-based mappings
		capturedProps.Headers.ShouldNotBeNull();
		capturedProps.Headers.ShouldContainKey("causation-id");
		capturedProps.Headers["causation-id"].ShouldBe("cause-xyz");
		capturedProps.Headers.ShouldContainKey("subject");
		capturedProps.Headers["subject"].ShouldBe("orders.created");
	}

	#endregion

	#region Test 2: AllMetadataFields_ReconstructedFromNativeProperties

	[Fact]
	public async Task AllMetadataFields_ReconstructedFromNativeProperties()
	{
		// Arrange
		var headers = new Dictionary<string, object?>
		{
			["causation-id"] = Encoding.UTF8.GetBytes("cause-xyz"),
			["subject"] = Encoding.UTF8.GetBytes("orders.created"),
			["custom-header"] = Encoding.UTF8.GetBytes("custom-value"),
		};

		var props = new RabbitMqBasicProperties
		{
			MessageId = "msg-001",
			ContentType = "application/json",
			Type = "OrderCreated",
			CorrelationId = "corr-abc",
			Headers = headers,
		};

		var getResult = new BasicGetResult(
			deliveryTag: 42,
			redelivered: false,
			exchange: TestExchange,
			routingKey: TestRoutingKey,
			messageCount: 0,
			basicProperties: props,
			body: TestBody);

		var callCount = 0;
		A.CallTo(() => _fakeChannel.BasicGetAsync(TestQueueName, false, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				callCount++;
				return callCount == 1
					? Task.FromResult<BasicGetResult?>(getResult)
					: Task.FromResult<BasicGetResult?>(null);
			});

		// Act
		var messages = await _receiver.ReceiveAsync(10, CancellationToken.None);

		// Assert
		messages.ShouldNotBeEmpty();
		var received = messages[0];

		received.Id.ShouldBe("msg-001");
		received.ContentType.ShouldBe("application/json");
		received.MessageType.ShouldBe("OrderCreated");
		received.CorrelationId.ShouldBe("corr-abc");
		received.Subject.ShouldBe("orders.created");

		// CausationId goes into Properties (TransportReceivedMessage has no CausationId property)
		received.Properties.ShouldContainKey("causation-id");
		(received.Properties["causation-id"] as string).ShouldBe("cause-xyz");

		// Custom headers survive
		received.Properties.ShouldContainKey("custom-header");
		(received.Properties["custom-header"] as string).ShouldBe("custom-value");
	}

	#endregion

	#region Test 3: MissingMetadata_DoNotCrashReceiver

	[Fact]
	public async Task MissingMetadata_DoNotCrashReceiver()
	{
		// Arrange -- BasicProperties with no headers, no MessageId, no Type, etc.
		var props = new RabbitMqBasicProperties();

		var getResult = new BasicGetResult(
			deliveryTag: 1,
			redelivered: false,
			exchange: string.Empty,
			routingKey: string.Empty,
			messageCount: 0,
			basicProperties: props,
			body: TestBody);

		var callCount = 0;
		A.CallTo(() => _fakeChannel.BasicGetAsync(TestQueueName, false, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				callCount++;
				return callCount == 1
					? Task.FromResult<BasicGetResult?>(getResult)
					: Task.FromResult<BasicGetResult?>(null);
			});

		// Act -- should not throw
		var messages = await _receiver.ReceiveAsync(10, CancellationToken.None);

		// Assert
		messages.ShouldNotBeEmpty();
		var received = messages[0];

		// With no MessageId, the receiver falls back to receipt handle
		received.Id.ShouldNotBeNullOrWhiteSpace();
		received.ContentType.ShouldBeNull();
		received.MessageType.ShouldBeNull();
		received.CorrelationId.ShouldBeNull();
		received.Subject.ShouldBeNull();
		received.Source.ShouldBe(TestSource);
	}

	#endregion

	#region Test 4: CustomHeaders_SurviveRoundTrip

	[Fact]
	public async Task CustomHeaders_SurviveRoundTrip()
	{
		// Arrange -- capture what the sender publishes, then feed it to the receiver
		RabbitMqBasicProperties? capturedProps = null;
		ReadOnlyMemory<byte> capturedBody = default;

		A.CallTo(() => _fakeChannel.BasicPublishAsync(
				A<string>._, A<string>._, A<bool>._,
				A<RabbitMqBasicProperties>._, A<ReadOnlyMemory<byte>>._, A<CancellationToken>._))
			.Invokes((string _, string _, bool _, RabbitMqBasicProperties props, ReadOnlyMemory<byte> body, CancellationToken _) =>
			{
				capturedProps = props;
				capturedBody = body;
			})
			.Returns(ValueTask.CompletedTask);

		var message = new TransportMessage
		{
			Id = "msg-custom",
			Body = TestBody,
			Properties =
			{
				["tenant-id"] = "acme-corp",
				["trace-parent"] = "00-abcdef1234567890-1234567890abcdef-01",
				["x-retry-count"] = "3",
			},
		};

		// Act (send)
		await _sender.SendAsync(message, CancellationToken.None);

		// Verify sender mapped custom headers (non-dispatch.* prefix)
		capturedProps.ShouldNotBeNull();
		capturedProps.Headers.ShouldNotBeNull();
		capturedProps.Headers.ShouldContainKey("tenant-id");
		capturedProps.Headers.ShouldContainKey("trace-parent");
		capturedProps.Headers.ShouldContainKey("x-retry-count");

		// Now simulate the receive side: convert string values to byte[] as RabbitMQ does
		var receiverHeaders = new Dictionary<string, object?>();
		foreach (var kvp in capturedProps.Headers)
		{
			receiverHeaders[kvp.Key] = kvp.Value is string s
				? Encoding.UTF8.GetBytes(s)
				: kvp.Value;
		}

		var receiverProps = new RabbitMqBasicProperties
		{
			MessageId = capturedProps.MessageId,
			ContentType = capturedProps.ContentType,
			CorrelationId = capturedProps.CorrelationId,
			Type = capturedProps.Type,
			Headers = receiverHeaders,
		};

		var getResult = new BasicGetResult(
			deliveryTag: 10,
			redelivered: false,
			exchange: TestExchange,
			routingKey: TestRoutingKey,
			messageCount: 0,
			basicProperties: receiverProps,
			body: capturedBody);

		var callCount = 0;
		A.CallTo(() => _fakeChannel.BasicGetAsync(TestQueueName, false, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				callCount++;
				return callCount == 1
					? Task.FromResult<BasicGetResult?>(getResult)
					: Task.FromResult<BasicGetResult?>(null);
			});

		// Act (receive)
		var received = await _receiver.ReceiveAsync(10, CancellationToken.None);

		// Assert -- custom headers round-tripped
		received.ShouldNotBeEmpty();
		var msg = received[0];
		(msg.Properties["tenant-id"] as string).ShouldBe("acme-corp");
		(msg.Properties["trace-parent"] as string).ShouldBe("00-abcdef1234567890-1234567890abcdef-01");
		(msg.Properties["x-retry-count"] as string).ShouldBe("3");
	}

	#endregion

	#region Test 5: ByteArrayHeaders_DecodedCorrectly

	[Fact]
	public async Task ByteArrayHeaders_DecodedCorrectly()
	{
		// Arrange -- RabbitMQ internally stores header values as byte[].
		// The receiver must decode these to UTF-8 strings.
		var unicodeValue = "Benutzerkonto geloescht - Auftrag #12345";
		var headers = new Dictionary<string, object?>
		{
			["reason"] = Encoding.UTF8.GetBytes(unicodeValue),
			["empty-header"] = Encoding.UTF8.GetBytes(string.Empty),
			["numeric-header"] = 42,  // Non-byte[] header value (int)
		};

		var props = new RabbitMqBasicProperties
		{
			MessageId = "msg-bytes",
			Headers = headers,
		};

		var getResult = new BasicGetResult(
			deliveryTag: 5,
			redelivered: false,
			exchange: TestExchange,
			routingKey: TestRoutingKey,
			messageCount: 0,
			basicProperties: props,
			body: TestBody);

		var callCount = 0;
		A.CallTo(() => _fakeChannel.BasicGetAsync(TestQueueName, false, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				callCount++;
				return callCount == 1
					? Task.FromResult<BasicGetResult?>(getResult)
					: Task.FromResult<BasicGetResult?>(null);
			});

		// Act
		var messages = await _receiver.ReceiveAsync(10, CancellationToken.None);

		// Assert
		messages.ShouldNotBeEmpty();
		var received = messages[0];

		// byte[] should be decoded to UTF-8 string
		(received.Properties["reason"] as string).ShouldBe(unicodeValue);
		(received.Properties["empty-header"] as string).ShouldBe(string.Empty);

		// Non-byte[] values use ToString() fallback
		(received.Properties["numeric-header"] as string).ShouldBe("42");
	}

	#endregion

	#region Test 6: BatchSend_PreservesPerMessageMetadata

	[Fact]
	public async Task BatchSend_PreservesPerMessageMetadata()
	{
		// Arrange -- capture properties for each publish call
		var capturedPropsList = new List<RabbitMqBasicProperties>();

		A.CallTo(() => _fakeChannel.BasicPublishAsync(
				A<string>._, A<string>._, A<bool>._,
				A<RabbitMqBasicProperties>._, A<ReadOnlyMemory<byte>>._, A<CancellationToken>._))
			.Invokes((string _, string _, bool _, RabbitMqBasicProperties props, ReadOnlyMemory<byte> _, CancellationToken _) =>
				capturedPropsList.Add(props))
			.Returns(ValueTask.CompletedTask);

		var messages = new List<TransportMessage>
		{
			new()
			{
				Id = "batch-1",
				Body = Encoding.UTF8.GetBytes("body-1"),
				ContentType = "application/json",
				MessageType = "OrderCreated",
				CorrelationId = "corr-1",
				CausationId = "cause-1",
				Subject = "orders.created",
			},
			new()
			{
				Id = "batch-2",
				Body = Encoding.UTF8.GetBytes("body-2"),
				ContentType = "text/plain",
				MessageType = "OrderShipped",
				CorrelationId = "corr-2",
				CausationId = "cause-2",
				Subject = "orders.shipped",
			},
			new()
			{
				Id = "batch-3",
				Body = Encoding.UTF8.GetBytes("body-3"),
				ContentType = "application/xml",
				MessageType = "OrderCancelled",
				CorrelationId = "corr-3",
				// No CausationId or Subject for this one
			},
		};

		// Act
		var result = await _sender.SendBatchAsync(messages, CancellationToken.None);

		// Assert -- batch result
		result.TotalMessages.ShouldBe(3);
		result.SuccessCount.ShouldBe(3);
		result.FailureCount.ShouldBe(0);

		capturedPropsList.Count.ShouldBe(3);

		// Message 1
		capturedPropsList[0].MessageId.ShouldBe("batch-1");
		capturedPropsList[0].ContentType.ShouldBe("application/json");
		capturedPropsList[0].Type.ShouldBe("OrderCreated");
		capturedPropsList[0].CorrelationId.ShouldBe("corr-1");
		capturedPropsList[0].Headers.ShouldContainKey("causation-id");
		capturedPropsList[0].Headers["causation-id"].ShouldBe("cause-1");
		capturedPropsList[0].Headers.ShouldContainKey("subject");
		capturedPropsList[0].Headers["subject"].ShouldBe("orders.created");

		// Message 2
		capturedPropsList[1].MessageId.ShouldBe("batch-2");
		capturedPropsList[1].ContentType.ShouldBe("text/plain");
		capturedPropsList[1].Type.ShouldBe("OrderShipped");
		capturedPropsList[1].CorrelationId.ShouldBe("corr-2");
		capturedPropsList[1].Headers.ShouldContainKey("causation-id");
		capturedPropsList[1].Headers["causation-id"].ShouldBe("cause-2");
		capturedPropsList[1].Headers.ShouldContainKey("subject");
		capturedPropsList[1].Headers["subject"].ShouldBe("orders.shipped");

		// Message 3 -- no CausationId or Subject, so headers should not contain them
		capturedPropsList[2].MessageId.ShouldBe("batch-3");
		capturedPropsList[2].ContentType.ShouldBe("application/xml");
		capturedPropsList[2].Type.ShouldBe("OrderCancelled");
		capturedPropsList[2].CorrelationId.ShouldBe("corr-3");
		capturedPropsList[2].Headers.ShouldNotContainKey("causation-id");
		capturedPropsList[2].Headers.ShouldNotContainKey("subject");
	}

	#endregion

	#region Test 7: DispatchPrefixHeaders_ExcludedFromRabbitMqHeaders

	[Fact]
	public async Task DispatchPrefixHeaders_ExcludedFromRabbitMqHeaders()
	{
		// Arrange -- "dispatch.*" properties are internal and should NOT be copied to RabbitMQ headers
		RabbitMqBasicProperties? capturedProps = null;

		A.CallTo(() => _fakeChannel.BasicPublishAsync(
				A<string>._, A<string>._, A<bool>._,
				A<RabbitMqBasicProperties>._, A<ReadOnlyMemory<byte>>._, A<CancellationToken>._))
			.Invokes((string _, string _, bool _, RabbitMqBasicProperties props, ReadOnlyMemory<byte> _, CancellationToken _) =>
				capturedProps = props)
			.Returns(ValueTask.CompletedTask);

		var message = new TransportMessage
		{
			Id = "msg-filter",
			Body = TestBody,
			Properties =
			{
				["dispatch.ordering_key"] = "partition-1",
				["dispatch.dedup_id"] = "dedup-001",
				["allowed-header"] = "should-appear",
			},
		};

		// Act
		await _sender.SendAsync(message, CancellationToken.None);

		// Assert
		capturedProps.ShouldNotBeNull();
		capturedProps.Headers.ShouldNotBeNull();

		// dispatch.* headers excluded
		capturedProps.Headers.ShouldNotContainKey("dispatch.ordering_key");
		capturedProps.Headers.ShouldNotContainKey("dispatch.dedup_id");

		// Non-dispatch header included
		capturedProps.Headers.ShouldContainKey("allowed-header");
		capturedProps.Headers["allowed-header"].ShouldBe("should-appear");
	}

	#endregion

	#region Test 8: NullContentType_DefaultsToOctetStream

	[Fact]
	public async Task NullContentType_DefaultsToOctetStream()
	{
		// Arrange
		RabbitMqBasicProperties? capturedProps = null;

		A.CallTo(() => _fakeChannel.BasicPublishAsync(
				A<string>._, A<string>._, A<bool>._,
				A<RabbitMqBasicProperties>._, A<ReadOnlyMemory<byte>>._, A<CancellationToken>._))
			.Invokes((string _, string _, bool _, RabbitMqBasicProperties props, ReadOnlyMemory<byte> _, CancellationToken _) =>
				capturedProps = props)
			.Returns(ValueTask.CompletedTask);

		var message = new TransportMessage
		{
			Id = "msg-no-ct",
			Body = TestBody,
			ContentType = null,
		};

		// Act
		await _sender.SendAsync(message, CancellationToken.None);

		// Assert
		capturedProps.ShouldNotBeNull();
		capturedProps.ContentType.ShouldBe("application/octet-stream");
	}

	#endregion

	#region Test 9: NullMessageType_OmittedFromBasicProperties

	[Fact]
	public async Task NullMessageType_OmittedFromBasicProperties()
	{
		// Arrange
		RabbitMqBasicProperties? capturedProps = null;

		A.CallTo(() => _fakeChannel.BasicPublishAsync(
				A<string>._, A<string>._, A<bool>._,
				A<RabbitMqBasicProperties>._, A<ReadOnlyMemory<byte>>._, A<CancellationToken>._))
			.Invokes((string _, string _, bool _, RabbitMqBasicProperties props, ReadOnlyMemory<byte> _, CancellationToken _) =>
				capturedProps = props)
			.Returns(ValueTask.CompletedTask);

		var message = new TransportMessage
		{
			Id = "msg-no-type",
			Body = TestBody,
			MessageType = null,
		};

		// Act
		await _sender.SendAsync(message, CancellationToken.None);

		// Assert
		capturedProps.ShouldNotBeNull();

		// BasicProperties.Type should remain null/default when MessageType is null
		capturedProps.Type.ShouldBeNull();
	}

	#endregion

	public async ValueTask DisposeAsync()
	{
		await _sender.DisposeAsync().ConfigureAwait(false);
		await _receiver.DisposeAsync().ConfigureAwait(false);
		await _fakeChannel.DisposeAsync().ConfigureAwait(false);
	}
}
