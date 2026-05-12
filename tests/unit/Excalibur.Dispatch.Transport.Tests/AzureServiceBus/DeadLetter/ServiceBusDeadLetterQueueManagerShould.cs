// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Azure.Messaging.ServiceBus;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.AzureServiceBus;
using Excalibur.Dispatch.Transport.AzureServiceBus.Internal;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.DeadLetter;

/// <summary>
/// Unit tests for <see cref="ServiceBusDeadLetterQueueManager"/>.
/// Validates Move, Get, Reprocess, Statistics, Purge, and error handling.
/// </summary>
/// <remarks>
/// S798-A5 re-migration (FORGE Q1=(c) reshape <c>f5c960341</c>): fakes the
/// flat use-case <see cref="IServiceBusClient"/> surface
/// (<c>SendMessageAsync</c>, <c>PeekDlqMessagesAsync</c>, <c>PurgeDlqAsync</c>).
/// No <c>ServiceBusClient</c> / <c>ServiceBusSender</c> / <c>ServiceBusReceiver</c>
/// fakes remain — SDK non-virtual-overload fragility (ADR-142 §D7) is fully
/// neutralized.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class ServiceBusDeadLetterQueueManagerShould : IDisposable
{
	private readonly IServiceBusClient _fakeClient;
	private readonly ServiceBusDeadLetterQueueManager _sut;

	private static readonly byte[] TestBody = Encoding.UTF8.GetBytes("test-body");

	public ServiceBusDeadLetterQueueManagerShould()
	{
		_fakeClient = A.Fake<IServiceBusClient>();

		var options = Microsoft.Extensions.Options.Options.Create(new ServiceBusDeadLetterOptions
		{
			EntityPath = "orders",
			MaxBatchSize = 10,
			ReceiveWaitTime = TimeSpan.FromSeconds(1),
			StatisticsPeekCount = 100,
		});

		_sut = new ServiceBusDeadLetterQueueManager(
			_fakeClient,
			options,
			NullLogger<ServiceBusDeadLetterQueueManager>.Instance);
	}

	#region MoveToDeadLetterAsync

	[Fact]
	public async Task MoveToDeadLetterAsync_SendsMessageViaAdapter()
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
		A.CallTo(() => _fakeClient.SendMessageAsync(
				"orders",
				A<ServiceBusMessage>.That.Matches(m => m.MessageId == "msg-1"),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MoveToDeadLetterAsync_IncludesExceptionDetails_WhenProvided()
	{
		// Arrange
		var message = new TransportMessage { Id = "msg-ex", Body = TestBody };
		var exception = new InvalidOperationException("Boom");

		ServiceBusMessage? capturedMessage = null;
		A.CallTo(() => _fakeClient.SendMessageAsync(
				A<string>._, A<ServiceBusMessage>._, A<CancellationToken>._))
			.Invokes((string _, ServiceBusMessage m, CancellationToken _) => capturedMessage = m)
			.Returns(Task.CompletedTask);

		// Act
		await _sut.MoveToDeadLetterAsync(message, "Error", exception, CancellationToken.None);

		// Assert
		capturedMessage.ShouldNotBeNull();
		capturedMessage.ApplicationProperties["dlq_exception_type"].ShouldBe(typeof(InvalidOperationException).FullName);
		capturedMessage.ApplicationProperties["dlq_exception_message"].ShouldBe("Boom");
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
	public async Task GetDeadLetterMessagesAsync_ReturnsPeekedMessages()
	{
		// Arrange
		var peekedMessages = new[]
		{
			ServiceBusModelFactory.ServiceBusReceivedMessage(
				body: BinaryData.FromBytes(TestBody),
				messageId: "msg-peek-1",
				contentType: "application/json",
				subject: "Timeout"),
		};

		A.CallTo(() => _fakeClient.PeekDlqMessagesAsync(
				"orders", A<int>._, A<CancellationToken>._))
			.Returns(peekedMessages);

		// Act
		var result = await _sut.GetDeadLetterMessagesAsync(10, CancellationToken.None);

		// Assert
		result.Count.ShouldBe(1);
		result[0].OriginalMessage.Id.ShouldBe("msg-peek-1");
		result[0].Reason.ShouldBe("Timeout");
	}

	[Fact]
	public async Task GetDeadLetterMessagesAsync_ReturnsEmpty_WhenNoneAvailable()
	{
		// Arrange
		A.CallTo(() => _fakeClient.PeekDlqMessagesAsync(
				"orders", A<int>._, A<CancellationToken>._))
			.Returns(Array.Empty<ServiceBusReceivedMessage>());

		// Act
		var result = await _sut.GetDeadLetterMessagesAsync(10, CancellationToken.None);

		// Assert
		result.ShouldBeEmpty();
	}

	#endregion

	#region ReprocessDeadLetterMessagesAsync

	[Fact]
	public async Task ReprocessDeadLetterMessagesAsync_SendsToOriginalEntity()
	{
		// Arrange
		var dlqMessages = new[] { CreateDeadLetterMessage("msg-rp-1", "orders") };
		var options = new ReprocessOptions { RetryDelay = TimeSpan.Zero };

		// Act
		var result = await _sut.ReprocessDeadLetterMessagesAsync(dlqMessages, options, CancellationToken.None);

		// Assert
		result.SuccessCount.ShouldBe(1);
		A.CallTo(() => _fakeClient.SendMessageAsync(
				"orders",
				A<ServiceBusMessage>.That.Matches(m => m.MessageId == "msg-rp-1"),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReprocessDeadLetterMessagesAsync_UsesTargetQueueOverride()
	{
		// Arrange
		var dlqMessages = new[] { CreateDeadLetterMessage("msg-1", "orders") };
		var options = new ReprocessOptions
		{
			TargetQueue = "retry-queue",
			RetryDelay = TimeSpan.Zero,
		};

		// Act
		var result = await _sut.ReprocessDeadLetterMessagesAsync(dlqMessages, options, CancellationToken.None);

		// Assert
		result.SuccessCount.ShouldBe(1);
		A.CallTo(() => _fakeClient.SendMessageAsync(
				"retry-queue",
				A<ServiceBusMessage>._,
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
		ServiceBusMessage? capturedMessage = null;
		A.CallTo(() => _fakeClient.SendMessageAsync(
				A<string>._, A<ServiceBusMessage>._, A<CancellationToken>._))
			.Invokes((string _, ServiceBusMessage m, CancellationToken _) => capturedMessage = m)
			.Returns(Task.CompletedTask);

		var transformedBody = Encoding.UTF8.GetBytes("transformed");
		var dlqMessages = new[] { CreateDeadLetterMessage("msg-t", "orders") };
		var options = new ReprocessOptions
		{
			MessageTransform = msg => new TransportMessage { Id = msg.Id, Body = transformedBody },
			RetryDelay = TimeSpan.Zero,
		};

		// Act
		await _sut.ReprocessDeadLetterMessagesAsync(dlqMessages, options, CancellationToken.None);

		// Assert
		capturedMessage.ShouldNotBeNull();
		capturedMessage.Body.ToArray().ShouldBe(transformedBody);
	}

	[Fact]
	public async Task ReprocessDeadLetterMessagesAsync_RespectsMaxMessages()
	{
		// Arrange
		var produceCount = 0;
		A.CallTo(() => _fakeClient.SendMessageAsync(
				A<string>._, A<ServiceBusMessage>._, A<CancellationToken>._))
			.Invokes(() => produceCount++)
			.Returns(Task.CompletedTask);

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
		produceCount.ShouldBe(2);
	}

	[Fact]
	public async Task ReprocessDeadLetterMessagesAsync_TracksFailures()
	{
		// Arrange
		A.CallTo(() => _fakeClient.SendMessageAsync(
				A<string>._, A<ServiceBusMessage>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Service Bus unavailable"));

		var dlqMessages = new[] { CreateDeadLetterMessage("msg-fail", "orders") };
		var options = new ReprocessOptions { RetryDelay = TimeSpan.Zero };

		// Act
		var result = await _sut.ReprocessDeadLetterMessagesAsync(dlqMessages, options, CancellationToken.None);

		// Assert
		result.FailureCount.ShouldBe(1);
		result.SuccessCount.ShouldBe(0);
		result.Failures.Count.ShouldBe(1);
		result.Failures.First().Reason.ShouldBe("Service Bus unavailable");
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
	public async Task GetStatisticsAsync_ReturnsZero_WhenEmpty()
	{
		// Arrange
		A.CallTo(() => _fakeClient.PeekDlqMessagesAsync(
				"orders", A<int>._, A<CancellationToken>._))
			.Returns(Array.Empty<ServiceBusReceivedMessage>());

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
	public async Task PurgeDeadLetterQueueAsync_ReturnsCountFromAdapter()
	{
		// Arrange — the adapter encapsulates the receive/complete cycle; manager
		// just forwards the drain and surfaces the total count.
		A.CallTo(() => _fakeClient.PurgeDlqAsync(
				"orders",
				A<int>._,
				A<TimeSpan>._,
				A<CancellationToken>._))
			.Returns(2);

		// Act
		var purgedCount = await _sut.PurgeDeadLetterQueueAsync(CancellationToken.None);

		// Assert
		purgedCount.ShouldBe(2);
		A.CallTo(() => _fakeClient.PurgeDlqAsync(
				"orders", 10, TimeSpan.FromSeconds(1), A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PurgeDeadLetterQueueAsync_ReturnsZero_WhenEmpty()
	{
		// Arrange
		A.CallTo(() => _fakeClient.PurgeDlqAsync(
				"orders", A<int>._, A<TimeSpan>._, A<CancellationToken>._))
			.Returns(0);

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
		services.AddSingleton(new ServiceBusClient("Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=dGVzdA=="));
		services.AddServiceBusDeadLetterQueue(options =>
		{
			options.EntityPath = "test-queue";
		});

		// Act
		var provider = services.BuildServiceProvider();
		// Sprint 697: DLQ now uses keyed registration
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IDeadLetterQueueManager) && sd.IsKeyedService);
	}

	#endregion

	public void Dispose()
	{
		_sut.Dispose();
		// _fakeClient is a fake of the internal IServiceBusClient interface,
		// which does not derive from IDisposable/IAsyncDisposable — nothing to dispose.
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
