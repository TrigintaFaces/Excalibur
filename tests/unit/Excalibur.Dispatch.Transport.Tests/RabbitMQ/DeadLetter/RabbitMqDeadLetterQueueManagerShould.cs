// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly — FakeItEasy .Returns() stores ValueTask

using System.Text;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using RabbitMQ.Client;

using RabbitMqBasicProperties = RabbitMQ.Client.BasicProperties;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ.DeadLetter;

/// <summary>
/// Unit tests for <see cref="RabbitMqDeadLetterQueueManager"/>.
/// Validates Move (DLX publish), Get (peek via BasicGet+Nack), Reprocess, Statistics, Purge, and error handling.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class RabbitMqDeadLetterQueueManagerShould : IDisposable
{
	private readonly IChannel _fakeChannel;
	private readonly RabbitMqDeadLetterQueueManager _sut;

	private static readonly byte[] TestBody = Encoding.UTF8.GetBytes("test-body");

	public RabbitMqDeadLetterQueueManagerShould()
	{
		_fakeChannel = A.Fake<IChannel>();

		var options = Microsoft.Extensions.Options.Options.Create(new RabbitMqDeadLetterOptions
		{
			Exchange = "dead-letters",
			QueueName = "dead-letter-queue",
			RoutingKey = "#",
			MaxBatchSize = 100,
		});

		_sut = new RabbitMqDeadLetterQueueManager(
			_fakeChannel,
			options,
			NullLogger<RabbitMqDeadLetterQueueManager>.Instance);
	}

	#region MoveToDeadLetterAsync

	[Fact]
	public async Task MoveToDeadLetterAsync_PublishesToDlxExchange()
	{
		// Arrange
		var message = new TransportMessage
		{
			Id = "msg-1",
			Body = TestBody,
			ContentType = "application/json",
		};

		// Act
		var dlqId = await _sut.MoveToDeadLetterAsync(message, "Processing failed", null, CancellationToken.None);

		// Assert
		dlqId.ShouldBe("msg-1");
		A.CallTo(() => _fakeChannel.BasicPublishAsync(
			"dead-letters",
			"#",
			A<bool>._,
			A<RabbitMqBasicProperties>.That.Matches(p => p.MessageId == "msg-1"),
			A<ReadOnlyMemory<byte>>._,
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MoveToDeadLetterAsync_IncludesExceptionHeaders_WhenProvided()
	{
		// Arrange
		var message = new TransportMessage { Id = "msg-ex", Body = TestBody };
		var exception = new InvalidOperationException("Boom");

		RabbitMqBasicProperties? capturedProps = null;
		A.CallTo(() => _fakeChannel.BasicPublishAsync(
			A<string>._, A<string>._, A<bool>._,
			A<RabbitMqBasicProperties>._, A<ReadOnlyMemory<byte>>._, A<CancellationToken>._))
			.Invokes((string _, string _, bool _, RabbitMqBasicProperties props, ReadOnlyMemory<byte> _, CancellationToken _) =>
				capturedProps = props)
			.Returns(ValueTask.CompletedTask);

		// Act
		await _sut.MoveToDeadLetterAsync(message, "Error", exception, CancellationToken.None);

		// Assert
		capturedProps.ShouldNotBeNull();
		capturedProps.Headers.ShouldNotBeNull();
		capturedProps.Headers["dlq_exception_type"].ShouldBe(typeof(InvalidOperationException).FullName);
		capturedProps.Headers["dlq_exception_message"].ShouldBe("Boom");
	}

	[Fact]
	public async Task MoveToDeadLetterAsync_ThrowsOnNullMessage()
	{
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.MoveToDeadLetterAsync(null!, "reason", null, CancellationToken.None));
	}

	[Fact]
	public async Task MoveToDeadLetterAsync_ThrowsOnEmptyReason()
	{
		var message = new TransportMessage { Id = "msg-1", Body = TestBody };

		await Should.ThrowAsync<ArgumentException>(() =>
			_sut.MoveToDeadLetterAsync(message, "", null, CancellationToken.None));

		await Should.ThrowAsync<ArgumentException>(() =>
			_sut.MoveToDeadLetterAsync(message, "   ", null, CancellationToken.None));
	}

	#endregion

	#region GetDeadLetterMessagesAsync

	[Fact]
	public async Task GetDeadLetterMessagesAsync_ReturnsMessages_UsingPeekSemantics()
	{
		// Arrange — BasicGetAsync returns a message, then null (end)
		var headers = new Dictionary<string, object?>
		{
			["dlq_reason"] = "Timeout",
			["dlq_moved_at"] = DateTimeOffset.UtcNow.AddMinutes(-3).ToString("O"),
			["dlq_original_source"] = "orders",
		};

		var props = new RabbitMqBasicProperties { MessageId = "msg-peek-1", ContentType = "application/json", Headers = headers };
		var getResult = new BasicGetResult(
			deliveryTag: 1,
			redelivered: false,
			exchange: "dead-letters",
			routingKey: "#",
			messageCount: 0,
			basicProperties: props,
			body: TestBody);

		var callCount = 0;
		A.CallTo(() => _fakeChannel.BasicGetAsync("dead-letter-queue", false, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				callCount++;
				return callCount == 1
					? Task.FromResult<BasicGetResult?>(getResult)
					: Task.FromResult<BasicGetResult?>(null);
			});

		// Act
		var result = await _sut.GetDeadLetterMessagesAsync(10, CancellationToken.None);

		// Assert
		result.Count.ShouldBe(1);
		result[0].OriginalMessage.Id.ShouldBe("msg-peek-1");
		result[0].Reason.ShouldBe("Timeout");

		// Verify non-destructive: BasicNack with requeue=true
		A.CallTo(() => _fakeChannel.BasicNackAsync(1UL, false, true, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GetDeadLetterMessagesAsync_ReturnsEmpty_WhenNoneAvailable()
	{
		// Arrange
		A.CallTo(() => _fakeChannel.BasicGetAsync(A<string>._, A<bool>._, A<CancellationToken>._))
			.Returns(Task.FromResult<BasicGetResult?>(null));

		// Act
		var result = await _sut.GetDeadLetterMessagesAsync(10, CancellationToken.None);

		// Assert
		result.ShouldBeEmpty();
	}

	#endregion

	#region ReprocessDeadLetterMessagesAsync

	[Fact]
	public async Task ReprocessDeadLetterMessagesAsync_PublishesToOriginalExchange()
	{
		// Arrange
		var dlqMessages = new[] { CreateDeadLetterMessage("msg-rp-1", "original-exchange") };
		var options = new ReprocessOptions { RetryDelay = TimeSpan.Zero };

		// Act
		var result = await _sut.ReprocessDeadLetterMessagesAsync(dlqMessages, options, CancellationToken.None);

		// Assert
		result.SuccessCount.ShouldBe(1);
		A.CallTo(() => _fakeChannel.BasicPublishAsync(
			"original-exchange",
			"#",
			A<bool>._,
			A<RabbitMqBasicProperties>.That.Matches(p => p.MessageId == "msg-rp-1"),
			A<ReadOnlyMemory<byte>>._,
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReprocessDeadLetterMessagesAsync_UsesTargetQueueOverride()
	{
		// Arrange
		var dlqMessages = new[] { CreateDeadLetterMessage("msg-1", "original-exchange") };
		var options = new ReprocessOptions
		{
			TargetQueue = "retry-exchange",
			RetryDelay = TimeSpan.Zero,
		};

		// Act
		var result = await _sut.ReprocessDeadLetterMessagesAsync(dlqMessages, options, CancellationToken.None);

		// Assert
		result.SuccessCount.ShouldBe(1);
		A.CallTo(() => _fakeChannel.BasicPublishAsync(
			"retry-exchange",
			A<string>._,
			A<bool>._,
			A<RabbitMqBasicProperties>._,
			A<ReadOnlyMemory<byte>>._,
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReprocessDeadLetterMessagesAsync_AppliesFilter()
	{
		// Arrange
		var dlqMessages = new[]
		{
			CreateDeadLetterMessage("msg-pass", "orders", "Timeout"),
			CreateDeadLetterMessage("msg-skip", "orders", "Poison"),
		};

		var options = new ReprocessOptions
		{
			MessageFilter = msg => msg.Reason == "Timeout",
			RetryDelay = TimeSpan.Zero,
		};

		// Act
		var result = await _sut.ReprocessDeadLetterMessagesAsync(dlqMessages, options, CancellationToken.None);

		// Assert
		result.SuccessCount.ShouldBe(1);
		result.SkippedCount.ShouldBe(1);
	}

	[Fact]
	public async Task ReprocessDeadLetterMessagesAsync_AppliesTransform()
	{
		// Arrange
		var transformedBody = Encoding.UTF8.GetBytes("transformed");
		ReadOnlyMemory<byte>? capturedBody = null;

		A.CallTo(() => _fakeChannel.BasicPublishAsync(
			A<string>._, A<string>._, A<bool>._,
			A<RabbitMqBasicProperties>._, A<ReadOnlyMemory<byte>>._, A<CancellationToken>._))
			.Invokes((string _, string _, bool _, RabbitMqBasicProperties _, ReadOnlyMemory<byte> body, CancellationToken _) =>
				capturedBody = body)
			.Returns(ValueTask.CompletedTask);

		var dlqMessages = new[] { CreateDeadLetterMessage("msg-t", "orders") };
		var options = new ReprocessOptions
		{
			MessageTransform = msg => new TransportMessage { Id = msg.Id, Body = transformedBody },
			RetryDelay = TimeSpan.Zero,
		};

		// Act
		await _sut.ReprocessDeadLetterMessagesAsync(dlqMessages, options, CancellationToken.None);

		// Assert
		capturedBody.ShouldNotBeNull();
		capturedBody.Value.ToArray().ShouldBe(transformedBody);
	}

	[Fact]
	public async Task ReprocessDeadLetterMessagesAsync_RespectsMaxMessages()
	{
		// Arrange
		var publishCount = 0;
		A.CallTo(() => _fakeChannel.BasicPublishAsync(
			A<string>._, A<string>._, A<bool>._,
			A<RabbitMqBasicProperties>._, A<ReadOnlyMemory<byte>>._, A<CancellationToken>._))
			.Invokes(() => publishCount++)
			.Returns(ValueTask.CompletedTask);

		var dlqMessages = new[]
		{
			CreateDeadLetterMessage("msg-1", "orders"),
			CreateDeadLetterMessage("msg-2", "orders"),
			CreateDeadLetterMessage("msg-3", "orders"),
		};

		var options = new ReprocessOptions { MaxMessages = 2, RetryDelay = TimeSpan.Zero };

		// Act
		var result = await _sut.ReprocessDeadLetterMessagesAsync(dlqMessages, options, CancellationToken.None);

		// Assert
		result.SuccessCount.ShouldBe(2);
		publishCount.ShouldBe(2);
	}

	[Fact]
	public async Task ReprocessDeadLetterMessagesAsync_TracksFailures()
	{
		// Arrange
		A.CallTo(() => _fakeChannel.BasicPublishAsync(
			A<string>._, A<string>._, A<bool>._,
			A<RabbitMqBasicProperties>._, A<ReadOnlyMemory<byte>>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("RabbitMQ unavailable"));

		var dlqMessages = new[] { CreateDeadLetterMessage("msg-fail", "orders") };
		var options = new ReprocessOptions { RetryDelay = TimeSpan.Zero };

		// Act
		var result = await _sut.ReprocessDeadLetterMessagesAsync(dlqMessages, options, CancellationToken.None);

		// Assert
		result.FailureCount.ShouldBe(1);
		result.SuccessCount.ShouldBe(0);
		result.Failures.Count.ShouldBe(1);
		result.Failures.First().Reason.ShouldBe("RabbitMQ unavailable");
	}

	[Fact]
	public async Task ReprocessDeadLetterMessagesAsync_RecordsProcessingTime()
	{
		// Arrange
		var dlqMessages = new[] { CreateDeadLetterMessage("msg-time", "orders") };
		var options = new ReprocessOptions { RetryDelay = TimeSpan.Zero };

		// Act
		var result = await _sut.ReprocessDeadLetterMessagesAsync(dlqMessages, options, CancellationToken.None);

		// Assert
		result.ProcessingTime.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
	}

	[Fact]
	public async Task ReprocessDeadLetterMessagesAsync_ThrowsOnNullMessages()
	{
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.ReprocessDeadLetterMessagesAsync(null!, new ReprocessOptions(), CancellationToken.None));
	}

	[Fact]
	public async Task ReprocessDeadLetterMessagesAsync_ThrowsOnNullOptions()
	{
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.ReprocessDeadLetterMessagesAsync([], null!, CancellationToken.None));
	}

	#endregion

	#region GetStatisticsAsync

	[Fact]
	public async Task GetStatisticsAsync_ReturnsMessageCount_FromQueueDeclarePassive()
	{
		// Arrange
		A.CallTo(() => _fakeChannel.QueueDeclarePassiveAsync("dead-letter-queue", A<CancellationToken>._))
			.Returns(new QueueDeclareOk("dead-letter-queue", 0, 0));

		A.CallTo(() => _fakeChannel.BasicGetAsync(A<string>._, A<bool>._, A<CancellationToken>._))
			.Returns(Task.FromResult<BasicGetResult?>(null));

		// Act
		var stats = await _sut.GetStatisticsAsync(CancellationToken.None);

		// Assert
		stats.ShouldNotBeNull();
		stats.MessageCount.ShouldBe(0);
		stats.GeneratedAt.ShouldBeGreaterThan(DateTimeOffset.MinValue);
	}

	#endregion

	#region PurgeDeadLetterQueueAsync

	[Fact]
	public async Task PurgeDeadLetterQueueAsync_CallsQueuePurge()
	{
		// Arrange
		A.CallTo(() => _fakeChannel.QueuePurgeAsync("dead-letter-queue", A<CancellationToken>._))
			.Returns(5U);

		// Act
		var purgedCount = await _sut.PurgeDeadLetterQueueAsync(CancellationToken.None);

		// Assert
		purgedCount.ShouldBe(5);
	}

	[Fact]
	public async Task PurgeDeadLetterQueueAsync_ReturnsZero_WhenEmpty()
	{
		// Arrange
		A.CallTo(() => _fakeChannel.QueuePurgeAsync("dead-letter-queue", A<CancellationToken>._))
			.Returns(0U);

		// Act
		var purgedCount = await _sut.PurgeDeadLetterQueueAsync(CancellationToken.None);

		// Assert
		purgedCount.ShouldBe(0);
	}

	#endregion

	#region DI Registration

	[Fact]
	public void DI_RegistersIDeadLetterQueueManager()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(A.Fake<IChannel>());
		services.AddRabbitMqDeadLetterQueue(options =>
		{
			options.QueueName = "test-dlq";
			options.Exchange = "test-dlx";
		});

		// Act
		var provider = services.BuildServiceProvider();
		var manager = provider.GetService<IDeadLetterQueueManager>();

		// Assert
		manager.ShouldNotBeNull();
		manager.ShouldBeOfType<RabbitMqDeadLetterQueueManager>();
	}

	#endregion

	public void Dispose()
	{
		_sut.Dispose();
		_fakeChannel.Dispose();
	}

	private static DeadLetterMessage CreateDeadLetterMessage(
		string messageId,
		string? originalSource,
		string reason = "Processing failed")
	{
		return new DeadLetterMessage
		{
			OriginalMessage = new TransportMessage
			{
				Id = messageId,
				Body = TestBody,
			},
			Reason = reason,
			OriginalSource = originalSource,
			DeadLetteredAt = DateTimeOffset.UtcNow.AddMinutes(-5),
			DeliveryAttempts = 3,
		};
	}
}
