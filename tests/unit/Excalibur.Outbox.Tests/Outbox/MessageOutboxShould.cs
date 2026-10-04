// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

#pragma warning disable CA2213 // Disposable fields should be disposed -- FakeItEasy fakes do not require disposal

using System.Text;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using DispatchOutboxOptions = Excalibur.Dispatch.Options.Delivery.OutboxDeliveryOptions;

namespace Excalibur.Outbox.Tests.Core;

/// <summary>
/// Unit tests for <see cref="MessageOutbox"/>.
/// Verifies outbox message processing, storage, and disposal behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class MessageOutboxShould : IAsyncDisposable
{
	private readonly IOutboxStore _outboxStore;
	private readonly IOutboxProcessor _outboxProcessor;
	private readonly DispatchJsonSerializer _serializer;
	private readonly IOptions<DispatchOutboxOptions> _options;
	private readonly ILogger<MessageOutbox> _logger;
	private MessageOutbox? _sut;

	public MessageOutboxShould()
	{
		_outboxStore = A.Fake<IOutboxStore>();
		_outboxProcessor = A.Fake<IOutboxProcessor>();
		_serializer = new DispatchJsonSerializer();
		_options = Options.Create(DispatchOutboxOptions.Balanced());
		_logger = A.Fake<ILogger<MessageOutbox>>();
	}

	public async ValueTask DisposeAsync()
	{
		if (_sut is not null)
		{
			await _sut.DisposeAsync();
		}
	}

	[Fact]
	public async Task CreateOnlyScopedProcessorsAndDisposeEachCycle()
	{
		var processors = new List<IOutboxProcessor>();
		var services = new ServiceCollection();
		services.AddTransient<IOutboxProcessor>(_ =>
		{
			var processor = A.Fake<IOutboxProcessor>();
			processors.Add(processor);
			A.CallTo(() => processor.DispatchPendingMessagesAsync(A<CancellationToken>._)).Returns(3);
			return processor;
		});
		await using var provider = services.BuildServiceProvider();
		await using var dispatcher = ActivatorUtilities.CreateInstance<MessageOutbox>(provider,
			_outboxStore, _serializer, _options, _logger);
		processors.ShouldBeEmpty("constructing the dispatcher must not capture a root processor");
		(await dispatcher.RunOutboxDispatchAsync("first", CancellationToken.None)).ShouldBe(3);
		(await dispatcher.RunOutboxDispatchAsync("second", CancellationToken.None)).ShouldBe(3);
		processors.Count.ShouldBe(2);
		A.CallTo(() => processors[0].Init("first")).MustHaveHappenedOnceExactly();
		A.CallTo(() => processors[1].Init("second")).MustHaveHappenedOnceExactly();
		foreach (var processor in processors)
		{
			A.CallTo(() => processor.DisposeAsync()).MustHaveHappenedOnceExactly();
		}
	}

	[Fact]
	public async Task JoinScopedCleanupWhenDisposingAnActiveCycle()
	{
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var cleanupEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var releaseCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var processor = A.Fake<IOutboxProcessor>();
		A.CallTo(() => processor.DispatchPendingMessagesAsync(A<CancellationToken>._))
			.ReturnsLazily(async call =>
			{
				entered.SetResult();
				await Task.Delay(Timeout.Infinite, call.GetArgument<CancellationToken>(0));
				return 0;
			});
		A.CallTo(() => processor.DisposeAsync()).ReturnsLazily(() =>
		{
			cleanupEntered.SetResult();
			return new ValueTask(releaseCleanup.Task);
		});
		await using var provider = new ServiceCollection().AddTransient<IOutboxProcessor>(_ => processor).BuildServiceProvider();
		var dispatcher = new MessageOutbox(_outboxStore, _serializer, _options, _logger,
			provider.GetRequiredService<IServiceScopeFactory>());
		var run = dispatcher.RunOutboxDispatchAsync("active", CancellationToken.None);
		await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
		var disposal = dispatcher.DisposeAsync().AsTask();
		var secondDisposal = dispatcher.DisposeAsync().AsTask();
		try
		{
			await cleanupEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
			disposal.IsCompleted.ShouldBeFalse();
			secondDisposal.IsCompleted.ShouldBeFalse();
			await Should.ThrowAsync<ObjectDisposedException>(() => dispatcher.RunOutboxDispatchAsync("late", CancellationToken.None));
		}
		finally
		{
			releaseCleanup.TrySetResult();
		}
		await Should.ThrowAsync<OperationCanceledException>(() => run);
		await Task.WhenAll(disposal, secondDisposal).WaitAsync(TimeSpan.FromSeconds(10));
	}

	#region Constructor Tests

	[Fact]
	public void CreateInstance_WithValidParameters()
	{
		// Act
		_sut = new MessageOutbox(_outboxStore, _outboxProcessor, _serializer, _options, _logger);

		// Assert
		_sut.ShouldNotBeNull();
	}

	#endregion

	#region SignalNewMessage Tests

	[Fact]
	public void NotThrow_WhenSignalingNewMessage()
	{
		// Arrange
		_sut = new MessageOutbox(_outboxStore, _outboxProcessor, _serializer, _options, _logger);

		// Act & Assert
		Should.NotThrow(() => _sut.SignalNewMessage());
	}

	[Fact]
	public void NotThrow_WhenSignalingMultipleTimes()
	{
		// Arrange
		_sut = new MessageOutbox(_outboxStore, _outboxProcessor, _serializer, _options, _logger);

		// Act & Assert
		Should.NotThrow(() =>
		{
			for (var i = 0; i < 100; i++)
			{
				_sut.SignalNewMessage();
			}
		});
	}

	#endregion

	#region SaveEventsAsync Tests

	[Fact]
	public async Task ThrowArgumentNullException_WhenIntegrationEventsIsNull()
	{
		// Arrange
		_sut = new MessageOutbox(_outboxStore, _outboxProcessor, _serializer, _options, _logger);
		var metadata = A.Fake<IMessageMetadata>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.SaveEventsAsync(null!, metadata, CancellationToken.None));
	}

	[Fact]
	public async Task ReturnEarly_WhenNoEventsToSave()
	{
		// Arrange
		_sut = new MessageOutbox(_outboxStore, _outboxProcessor, _serializer, _options, _logger);
		var events = Array.Empty<IIntegrationEvent>();
		var metadata = A.Fake<IMessageMetadata>();

		// Act
		await _sut.SaveEventsAsync(events, metadata, CancellationToken.None);

		// Assert
		A.CallTo(() => _outboxStore.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task CallStageMessageAsync_ForEachEvent()
	{
		// Arrange - use a real DispatchJsonSerializer instance (sealed class, cannot be subclassed or faked)
		_sut = new MessageOutbox(_outboxStore, _outboxProcessor, _serializer, _options, _logger);
		var events = new IIntegrationEvent[]
		{
			new TestIntegrationEvent { Data = "event1" },
			new TestIntegrationEvent { Data = "event2" }
		};
		var metadata = A.Fake<IMessageMetadata>();

		// Act
		await _sut.SaveEventsAsync(events, metadata, CancellationToken.None);

		// Assert
		A.CallTo(() => _outboxStore.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.MustHaveHappened(2, Times.Exactly);
	}

	#endregion

	#region SaveMessagesAsync Tests

	[Fact]
	public async Task ThrowArgumentNullException_WhenOutboxMessagesIsNull()
	{
		// Arrange
		_sut = new MessageOutbox(_outboxStore, _outboxProcessor, _serializer, _options, _logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.SaveMessagesAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ReturnZero_WhenNoMessagesToSave()
	{
		// Arrange
		_sut = new MessageOutbox(_outboxStore, _outboxProcessor, _serializer, _options, _logger);
		var messages = Array.Empty<IOutboxMessage>();

		// Act
		var result = await _sut.SaveMessagesAsync(messages, CancellationToken.None);

		// Assert
		result.ShouldBe(0);
		A.CallTo(() => _outboxStore.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ReturnMessageCount_WhenSavingMessages()
	{
		// Arrange
		_sut = new MessageOutbox(_outboxStore, _outboxProcessor, _serializer, _options, _logger);
		var messages = new IOutboxMessage[]
		{
			CreateTestOutboxMessage("msg-1"),
			CreateTestOutboxMessage("msg-2"),
			CreateTestOutboxMessage("msg-3")
		};

		// Act
		var result = await _sut.SaveMessagesAsync(messages, CancellationToken.None);

		// Assert
		result.ShouldBe(3);
	}

	[Fact]
	public async Task CallStageMessageAsync_ForEachMessage()
	{
		// Arrange
		_sut = new MessageOutbox(_outboxStore, _outboxProcessor, _serializer, _options, _logger);
		var messages = new IOutboxMessage[]
		{
			CreateTestOutboxMessage("msg-1"),
			CreateTestOutboxMessage("msg-2")
		};

		// Act
		await _sut.SaveMessagesAsync(messages, CancellationToken.None);

		// Assert
		A.CallTo(() => _outboxStore.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.MustHaveHappened(2, Times.Exactly);
	}

	[Fact]
	public async Task ApplyTtl_WhenExpiresAtIsNull()
	{
		// Arrange
		var optionsWithTtl = Options.Create(DispatchOutboxOptions.Balanced()
			.WithTimeout(TimeSpan.FromHours(1)));

		// Modify options to have TTL
		var opts = DispatchOutboxOptions.Balanced();
		var optionsField = typeof(DispatchOutboxOptions).GetProperty("DefaultMessageTimeToLive");
		if (optionsField != null)
		{
			optionsField.SetValue(opts, TimeSpan.FromHours(1));
		}

		_sut = new MessageOutbox(_outboxStore, _outboxProcessor, _serializer, Options.Create(opts), _logger);

		var message = CreateTestOutboxMessage("msg-ttl");
		message.ExpiresAt = null;

		// Act
		await _sut.SaveMessagesAsync([message], CancellationToken.None);

		// Assert - message should have been staged
		A.CallTo(() => _outboxStore.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region GetPendingMessagesAsync Tests

	[Fact]
	public async Task ReturnEmptyCollection_WhenNoMessages()
	{
		// Arrange
		_sut = new MessageOutbox(_outboxStore, _outboxProcessor, _serializer, _options, _logger);
		A.CallTo(() => _outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(Array.Empty<OutboundMessage>());

		// Act
		var result = await _sut.GetPendingMessagesAsync(CancellationToken.None);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task UseProducerBatchSize_FromOptions()
	{
		// Arrange
		var options = DispatchOutboxOptions.HighThroughput();
		_sut = new MessageOutbox(_outboxStore, _outboxProcessor, _serializer, Options.Create(options), _logger);
		A.CallTo(() => _outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(Array.Empty<OutboundMessage>());

		// Act
		await _sut.GetPendingMessagesAsync(CancellationToken.None);

		// Assert - should use batch size from high throughput options (1000)
		A.CallTo(() => _outboxStore.GetUnsentMessagesAsync(1000, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task HandleDeserializationErrors_Gracefully()
	{
		// Arrange
		_sut = new MessageOutbox(_outboxStore, _outboxProcessor, _serializer, _options, _logger);
		var message = new OutboundMessage(
			"InvalidType.That.Does.Not.Exist",
			Encoding.UTF8.GetBytes("{}"),
			"default",
			new Dictionary<string, object>())
		{
			Id = "msg-invalid"
		};

		A.CallTo(() => _outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(new[] { message });

		// Act
		var result = await _sut.GetPendingMessagesAsync(CancellationToken.None);

		// Assert - should return empty since type cannot be resolved
		result.ShouldBeEmpty();
	}

	#endregion

	#region RunOutboxDispatchAsync Tests

	[Fact]
	public async Task InitializeProcessor_WithDispatcherId()
	{
		// Arrange
		_sut = new MessageOutbox(_outboxStore, _outboxProcessor, _serializer, _options, _logger);
		using var cts = new CancellationTokenSource();
		var dispatchObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		A.CallTo(() => _outboxProcessor.DispatchPendingMessagesAsync(A<CancellationToken>._))
			.Invokes(() => _ = dispatchObserved.TrySetResult())
			.ReturnsLazily(async call =>
			{
				var ct = call.GetArgument<CancellationToken>(0);
				try
				{
					await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(Timeout.Infinite, ct);
				}
				catch (OperationCanceledException)
				{
					// Expected
				}
				return 0;
			});

		// Act
		try
		{
			var runTask = _sut.RunOutboxDispatchAsync("dispatcher-1", cts.Token);
			var dispatchStarted = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
				() => dispatchObserved.Task.IsCompleted,
				TimeSpan.FromSeconds(10),
				TimeSpan.FromMilliseconds(20));
			dispatchStarted.ShouldBeTrue("dispatch loop should start");
			await cts.CancelAsync();
			await runTask;
		}
		catch (OperationCanceledException)
		{
			// Expected
		}

		// Assert
		A.CallTo(() => _outboxProcessor.Init("dispatcher-1")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReturnAfterExactlyOneCycle()
	{
		_sut = new MessageOutbox(_outboxStore, _outboxProcessor, _serializer, _options, _logger);
		A.CallTo(() => _outboxProcessor.DispatchPendingMessagesAsync(A<CancellationToken>._)).Returns(7);
		(await _sut.RunOutboxDispatchAsync("dispatcher-1", CancellationToken.None)).ShouldBe(7);
		A.CallTo(() => _outboxProcessor.DispatchPendingMessagesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PropagateCycleFailureToTheScheduler()
	{
		_sut = new MessageOutbox(_outboxStore, _outboxProcessor, _serializer, _options, _logger);
		var failure = new InvalidOperationException("store failed");
		A.CallTo(() => _outboxProcessor.DispatchPendingMessagesAsync(A<CancellationToken>._)).Throws(failure);
		(await Should.ThrowAsync<InvalidOperationException>(() => _sut.RunOutboxDispatchAsync("dispatcher-1", CancellationToken.None)))
			.ShouldBeSameAs(failure);
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public async Task RefuseSyncDisposalWithoutLosingAsyncCleanup()
	{
		_sut = new MessageOutbox(_outboxStore, _outboxProcessor, _serializer, _options, _logger);
		Should.Throw<InvalidOperationException>(() => _sut.Dispose());
		await _sut.DisposeAsync();
		await _sut.DisposeAsync();
		A.CallTo(() => _outboxProcessor.DisposeAsync()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void DisposeProcessor_WhenDisposable()
	{
		// Arrange
		var disposableProcessor = A.Fake<IOutboxProcessor>(x => x.Implements<IDisposable>());
		_sut = new MessageOutbox(_outboxStore, disposableProcessor, _serializer, _options, _logger);

		// Act
		_sut.Dispose();

		// Assert
		A.CallTo(() => ((IDisposable)disposableProcessor).Dispose())
			.MustHaveHappenedOnceExactly();

		// Clear reference since it's disposed
		_sut = null;
	}

	[Fact]
	public async Task DisposeAsync_DisposesProcessor_WhenAsyncDisposable()
	{
		// Arrange
		var asyncDisposableProcessor = A.Fake<IOutboxProcessor>(x => x.Implements<IAsyncDisposable>());
		_sut = new MessageOutbox(_outboxStore, asyncDisposableProcessor, _serializer, _options, _logger);

		// Act
		await _sut.DisposeAsync();

		// Assert
		A.CallTo(() => ((IAsyncDisposable)asyncDisposableProcessor).DisposeAsync())
			.MustHaveHappenedOnceExactly();

		// Clear reference since it's disposed
		_sut = null;
	}

	#endregion

	#region Helper Methods

	private static TestOutboxMessage CreateTestOutboxMessage(string messageId)
	{
		return new TestOutboxMessage
		{
			MessageId = messageId,
			MessageType = "TestType",
			MessageBody = Encoding.UTF8.GetBytes("{}"),
			MessageMetadata = "{}",
			CreatedAt = DateTimeOffset.UtcNow
		};
	}

	#endregion

	#region Test Doubles

	private sealed class TestIntegrationEvent : IIntegrationEvent
	{
		public string Data { get; set; } = string.Empty;
	}

	private sealed class TestOutboxMessage : IOutboxMessage
	{
		public required string MessageId { get; init; }
		public required string MessageType { get; init; }
		public required byte[] MessageBody { get; init; }
		public required string MessageMetadata { get; init; }
		public required DateTimeOffset CreatedAt { get; init; }
		public DateTimeOffset? ExpiresAt { get; set; }
		public int Attempts { get; set; }
		public string? DispatcherId { get; set; }
		public DateTimeOffset? DispatcherTimeout { get; set; }
	}

	#endregion
}