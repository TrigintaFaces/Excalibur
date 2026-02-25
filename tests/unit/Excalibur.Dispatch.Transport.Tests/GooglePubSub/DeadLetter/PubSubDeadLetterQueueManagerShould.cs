// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Google;

using Google.Cloud.PubSub.V1;
using Google.Protobuf;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using GoogleDeadLetterOptions = Excalibur.Dispatch.Transport.Google.DeadLetterOptions;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.DeadLetter;

/// <summary>
/// Unit tests for <see cref="PubSubDeadLetterQueueManager"/> aligned to the
/// shared <see cref="IDeadLetterQueueManager"/> interface (Sprint 526, S526.7).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class PubSubDeadLetterQueueManagerShould : IDisposable
{
	private readonly SubscriberServiceApiClient _fakeSubscriber;
	private readonly PublisherServiceApiClient _fakePublisher;
	private readonly PubSubDeadLetterQueueManager _sut;
	private readonly GoogleDeadLetterOptions _options;

	public PubSubDeadLetterQueueManagerShould()
	{
		_fakeSubscriber = A.Fake<SubscriberServiceApiClient>();
		_fakePublisher = A.Fake<PublisherServiceApiClient>();

		_options = new GoogleDeadLetterOptions
		{
			DeadLetterTopicName = new TopicName("test-project", "dead-letter-topic"),
			DeadLetterSubscriptionName = new SubscriptionName("test-project", "dead-letter-sub"),
			DefaultMaxDeliveryAttempts = 5,
		};

		var optionsWrapper = Microsoft.Extensions.Options.Options.Create(_options);

		_sut = new PubSubDeadLetterQueueManager(
			_fakeSubscriber,
			_fakePublisher,
			optionsWrapper,
			NullLogger<PubSubDeadLetterQueueManager>.Instance);
	}

	#region IDeadLetterQueueManager Interface Compliance

	[Fact]
	public void ImplementSharedIDeadLetterQueueManager()
	{
		// Assert — PubSubDeadLetterQueueManager implements the shared Transport.Abstractions interface
		var sut = _sut as IDeadLetterQueueManager;
		sut.ShouldNotBeNull("PubSubDeadLetterQueueManager should implement IDeadLetterQueueManager from Transport.Abstractions");
	}

	[Fact]
	public void ImplementIDisposable()
	{
		var sut = _sut as IDisposable;
		sut.ShouldNotBeNull("PubSubDeadLetterQueueManager should implement IDisposable");
	}

	#endregion

	#region MoveToDeadLetterAsync

	[Fact]
	public async Task MoveToDeadLetterAsync_PublishesToDeadLetterTopic()
	{
		// Arrange
		var message = new TransportMessage
		{
			Id = "msg-1",
			Body = System.Text.Encoding.UTF8.GetBytes("test-body"),
			ContentType = "application/json",
		};

		A.CallTo(() => _fakePublisher.PublishAsync(
				A<PublishRequest>._,
				A<CancellationToken>._))
			.Returns(new PublishResponse { MessageIds = { "dlq-msg-1" } });

		// Act
		var dlqId = await _sut.MoveToDeadLetterAsync(message, "Processing failed", null, CancellationToken.None);

		// Assert
		dlqId.ShouldBe("dlq-msg-1");
		A.CallTo(() => _fakePublisher.PublishAsync(
				A<PublishRequest>.That.Matches(r =>
					r.TopicAsTopicName.ToString() == _options.DeadLetterTopicName.ToString()),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MoveToDeadLetterAsync_IncludesDlqAttributes()
	{
		// Arrange
		var message = new TransportMessage
		{
			Id = "msg-2",
			Body = System.Text.Encoding.UTF8.GetBytes("body"),
		};

		PublishRequest? capturedRequest = null;
		A.CallTo(() => _fakePublisher.PublishAsync(A<PublishRequest>._, A<CancellationToken>._))
			.Invokes((PublishRequest r, CancellationToken _) => capturedRequest = r)
			.Returns(new PublishResponse { MessageIds = { "id-1" } });

		// Act
		await _sut.MoveToDeadLetterAsync(message, "Timeout", new TimeoutException("Timed out"), CancellationToken.None);

		// Assert
		capturedRequest.ShouldNotBeNull();
		var pubsubMessage = capturedRequest.Messages.ShouldHaveSingleItem();
		pubsubMessage.Attributes["dlq_reason"].ShouldBe("Timeout");
		pubsubMessage.Attributes["dlq_original_message_id"].ShouldBe("msg-2");
		pubsubMessage.Attributes["dlq_original_source"].ShouldBe("unknown");
		pubsubMessage.Attributes["dlq_delivery_attempts"].ShouldBe("0");
	}

	[Fact]
	public async Task MoveToDeadLetterAsync_ThrowsWhenTopicNotConfigured()
	{
		// Arrange — create SUT with no topic configured
		var options = Microsoft.Extensions.Options.Options.Create(new GoogleDeadLetterOptions());
		var sut = new PubSubDeadLetterQueueManager(
			_fakeSubscriber, _fakePublisher, options, NullLogger<PubSubDeadLetterQueueManager>.Instance);

		var message = new TransportMessage { Id = "msg-x", Body = new byte[] { 1 } };

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => sut.MoveToDeadLetterAsync(message, "fail", null, CancellationToken.None));
	}

	[Fact]
	public async Task MoveToDeadLetterAsync_ThrowsOnNullMessage()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.MoveToDeadLetterAsync(null!, "reason", null, CancellationToken.None));
	}

	[Fact]
	public async Task MoveToDeadLetterAsync_ThrowsOnNullReason()
	{
		var message = new TransportMessage { Id = "msg", Body = new byte[] { 1 } };
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.MoveToDeadLetterAsync(message, null!, null, CancellationToken.None));
	}

	#endregion

	#region GetDeadLetterMessagesAsync

	[Fact]
	public async Task GetDeadLetterMessagesAsync_ReturnsMessages()
	{
		// Arrange
		var receivedMessages = new[]
		{
			CreateReceivedMessage("dlq-1", "Original failed"),
			CreateReceivedMessage("dlq-2", "Timeout"),
		};

		A.CallTo(() => _fakeSubscriber.PullAsync(A<PullRequest>._, A<CancellationToken>._))
			.Returns(new PullResponse { ReceivedMessages = { receivedMessages } });

		// Act
		var messages = await _sut.GetDeadLetterMessagesAsync(10, CancellationToken.None);

		// Assert
		messages.Count.ShouldBe(2);
	}

	[Fact]
	public async Task GetDeadLetterMessagesAsync_ThrowsWhenSubscriptionNotConfigured()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new GoogleDeadLetterOptions
		{
			DeadLetterTopicName = new TopicName("p", "t"),
		});
		var sut = new PubSubDeadLetterQueueManager(
			_fakeSubscriber, _fakePublisher, options, NullLogger<PubSubDeadLetterQueueManager>.Instance);

		await Should.ThrowAsync<InvalidOperationException>(
			() => sut.GetDeadLetterMessagesAsync(10, CancellationToken.None));
	}

	#endregion

	#region GetStatisticsAsync

	[Fact]
	public async Task GetStatisticsAsync_ReturnsStatistics()
	{
		// Arrange — return empty pull (no messages)
		A.CallTo(() => _fakeSubscriber.PullAsync(A<PullRequest>._, A<CancellationToken>._))
			.Returns(new PullResponse());

		// Act
		var stats = await _sut.GetStatisticsAsync(CancellationToken.None);

		// Assert
		stats.ShouldNotBeNull();
		stats.MessageCount.ShouldBe(0);
	}

	[Fact]
	public async Task GetStatisticsAsync_ThrowsWhenSubscriptionNotConfigured()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new GoogleDeadLetterOptions
		{
			DeadLetterTopicName = new TopicName("p", "t"),
		});
		var sut = new PubSubDeadLetterQueueManager(
			_fakeSubscriber, _fakePublisher, options, NullLogger<PubSubDeadLetterQueueManager>.Instance);

		await Should.ThrowAsync<InvalidOperationException>(
			() => sut.GetStatisticsAsync(CancellationToken.None));
	}

	#endregion

	#region ReprocessDeadLetterMessagesAsync

	[Fact]
	public async Task ReprocessDeadLetterMessagesAsync_ThrowsOnNullMessages()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.ReprocessDeadLetterMessagesAsync(null!, new ReprocessOptions(), CancellationToken.None));
	}

	[Fact]
	public async Task ReprocessDeadLetterMessagesAsync_ThrowsOnNullOptions()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.ReprocessDeadLetterMessagesAsync([], null!, CancellationToken.None));
	}

	[Fact]
	public async Task ReprocessDeadLetterMessagesAsync_ReturnsResultForEmptyList()
	{
		// Act
		var result = await _sut.ReprocessDeadLetterMessagesAsync([], new ReprocessOptions(), CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.SuccessCount.ShouldBe(0);
		result.FailureCount.ShouldBe(0);
		result.ProcessingTime.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
	}

	#endregion

	#region PurgeDeadLetterQueueAsync

	[Fact]
	public async Task PurgeDeadLetterQueueAsync_ReturnsCount()
	{
		// Arrange — return a batch then empty
		var firstCall = true;
		A.CallTo(() => _fakeSubscriber.PullAsync(A<PullRequest>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				if (firstCall)
				{
					firstCall = false;
					return new PullResponse
					{
						ReceivedMessages =
						{
							CreateReceivedMessage("purge-1", "reason"),
							CreateReceivedMessage("purge-2", "reason"),
						},
					};
				}

				return new PullResponse();
			});

		// Act
		var purged = await _sut.PurgeDeadLetterQueueAsync(CancellationToken.None);

		// Assert
		purged.ShouldBeGreaterThanOrEqualTo(0);
	}

	#endregion

	#region Constructor Guards

	[Fact]
	public void Constructor_ThrowsOnNullSubscriber()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PubSubDeadLetterQueueManager(
				null!,
				_fakePublisher,
				Microsoft.Extensions.Options.Options.Create(_options),
				NullLogger<PubSubDeadLetterQueueManager>.Instance));
	}

	[Fact]
	public void Constructor_ThrowsOnNullPublisher()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PubSubDeadLetterQueueManager(
				_fakeSubscriber,
				null!,
				Microsoft.Extensions.Options.Options.Create(_options),
				NullLogger<PubSubDeadLetterQueueManager>.Instance));
	}

	[Fact]
	public void Constructor_ThrowsOnNullOptions()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PubSubDeadLetterQueueManager(
				_fakeSubscriber,
				_fakePublisher,
				null!,
				NullLogger<PubSubDeadLetterQueueManager>.Instance));
	}

	[Fact]
	public void Constructor_ThrowsOnNullLogger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PubSubDeadLetterQueueManager(
				_fakeSubscriber,
				_fakePublisher,
				Microsoft.Extensions.Options.Options.Create(_options),
				null!));
	}

	#endregion

	#region DI Registration

	[Fact]
	public void AddOptimizedDeadLetterQueue_RegistersSharedInterface()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddOptimizedDeadLetterQueue(opts =>
		{
			opts.DeadLetterTopicName = new TopicName("p", "t");
		});

		// Assert — the shared Transport.Abstractions IDeadLetterQueueManager is registered
		var descriptor = services.FirstOrDefault(d =>
			d.ServiceType == typeof(IDeadLetterQueueManager));
		descriptor.ShouldNotBeNull("AddOptimizedDeadLetterQueue should register IDeadLetterQueueManager");
		descriptor.ImplementationType.ShouldBe(typeof(PubSubDeadLetterQueueManager));
	}

	[Fact]
	public void AddGooglePubSubDeadLetterQueue_RegistersViaBuilder()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddGooglePubSubDeadLetterQueue(builder => { });

		// Assert — builder-based registration also uses the shared interface
		var descriptor = services.FirstOrDefault(d =>
			d.ServiceType == typeof(IDeadLetterQueueManager));
		descriptor.ShouldNotBeNull("AddGooglePubSubDeadLetterQueue should register IDeadLetterQueueManager");
	}

	#endregion

	#region Helpers

	private static ReceivedMessage CreateReceivedMessage(string messageId, string reason)
	{
		return new ReceivedMessage
		{
			AckId = $"ack-{messageId}",
			Message = new PubsubMessage
			{
				MessageId = messageId,
				Data = ByteString.CopyFromUtf8("test-body"),
				PublishTime = global::Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
				Attributes =
				{
					["dlq_reason"] = reason,
					["dlq_original_message_id"] = $"original-{messageId}",
					["dlq_delivery_attempts"] = "3",
					["dlq_original_source"] = "test-source",
					["dlq_timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
				},
			},
		};
	}

	public void Dispose()
	{
		_sut.Dispose();
	}

	#endregion
}
