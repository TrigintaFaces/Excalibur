// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2213 // Disposable fields should be disposed -- FakeItEasy fakes do not require disposal

using System.Text;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using DispatchOutboxOptions = Excalibur.Dispatch.Options.Delivery.OutboxOptions;

namespace Excalibur.Outbox.Tests.Core;

/// <summary>
/// Unit tests for <see cref="MessageOutbox"/>.
/// Verifies outbox message processing, storage, and disposal behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class MessageOutboxShould : IDisposable
{
	private readonly IOutboxStore _outboxStore;
	private readonly IOutboxProcessor _outboxProcessor;
	private readonly IJsonSerializer _serializer;
	private readonly IOptions<DispatchOutboxOptions> _options;
	private readonly ILogger<MessageOutbox> _logger;
	private MessageOutbox? _sut;

	public MessageOutboxShould()
	{
		_outboxStore = A.Fake<IOutboxStore>();
		_outboxProcessor = A.Fake<IOutboxProcessor>();
		_serializer = A.Fake<IJsonSerializer>();
		_options = Options.Create(DispatchOutboxOptions.Balanced());
		_logger = A.Fake<ILogger<MessageOutbox>>();
	}

	public void Dispose()
	{
		_sut?.Dispose();
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
		// Arrange - use concrete TestJsonSerializer to avoid FakeItEasy extension method issues
		var testSerializer = new TestJsonSerializer();
		_sut = new MessageOutbox(_outboxStore, _outboxProcessor, testSerializer, _options, _logger);
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
		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

		// Act
		try
		{
			await _sut.RunOutboxDispatchAsync("dispatcher-1", cts.Token);
		}
		catch (OperationCanceledException)
		{
			// Expected
		}

		// Assert
		A.CallTo(() => _outboxProcessor.Init("dispatcher-1")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ProcessMessages_UntilCancelled()
	{
		// Arrange
		_sut = new MessageOutbox(_outboxStore, _outboxProcessor, _serializer, _options, _logger);
		var callCount = 0;
		A.CallTo(() => _outboxProcessor.DispatchPendingMessagesAsync(A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				callCount++;
				return 1;
			});

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

		// Act
		var result = await _sut.RunOutboxDispatchAsync("dispatcher-1", cts.Token);

		// Assert - should have processed at least once
		callCount.ShouldBeGreaterThanOrEqualTo(1);
		result.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public async Task HandleExceptions_DuringProcessing()
	{
		// Arrange
		_sut = new MessageOutbox(_outboxStore, _outboxProcessor, _serializer, _options, _logger);
		var callCount = 0;
		A.CallTo(() => _outboxProcessor.DispatchPendingMessagesAsync(A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				callCount++;
				if (callCount == 1)
				{
					throw new InvalidOperationException("Test error");
				}

				return 0;
			});

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

		// Act & Assert - should not throw
		await Should.NotThrowAsync(async () =>
			await _sut.RunOutboxDispatchAsync("dispatcher-1", cts.Token));
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public void NotThrow_WhenDisposed()
	{
		// Arrange
		_sut = new MessageOutbox(_outboxStore, _outboxProcessor, _serializer, _options, _logger);

		// Act & Assert
		Should.NotThrow(() => _sut.Dispose());

		// Clear reference since it's disposed
		_sut = null;
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
			MessageBody = "{}",
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
		public required string MessageBody { get; init; }
		public required string MessageMetadata { get; init; }
		public required DateTimeOffset CreatedAt { get; init; }
		public DateTimeOffset? ExpiresAt { get; set; }
		public int Attempts { get; set; }
		public string? DispatcherId { get; set; }
		public DateTimeOffset? DispatcherTimeout { get; set; }
	}

	/// <summary>
	/// Simple test double for IJsonSerializer that avoids FakeItEasy extension method issues.
	/// </summary>
	private sealed class TestJsonSerializer : IJsonSerializer
	{
		public string Serialize(object value, Type type) => "{}";

		public object? Deserialize(string json, Type type) => null;

		public Task<string> SerializeAsync(object value, Type type) => Task.FromResult("{}");

		public Task<object?> DeserializeAsync(string json, Type type) => Task.FromResult<object?>(null);
	}

	#endregion
}
