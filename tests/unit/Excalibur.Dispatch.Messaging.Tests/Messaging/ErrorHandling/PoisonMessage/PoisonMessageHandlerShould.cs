// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Options.ErrorHandling;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Messaging.ErrorHandling.PoisonMessage;

/// <summary>
/// Unit tests for the <see cref="PoisonMessageHandler"/> class.
/// </summary>
/// <remarks>
/// Sprint 461 - Task S461.1: Remaining 0% Coverage Tests.
/// Tests the poison message handler including error handling and dead letter queue operations.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "ErrorHandling")]
[Trait("Priority", "0")]
public sealed class PoisonMessageHandlerShould : IDisposable
{
	private readonly IDeadLetterStore _deadLetterStore;
	private readonly IJsonSerializer _serializer;
	private readonly IServiceProvider _serviceProvider;
	private readonly IOptions<PoisonMessageOptions> _options;
	private readonly ILogger<PoisonMessageHandler> _logger;
	private readonly PoisonMessageHandler _sut;

	public PoisonMessageHandlerShould()
	{
		_deadLetterStore = A.Fake<IDeadLetterStore>();
		_serializer = A.Fake<IJsonSerializer>();
		_serviceProvider = A.Fake<IServiceProvider>();
		_options = Microsoft.Extensions.Options.Options.Create(new PoisonMessageOptions());
		_logger = NullLogger<PoisonMessageHandler>.Instance;

		_sut = new PoisonMessageHandler(
			_deadLetterStore,
			_serializer,
			_serviceProvider,
			_options,
			_logger);
	}

