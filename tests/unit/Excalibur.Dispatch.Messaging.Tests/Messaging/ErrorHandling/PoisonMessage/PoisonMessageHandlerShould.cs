// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.TypeResolution;
using Excalibur.Dispatch.Options.ErrorHandling;
using Excalibur.Dispatch.Serialization;

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
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "ErrorHandling")]
[Trait("Priority", "0")]
public sealed class PoisonMessageHandlerShould : IDisposable
{
	private readonly IDeadLetterStore _deadLetterStore;
	private readonly DispatchJsonSerializer _serializer;
	private readonly IServiceProvider _serviceProvider;
	private readonly IOptions<PoisonMessageOptions> _options;
	private readonly ILogger<PoisonMessageHandler> _logger;
	private readonly PoisonMessageHandler _sut;

	public PoisonMessageHandlerShould()
	{
		_deadLetterStore = A.Fake<IDeadLetterStore>(o => o.Implements<IDeadLetterStoreAdmin>());

		// AN UNCONFIGURED FAKE REPORTS Unknown, AND THE HANDLER REFUSES THAT -- correctly, because a store
		// that decided nothing has granted nothing. Arms below are about the replay path rather than the
		// claim, so the won claim is the default here; the arms that test a refusal set their own.
		_ = A.CallTo(() => _deadLetterStore.MarkAsReplayedAsync(A<string>._, A<CancellationToken>._))
			.Returns(DeadLetterReplayOutcome.Claimed);
		_serializer = new DispatchJsonSerializer();
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
		_serializer.Dispose();
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

		// Act
		await _sut.HandlePoisonMessageAsync(message, context, "reason", CancellationToken.None);

		// Assert - The message should be serialized and stored
		_ = A.CallTo(() => _deadLetterStore.StoreAsync(A<DeadLetterMessage>._, A<CancellationToken>._))
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
	public async Task HandlePoisonMessageAsync_CapturesInnerExceptionDetailsWhenEnabled()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new PoisonMessageOptions { CaptureExceptionDetails = true });
		using var sut = new PoisonMessageHandler(_deadLetterStore, _serializer, _serviceProvider, options, _logger);

		var message = new TestMessage();
		var context = CreateFakeContext();
		var innerEx = new ArgumentException("Bad argument");
		var exception = new InvalidOperationException("Outer error", innerEx);

		DeadLetterMessage? capturedMessage = null;
		_ = A.CallTo(() => _deadLetterStore.StoreAsync(A<DeadLetterMessage>._, A<CancellationToken>._))
			.Invokes(call => capturedMessage = call.GetArgument<DeadLetterMessage>(0));

		// Act
		await sut.HandlePoisonMessageAsync(message, context, "reason", CancellationToken.None, exception);