	public void Dispose()
	{
		_sut.Dispose();
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowsOnNullDeadLetterStore()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new PoisonMessageHandler(
			null!,
			_serializer,
			_serviceProvider,
			_options,
			_logger));
	}

	[Fact]
	public void Constructor_ThrowsOnNullSerializer()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new PoisonMessageHandler(
			_deadLetterStore,
			null!,
			_serviceProvider,
			_options,
			_logger));
	}

	[Fact]
	public void Constructor_ThrowsOnNullServiceProvider()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new PoisonMessageHandler(
			_deadLetterStore,
			_serializer,
			null!,
			_options,
			_logger));
	}

	[Fact]
	public void Constructor_ThrowsOnNullOptions()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new PoisonMessageHandler(
			_deadLetterStore,
			_serializer,
			_serviceProvider,
			null!,
			_logger));
	}

	[Fact]
	public void Constructor_ThrowsOnNullLogger()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new PoisonMessageHandler(
			_deadLetterStore,
			_serializer,
			_serviceProvider,
			_options,
			null!));
	}

	[Fact]
	public void Constructor_AcceptsValidParameters()
	{
		// Act
		using var sut = new PoisonMessageHandler(
			_deadLetterStore,
			_serializer,
			_serviceProvider,
			_options,
			_logger);

		// Assert
		_ = sut.ShouldNotBeNull();
	}

	#endregion

	#region HandlePoisonMessageAsync Tests - Parameter Validation

	[Fact]
	public async Task HandlePoisonMessageAsync_ThrowsOnNullMessage()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.HandlePoisonMessageAsync(null!, context, "reason", CancellationToken.None));
	}

	[Fact]
	public async Task HandlePoisonMessageAsync_ThrowsOnNullContext()
	{
		// Arrange
		var message = new TestMessage();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.HandlePoisonMessageAsync(message, null!, "reason", CancellationToken.None));
	}

	[Fact]
	public async Task HandlePoisonMessageAsync_ThrowsOnNullOrEmptyReason()
	{
		// Arrange
		var message = new TestMessage();
		var context = A.Fake<IMessageContext>();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.HandlePoisonMessageAsync(message, context, null!, CancellationToken.None));

		_ = await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.HandlePoisonMessageAsync(message, context, "", CancellationToken.None));

		_ = await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.HandlePoisonMessageAsync(message, context, "   ", CancellationToken.None));
	}

	#endregion

	#region HandlePoisonMessageAsync Tests - Behavior

	[Fact]
	public async Task HandlePoisonMessageAsync_StoresMessageInDeadLetterStore()
	{
		// Arrange
		var message = new TestMessage();
		var context = CreateFakeContext();
		var reason = "Test reason";

		_ = A.CallTo(() => _serializer.SerializeAsync(A<object>._, A<Type>._))
			.Returns(Task.FromResult("{}"));

		// Act
		await _sut.HandlePoisonMessageAsync(message, context, reason, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _deadLetterStore.StoreAsync(
				A<DeadLetterMessage>.That.Matches(m => m.Reason == reason),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task HandlePoisonMessageAsync_SerializesMessage()
	{
		// Arrange
		var message = new TestMessage();
		var context = CreateFakeContext();

		_ = A.CallTo(() => _serializer.SerializeAsync(A<object>._, A<Type>._))
			.Returns(Task.FromResult("{}"));

		// Act
		await _sut.HandlePoisonMessageAsync(message, context, "reason", CancellationToken.None);

		// Assert - The message should be serialized (as IDispatchMessage due to interface-based serialization)
		_ = A.CallTo(() => _serializer.SerializeAsync(A<object>._, A<Type>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task HandlePoisonMessageAsync_CapturesExceptionDetailsWhenEnabled()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new PoisonMessageOptions { CaptureExceptionDetails = true });
		using var sut = new PoisonMessageHandler(_deadLetterStore, _serializer, _serviceProvider, options, _logger);

		var message = new TestMessage();
		var context = CreateFakeContext();
		var exception = new InvalidOperationException("Test exception");

		_ = A.CallTo(() => _serializer.SerializeAsync(A<object>._, A<Type>._))
			.Returns(Task.FromResult("{}"));

		DeadLetterMessage? capturedMessage = null;
		_ = A.CallTo(() => _deadLetterStore.StoreAsync(A<DeadLetterMessage>._, A<CancellationToken>._))
			.Invokes(call => capturedMessage = call.GetArgument<DeadLetterMessage>(0));

		// Act
		await sut.HandlePoisonMessageAsync(message, context, "reason", CancellationToken.None, exception);

		// Assert
		_ = capturedMessage.ShouldNotBeNull();
		_ = capturedMessage.ExceptionDetails.ShouldNotBeNull();
		capturedMessage.ExceptionDetails.ShouldContain("InvalidOperationException");
		capturedMessage.ExceptionDetails.ShouldContain("Test exception");
	}

	[Fact]
	public async Task HandlePoisonMessageAsync_DoesNotCaptureExceptionDetailsWhenDisabled()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new PoisonMessageOptions { CaptureExceptionDetails = false });
		using var sut = new PoisonMessageHandler(_deadLetterStore, _serializer, _serviceProvider, options, _logger);

		var message = new TestMessage();
		var context = CreateFakeContext();
		var exception = new InvalidOperationException("Test exception");

		_ = A.CallTo(() => _serializer.SerializeAsync(A<object>._, A<Type>._))
			.Returns(Task.FromResult("{}"));

		DeadLetterMessage? capturedMessage = null;
		_ = A.CallTo(() => _deadLetterStore.StoreAsync(A<DeadLetterMessage>._, A<CancellationToken>._))
			.Invokes(call => capturedMessage = call.GetArgument<DeadLetterMessage>(0));

		// Act
		await sut.HandlePoisonMessageAsync(message, context, "reason", CancellationToken.None, exception);

		// Assert
		_ = capturedMessage.ShouldNotBeNull();
		capturedMessage.ExceptionDetails.ShouldBeNull();
	}

	[Fact]
	public async Task HandlePoisonMessageAsync_ExtractsProcessingInfoFromContext()
	{
		// Arrange
		var message = new TestMessage();
		var context = A.Fake<IMessageContext>();

		// Add processing info to context using IDictionary<string, object>
		var items = new Dictionary<string, object>
		{
			["ProcessingAttempts"] = 5,
			["FirstAttemptTime"] = DateTimeOffset.UtcNow.AddMinutes(-10),
			["CurrentAttemptTime"] = DateTimeOffset.UtcNow,
		};
		_ = A.CallTo(() => context.Items).Returns(items);
		_ = A.CallTo(() => context.MessageId).Returns(Guid.NewGuid().ToString("N"));
		_ = A.CallTo(() => context.CorrelationId).Returns(Guid.NewGuid().ToString("N"));

		_ = A.CallTo(() => _serializer.SerializeAsync(A<object>._, A<Type>._))
			.Returns(Task.FromResult("{}"));

		DeadLetterMessage? capturedMessage = null;
		_ = A.CallTo(() => _deadLetterStore.StoreAsync(A<DeadLetterMessage>._, A<CancellationToken>._))
			.Invokes(call => capturedMessage = call.GetArgument<DeadLetterMessage>(0));

		// Act
		await _sut.HandlePoisonMessageAsync(message, context, "reason", CancellationToken.None);

		// Assert
		_ = capturedMessage.ShouldNotBeNull();
		capturedMessage.ProcessingAttempts.ShouldBe(5);
	}

	[Fact]
	public async Task HandlePoisonMessageAsync_RethrowsOnStoreFailure()
	{
		// Arrange
		var message = new TestMessage();
		var context = CreateFakeContext();

		_ = A.CallTo(() => _serializer.SerializeAsync(A<object>._, A<Type>._))
			.Returns(Task.FromResult("{}"));

		_ = A.CallTo(() => _deadLetterStore.StoreAsync(A<DeadLetterMessage>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Store failed"));

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await _sut.HandlePoisonMessageAsync(message, context, "reason", CancellationToken.None));
	}

	#endregion

	#region ReplayMessageAsync Tests - Parameter Validation

	[Fact]
	public async Task ReplayMessageAsync_ThrowsOnNullOrEmptyMessageId()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.ReplayMessageAsync(null!, CancellationToken.None));

		_ = await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.ReplayMessageAsync("", CancellationToken.None));

		_ = await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.ReplayMessageAsync("   ", CancellationToken.None));
	}

	#endregion

	#region ReplayMessageAsync Tests - Behavior

	[Fact]
	public async Task ReplayMessageAsync_ReturnsFalseWhenMessageNotFound()
	{
		// Arrange
		_ = A.CallTo(() => _deadLetterStore.GetByIdAsync("test-id", A<CancellationToken>._))
			.Returns(Task.FromResult<DeadLetterMessage?>(null));

		// Act
		var result = await _sut.ReplayMessageAsync("test-id", CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task ReplayMessageAsync_ReturnsFalseWhenTypeNotFound()
	{
		// Arrange
		var deadLetterMessage = new DeadLetterMessage
		{
			Id = Guid.NewGuid().ToString("N"),
			MessageId = "test-id",
			MessageType = "NonExistent.Type, NonExistent.Assembly",
			MessageBody = "{}",
			MessageMetadata = "{}",
			Reason = "test",
		};

		_ = A.CallTo(() => _deadLetterStore.GetByIdAsync("test-id", A<CancellationToken>._))
			.Returns(deadLetterMessage);

		// Act
		var result = await _sut.ReplayMessageAsync("test-id", CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task ReplayMessageAsync_ReturnsFalseOnError()
	{
		// Arrange
		_ = A.CallTo(() => _deadLetterStore.GetByIdAsync("test-id", A<CancellationToken>._))
			.Throws(new InvalidOperationException("DB error"));

		// Act
		var result = await _sut.ReplayMessageAsync("test-id", CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region GetStatisticsAsync Tests

	[Fact]
	public async Task GetStatisticsAsync_ReturnsStatistics()
	{
		// Arrange
		_ = A.CallTo(() => _deadLetterStore.GetCountAsync(A<CancellationToken>._))
			.Returns(100L);

		_ = A.CallTo(() => _deadLetterStore.GetMessagesAsync(A<DeadLetterFilter>._, A<CancellationToken>._))
			.Returns(Enumerable.Empty<DeadLetterMessage>());

		// Act
		var result = await _sut.GetStatisticsAsync(CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.TotalCount.ShouldBe(100);
	}

	[Fact]
	public async Task GetStatisticsAsync_GroupsByMessageType()
	{
		// Arrange
		_ = A.CallTo(() => _deadLetterStore.GetCountAsync(A<CancellationToken>._))
			.Returns(5L);

		var messages = new[]
		{
			CreateDeadLetterMessage(messageType: "TypeA", reason: "Error"),
			CreateDeadLetterMessage(messageType: "TypeA", reason: "Error"),
			CreateDeadLetterMessage(messageType: "TypeB", reason: "Timeout"),
		};

		_ = A.CallTo(() => _deadLetterStore.GetMessagesAsync(A<DeadLetterFilter>._, A<CancellationToken>._))
			.Returns(messages);

		// Act
		var result = await _sut.GetStatisticsAsync(CancellationToken.None);

		// Assert
		result.MessagesByType.ShouldContainKey("TypeA");
		result.MessagesByType["TypeA"].ShouldBe(2);
		result.MessagesByType.ShouldContainKey("TypeB");
		result.MessagesByType["TypeB"].ShouldBe(1);
	}

	[Fact]
	public async Task GetStatisticsAsync_GroupsByReason()
	{
		// Arrange
		_ = A.CallTo(() => _deadLetterStore.GetCountAsync(A<CancellationToken>._))
			.Returns(3L);

		var messages = new[]
		{
			CreateDeadLetterMessage(messageType: "Type", reason: "Error"),
			CreateDeadLetterMessage(messageType: "Type", reason: "Error"),
			CreateDeadLetterMessage(messageType: "Type", reason: "Timeout"),
		};

		_ = A.CallTo(() => _deadLetterStore.GetMessagesAsync(A<DeadLetterFilter>._, A<CancellationToken>._))
			.Returns(messages);

		// Act
		var result = await _sut.GetStatisticsAsync(CancellationToken.None);

		// Assert
		result.MessagesByReason.ShouldContainKey("Error");
		result.MessagesByReason["Error"].ShouldBe(2);
		result.MessagesByReason.ShouldContainKey("Timeout");
		result.MessagesByReason["Timeout"].ShouldBe(1);
	}

	[Fact]
	public async Task GetStatisticsAsync_CalculatesOldestAndNewestDates()
	{
		// Arrange
		var oldest = DateTimeOffset.UtcNow.AddHours(-5);
		var newest = DateTimeOffset.UtcNow;

		_ = A.CallTo(() => _deadLetterStore.GetCountAsync(A<CancellationToken>._))
			.Returns(2L);

		var messages = new[]
		{
			CreateDeadLetterMessage(messageType: "Type", reason: "Error", movedToDeadLetterAt: oldest),
			CreateDeadLetterMessage(messageType: "Type", reason: "Error", movedToDeadLetterAt: newest),
		};

		_ = A.CallTo(() => _deadLetterStore.GetMessagesAsync(A<DeadLetterFilter>._, A<CancellationToken>._))
			.Returns(messages);

		// Act
		var result = await _sut.GetStatisticsAsync(CancellationToken.None);

		// Assert
		result.OldestMessageDate.ShouldBe(oldest);
		result.NewestMessageDate.ShouldBe(newest);
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		// Arrange
		var sut = new PoisonMessageHandler(
			_deadLetterStore,
			_serializer,
			_serviceProvider,
			_options,
			_logger);

		// Act & Assert - Should not throw
		sut.Dispose();
		sut.Dispose();
		sut.Dispose();
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void Handler_ImplementsIPoisonMessageHandler()
	{
		// Assert
		_ = _sut.ShouldBeAssignableTo<IPoisonMessageHandler>();
	}

	[Fact]
	public void Handler_ImplementsIDisposable()
	{
		// Assert
		_ = _sut.ShouldBeAssignableTo<IDisposable>();
	}

	#endregion

	#region Helper Methods

	private static IMessageContext CreateFakeContext()
	{
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.MessageId).Returns(Guid.NewGuid().ToString("N"));
		_ = A.CallTo(() => context.CorrelationId).Returns(Guid.NewGuid().ToString("N"));
		_ = A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		return context;
	}

	private static DeadLetterMessage CreateDeadLetterMessage(
		string messageType = "Type",
		string reason = "Error",
		DateTimeOffset? movedToDeadLetterAt = null)
	{
		return new DeadLetterMessage
		{
			MessageId = Guid.NewGuid().ToString("N"),
			MessageType = messageType,
			MessageBody = "{}",
			MessageMetadata = "{}",
			Reason = reason,
			MovedToDeadLetterAt = movedToDeadLetterAt ?? DateTimeOffset.UtcNow,
		};
	}

	#endregion

	#region Test Types

	private sealed class TestMessage : IDispatchMessage { }

	#endregion
}