		// Assert
		_ = capturedMessage.ShouldNotBeNull();
		_ = capturedMessage.ExceptionDetails.ShouldNotBeNull();
		capturedMessage.ExceptionDetails.ShouldContain("InvalidOperationException");
		capturedMessage.ExceptionDetails.ShouldContain("Outer error");
		capturedMessage.ExceptionDetails.ShouldContain("ArgumentException");
		capturedMessage.ExceptionDetails.ShouldContain("Bad argument");
		capturedMessage.ExceptionDetails.ShouldContain("InnerException");
	}

	[Fact]
	public async Task HandlePoisonMessageAsync_CapturesExceptionDataWhenEnabled()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new PoisonMessageOptions { CaptureExceptionDetails = true });
		using var sut = new PoisonMessageHandler(_deadLetterStore, _serializer, _serviceProvider, options, _logger);

		var message = new TestMessage();
		var context = CreateFakeContext();
		var exception = new InvalidOperationException("Data test");
		exception.Data["OrderId"] = "ORD-123";
		exception.Data["Retry"] = "3";

		DeadLetterMessage? capturedMessage = null;
		_ = A.CallTo(() => _deadLetterStore.StoreAsync(A<DeadLetterMessage>._, A<CancellationToken>._))
			.Invokes(call => capturedMessage = call.GetArgument<DeadLetterMessage>(0));

		// Act
		await sut.HandlePoisonMessageAsync(message, context, "reason", CancellationToken.None, exception);

		// Assert
		_ = capturedMessage.ShouldNotBeNull();
		_ = capturedMessage.ExceptionDetails.ShouldNotBeNull();
		capturedMessage.ExceptionDetails.ShouldContain("Data");
		capturedMessage.ExceptionDetails.ShouldContain("OrderId");
		capturedMessage.ExceptionDetails.ShouldContain("ORD-123");
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
	public async Task PreserveTypedFirstAttemptEvidenceInTheDeadLetterDespiteProducerMetadata()
	{
		var context = DispatchContextInitializer.CreateFromMetadata(new Dictionary<string, string?>
		{
			["FirstAttemptTime"] = "producer-owned-value",
		});
		var retainedFirst = DateTimeOffset.UtcNow.AddDays(-1);
		context.GetOrCreateProcessingFeature().FirstAttemptTime = retainedFirst;
		context.Items["CurrentAttemptTime"] = retainedFirst.AddHours(2);
		DeadLetterMessage? stored = null;
		_ = A.CallTo(() => _deadLetterStore.StoreAsync(A<DeadLetterMessage>._, A<CancellationToken>._))
			.Invokes(call => stored = call.GetArgument<DeadLetterMessage>(0));

		await _sut.HandlePoisonMessageAsync(new TestMessage(), context, "expired", CancellationToken.None);

		stored.ShouldNotBeNull().FirstAttemptAt.ShouldBe(retainedFirst);
		context.Items["FirstAttemptTime"].ShouldBe("producer-owned-value");
	}

	[Fact]
	public async Task HandlePoisonMessageAsync_RethrowsOnStoreFailure()
	{
		// Arrange
		var message = new TestMessage();
		var context = CreateFakeContext();

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
		// Arrange - GetCountAsync is now on IDeadLetterStoreAdmin (ISP split in Sprint 745)
		_ = A.CallTo(() => ((IDeadLetterStoreAdmin)_deadLetterStore).GetCountAsync(A<CancellationToken>._))
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
		// Arrange - GetCountAsync is now on IDeadLetterStoreAdmin (ISP split in Sprint 745)
		_ = A.CallTo(() => ((IDeadLetterStoreAdmin)_deadLetterStore).GetCountAsync(A<CancellationToken>._))
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
		// Arrange - GetCountAsync is now on IDeadLetterStoreAdmin (ISP split in Sprint 745)
		_ = A.CallTo(() => ((IDeadLetterStoreAdmin)_deadLetterStore).GetCountAsync(A<CancellationToken>._))
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

		// GetCountAsync is now on IDeadLetterStoreAdmin (ISP split in Sprint 745)
		_ = A.CallTo(() => ((IDeadLetterStoreAdmin)_deadLetterStore).GetCountAsync(A<CancellationToken>._))
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

	#region Declared-name identity

	[Fact]
	public async Task HandlePoisonMessageAsync_CaptureTheDeclaredNameNotTheClrFullName()
	{
		// Arrange
		var message = new NamedPoisonMessage();

		// Act
		await _sut.HandlePoisonMessageAsync(message, CreateFakeContext(), "reason", CancellationToken.None);

		// Assert: the declared name is stored, not the CLR full name. Every other durable surface
		// stores the declared name, and a CLR name moves when a consumer refactors a namespace.
		_ = A.CallTo(() => _deadLetterStore.StoreAsync(
				A<DeadLetterMessage>.That.Matches(m => m.MessageType == DeclaredName),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task HandlePoisonMessageAsync_FallBackToTheClrNameForAMessageThatDeclaresNone()
	{
		// Arrange: the dead-letter path is the error path, so a message with no declared name must
		// still be captured rather than failing a second time.
		var message = new TestMessage();

		// Act
		await _sut.HandlePoisonMessageAsync(message, CreateFakeContext(), "reason", CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _deadLetterStore.StoreAsync(
				A<DeadLetterMessage>.That.Matches(m => m.MessageType == typeof(TestMessage).FullName),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	/// <summary>
	/// A SECOND REPLAY OF THE SAME MESSAGE MUST NOT DISPATCH IT AGAIN.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The mark happens AFTER the dispatch, so nothing between the two consulted it: a second call read
	/// the entry, found no guard, and dispatched. This needs no concurrency -- a sequential second call
	/// is enough, and an operator retrying a replay that appeared to hang is the ordinary way to reach
	/// it.
	/// </para>
	/// <para>
	/// The fixture deliberately uses the wiring under which a replay SUCCEEDS -- a registered declared
	/// name and a real dispatcher -- because that is the only substrate on which removing the guard is
	/// observable. With an unresolvable type or an unwired dispatcher the replay fails for its own
	/// reasons and the arm would pass whether the guard exists or not.
	/// </para>
	/// <para>
	/// Both assertions are needed. The refusal alone could be satisfied by a handler that refuses
	/// everything, so the arm also requires that the already-replayed entry was NOT marked a second
	/// time; and its sibling above requires that a fresh entry still replays, which is what stops a
	/// blanket refusal passing this file.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task ReplayMessageAsync_RefuseAMessageThatWasAlreadyReplayed()
	{
		_ = MessageTypeResolver.Instance;
		MessageTypeRegistry.RegisterType<NamedPoisonMessage>();

		var deadLetterMessage = new DeadLetterMessage
		{
			Id = Guid.NewGuid().ToString("N"),
			MessageId = "already-replayed-id",
			MessageType = DeclaredName,
			MessageBody = "{}",
			MessageMetadata = "{}",
			Reason = "test",

			// THE STATE UNDER TEST. Everything else about this entry is replayable, so the only thing
			// that may refuse it is the guard.
			IsReplayed = true,
		};

		_ = A.CallTo(() => _deadLetterStore.GetByIdAsync("already-replayed-id", A<CancellationToken>._))
			.Returns(deadLetterMessage);

		using var sut = CreateHandlerWithDispatcher();

		var result = await sut.ReplayMessageAsync("already-replayed-id", CancellationToken.None);

		result.ShouldBeFalse(
			"a message that has already been replayed must not be replayed again. The mark is written "
			+ "after the dispatch, so without a guard a second call dispatches the message a second time "
			+ "-- and a consumer or operator retrying a replay is how that happens in practice");

		A.CallTo(() => _deadLetterStore.MarkAsReplayedAsync(
				A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	/// <summary>
	/// A REFUSED CLAIM MUST NOT DISPATCH, WHICH IS THE CONCURRENT CASE NO READ CAN CLOSE.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The early guard reads the stored flag, so it closes the sequential case only: two callers racing
	/// both read an unmarked message and both pass it. What separates them is the store's conditional
	/// write, which exactly one of them wins. This arm is the loser, and the loser must not dispatch.
	/// </para>
	/// <para>
	/// Driven through the real wiring that makes a replay SUCCEED -- a registered declared name and a live
	/// dispatcher -- because that is the only substrate on which removing the claim check is observable.
	/// With an unresolvable type the replay fails for its own reasons and the arm would pass either way.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task ReplayMessageAsync_RefuseAMessageWhoseReplayClaimWasLost()
	{
		_ = MessageTypeResolver.Instance;
		MessageTypeRegistry.RegisterType<NamedPoisonMessage>();

		var deadLetterMessage = new DeadLetterMessage
		{
			Id = Guid.NewGuid().ToString("N"),
			MessageId = "contested-id",
			MessageType = DeclaredName,
			MessageBody = "{}",
			MessageMetadata = "{}",
			Reason = "test",

			// NOT yet replayed as far as this caller's READ is concerned. That is the whole point: the read
			// cannot see the other caller, so only the claim can refuse.
			IsReplayed = false,
		};

		_ = A.CallTo(() => _deadLetterStore.GetByIdAsync("contested-id", A<CancellationToken>._))
			.Returns(deadLetterMessage);

		// THE STATE UNDER TEST: somebody else won the claim between our read and our write.
		_ = A.CallTo(() => _deadLetterStore.MarkAsReplayedAsync("contested-id", A<CancellationToken>._))
			.Returns(DeadLetterReplayOutcome.AlreadyReplayed);

		using var sut = CreateHandlerWithDispatcher();

		var result = await sut.ReplayMessageAsync("contested-id", CancellationToken.None);

		result.ShouldBeFalse(
			"the caller that lost the claim must not dispatch. Both callers read an unmarked message, so "
			+ "nothing but the store's own conditional write can separate them -- and if the loser "
			+ "dispatches anyway the message is delivered twice with nothing recording it");
	}

	/// <summary>
	/// A REPLAY THAT DID NOT SUCCEED MUST GIVE THE CLAIM BACK, or it is permanently unreplayable.
	/// </summary>
	/// <remarks>
	/// This is the liveness half of claiming before the effect. Claiming first bounds the duplicate to
	/// zero; without the release it also converts every failed replay into a message marked replayed that
	/// was never delivered, which no later attempt can correct and which reads identically to a success.
	/// </remarks>
	[Fact]
	public async Task ReplayMessageAsync_ReleaseTheClaimWhenTheReplayDoesNotSucceed()
	{
		_ = MessageTypeResolver.Instance;
		MessageTypeRegistry.RegisterType<NamedPoisonMessage>();

		var deadLetterMessage = new DeadLetterMessage
		{
			Id = Guid.NewGuid().ToString("N"),
			MessageId = "failing-id",
			MessageType = DeclaredName,
			MessageBody = "{}",
			MessageMetadata = "{}",
			Reason = "test",
			IsReplayed = false,
		};

		_ = A.CallTo(() => _deadLetterStore.GetByIdAsync("failing-id", A<CancellationToken>._))
			.Returns(deadLetterMessage);

		using var sut = CreateHandlerWithFailingDispatcher();

		var result = await sut.ReplayMessageAsync("failing-id", CancellationToken.None);

		result.ShouldBeFalse("the dispatcher reported failure, so the replay did not succeed");

		_ = A.CallTo(() => _deadLetterStore.ReleaseReplayClaimAsync("failing-id", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReplayMessageAsync_ResolveAMessageWhoseDeclaredNameIsNotAClrTypeName()
	{
		// Arrange: the declared name is deliberately nothing like a CLR type name, so this can only
		// pass through the declared-name claim in the type registry.
		// The registry only answers through a registered resolver; production forces this when the
		// dispatch services are added.
		_ = MessageTypeResolver.Instance;
		MessageTypeRegistry.RegisterType<NamedPoisonMessage>();

		var deadLetterMessage = new DeadLetterMessage
		{
			Id = Guid.NewGuid().ToString("N"),
			MessageId = "declared-name-id",
			MessageType = DeclaredName,
			MessageBody = "{}",
			MessageMetadata = "{}",
			Reason = "test",
		};

		_ = A.CallTo(() => _deadLetterStore.GetByIdAsync("declared-name-id", A<CancellationToken>._))
			.Returns(deadLetterMessage);

		using var sut = CreateHandlerWithDispatcher();

		// Act
		var result = await sut.ReplayMessageAsync("declared-name-id", CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task ReplayMessageAsync_StillResolveAnEntryStoredUnderTheClrFullName()
	{
		// Arrange: entries captured before this identity change hold the CLR full name. The registry
		// claims a type under BOTH forms, so those entries must keep replaying.
		// The registry only answers through a registered resolver; production forces this when the
		// dispatch services are added.
		_ = MessageTypeResolver.Instance;
		MessageTypeRegistry.RegisterType<NamedPoisonMessage>();

		var deadLetterMessage = new DeadLetterMessage
		{
			Id = Guid.NewGuid().ToString("N"),
			MessageId = "clr-name-id",
			MessageType = typeof(NamedPoisonMessage).FullName!,
			MessageBody = "{}",
			MessageMetadata = "{}",
			Reason = "test",
		};

		_ = A.CallTo(() => _deadLetterStore.GetByIdAsync("clr-name-id", A<CancellationToken>._))
			.Returns(deadLetterMessage);

		using var sut = CreateHandlerWithDispatcher();

		// Act
		var result = await sut.ReplayMessageAsync("clr-name-id", CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	#endregion

	/// <summary>
	/// Replay resolves the stored type, then dispatches it through a scope. The class fixture's bare
	/// fake provider cannot create a scope, so a replay that must reach a verdict needs a real
	/// container with a dispatcher in it.
	/// </summary>
	private PoisonMessageHandler CreateHandlerWithDispatcher()
	{
		var dispatcher = A.Fake<IDispatcher>();
		_ = A.CallTo(dispatcher)
			.WithReturnType<Task<IMessageResult>>()
			.Returns(Task.FromResult<IMessageResult>(MessageResult.Success()));

		var services = new ServiceCollection();
		_ = services.AddSingleton(dispatcher);

		return new PoisonMessageHandler(
			_deadLetterStore,
			_serializer,
			services.BuildServiceProvider(),
			_options,
			_logger);
	}

	/// <summary>
	/// A handler whose dispatcher reports failure, so the replay path reaches its own failure branch rather
	/// than throwing somewhere earlier.
	/// </summary>
	private PoisonMessageHandler CreateHandlerWithFailingDispatcher()
	{
		var dispatcher = A.Fake<IDispatcher>();
		_ = A.CallTo(dispatcher)
			.WithReturnType<Task<IMessageResult>>()
			.Returns(Task.FromResult<IMessageResult>(MessageResult.Failed("the handler refused")));

		var services = new ServiceCollection();
		_ = services.AddSingleton(dispatcher);

		return new PoisonMessageHandler(
			_deadLetterStore,
			_serializer,
			services.BuildServiceProvider(),
			_options,
			_logger);
	}

	#region Test Types

	private const string DeclaredName = "poison.declared.v1";

	private sealed class TestMessage : IDispatchMessage { }

	[MessageName(DeclaredName)]
	private sealed class NamedPoisonMessage : IDispatchMessage { }

	#endregion
}