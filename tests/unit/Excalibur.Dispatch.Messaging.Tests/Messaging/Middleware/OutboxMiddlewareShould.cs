// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Tests.TestFakes;

using Microsoft.Extensions.Logging.Abstractions;

using MsOptions = Microsoft.Extensions.Options.Options;
using OutboxOptions = Excalibur.Dispatch.Options.Middleware.OutboxOptions;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for the <see cref="OutboxMiddleware"/> class.
/// Validates outbox message staging, bypass logic, error handling, and configuration behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class OutboxMiddlewareShould
{
	private readonly IOutboxStore _outboxStore;
	private readonly ILogger<OutboxMiddleware> _logger;

	public OutboxMiddlewareShould()
	{
		_outboxStore = A.Fake<IOutboxStore>();
		_logger = NullLoggerFactory.Instance.CreateLogger<OutboxMiddleware>();
	}

	private OutboxMiddleware CreateMiddleware(
		OutboxOptions? options = null,
		IOutboxStore? outboxStore = null,
		bool omitOutboxStore = false)
	{
		var opts = options ?? new OutboxOptions { Enabled = true };
		return new OutboxMiddleware(
			MsOptions.Create(opts),
			omitOutboxStore ? null : (outboxStore ?? _outboxStore),
			_logger);
	}

	private static DispatchRequestDelegate CreateSuccessDelegate(bool[] wasCalled)
	{
		return (msg, ctx, ct) =>
		{
			wasCalled[0] = true;
			return new ValueTask<IMessageResult>(TestFakes.MessageResult.Success());
		};
	}

	private static DispatchRequestDelegate CreateFailureDelegate(string errorMessage)
	{
		return (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(TestFakes.MessageResult.Failure(errorMessage));
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new OutboxMiddleware(null!, _outboxStore, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new OutboxOptions());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new OutboxMiddleware(options, _outboxStore, null!));
	}

	[Fact]
	public void ThrowInvalidOperationException_WhenEnabledButNoOutboxStore()
	{
		// Arrange
		var options = MsOptions.Create(new OutboxOptions { Enabled = true });

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			new OutboxMiddleware(options, outboxStore: null, _logger));
	}

	[Fact]
	public void ConstructSuccessfully_WhenDisabledWithoutOutboxStore()
	{
		// Arrange
		var options = MsOptions.Create(new OutboxOptions { Enabled = false });

		// Act
		var middleware = new OutboxMiddleware(options, outboxStore: null, _logger);

		// Assert
		_ = middleware.ShouldNotBeNull();
	}

	[Fact]
	public void ConstructSuccessfully_WhenEnabledWithOutboxStore()
	{
		// Arrange
		var options = MsOptions.Create(new OutboxOptions { Enabled = true });

		// Act
		var middleware = new OutboxMiddleware(options, _outboxStore, _logger);

		// Assert
		_ = middleware.ShouldNotBeNull();
	}

	#endregion

	#region Stage and ApplicableMessageKinds Tests

	[Fact]
	public void HavePostProcessingStage()
	{
		// Arrange
		var middleware = CreateMiddleware();

		// Assert
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.PostProcessing);
	}

	[Fact]
	public void ApplyToActionAndEventMessageKinds()
	{
		// Arrange
		var middleware = CreateMiddleware();

		// Assert
		middleware.ApplicableMessageKinds.ShouldBe(MessageKinds.Action | MessageKinds.Event);
	}

	#endregion

	#region InvokeAsync Parameter Validation Tests

	[Fact]
	public async Task ThrowArgumentNullException_WhenMessageIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var context = new FakeMessageContext { MessageId = "test-123" };

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new(TestFakes.MessageResult.Success());

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(null!, context, NextDelegate, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchActionMessage();

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new(TestFakes.MessageResult.Success());

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(message, null!, NextDelegate, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenNextDelegateIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchActionMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(message, context, null!, CancellationToken.None).AsTask());
	}

	#endregion

	#region Disabled Mode Tests

	[Fact]
	public async Task PassThroughDirectly_WhenDisabled()
	{
		// Arrange
		var middleware = CreateMiddleware(new OutboxOptions { Enabled = false });
		var message = new FakeDispatchActionMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };
		var wasCalled = new[] { false };

		// Act
		var result = await middleware.InvokeAsync(
			message, context, CreateSuccessDelegate(wasCalled), CancellationToken.None);

		// Assert
		wasCalled[0].ShouldBeTrue();
		result.Succeeded.ShouldBeTrue();
	}

	#endregion

	#region ShouldStageMessage Bypass Tests

	[Fact]
	public async Task PassThroughDirectly_WhenMessageIsNotActionOrEvent()
	{
		// Arrange - FakeDispatchMessage is a plain IDispatchMessage, NOT IDispatchAction or IDispatchEvent
		var middleware = CreateMiddleware();
		var message = new FakeNonActionMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };
		var wasCalled = new[] { false };

		// Act
		var result = await middleware.InvokeAsync(
			message, context, CreateSuccessDelegate(wasCalled), CancellationToken.None);

		// Assert - should pass through without outbox processing
		wasCalled[0].ShouldBeTrue();
		result.Succeeded.ShouldBeTrue();
		A.CallTo(() => _outboxStore.StageMessageAsync(
			A<OutboundMessage>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task PassThroughDirectly_WhenMessageHasBypassOutboxAttribute()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new BypassOutboxMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };
		var wasCalled = new[] { false };

		// Act
		var result = await middleware.InvokeAsync(
			message, context, CreateSuccessDelegate(wasCalled), CancellationToken.None);

		// Assert
		wasCalled[0].ShouldBeTrue();
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task PassThroughDirectly_WhenMessageTypeIsInBypassList()
	{
		// Arrange
		var options = new OutboxOptions
		{
			Enabled = true,
			BypassOutboxForTypes = [nameof(FakeDispatchActionMessage)]
		};
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchActionMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };
		var wasCalled = new[] { false };

		// Act
		var result = await middleware.InvokeAsync(
			message, context, CreateSuccessDelegate(wasCalled), CancellationToken.None);

		// Assert
		wasCalled[0].ShouldBeTrue();
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task PassThroughDirectly_WhenContextHasBypassOutboxFlag()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchActionMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };
		context.SetItem<bool?>("BypassOutbox", true);
		var wasCalled = new[] { false };

		// Act
		var result = await middleware.InvokeAsync(
			message, context, CreateSuccessDelegate(wasCalled), CancellationToken.None);

		// Assert
		wasCalled[0].ShouldBeTrue();
		result.Succeeded.ShouldBeTrue();
	}

	#endregion

	#region Outbox Staging Tests

	[Fact]
	public async Task CallNextDelegate_AndStageMessages_WhenHandlerSucceeds()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchActionMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };

		var outboundMessages = new List<OutboundMessage>
		{
			new("OrderCreated", [1, 2, 3], "orders-queue"),
			new("NotificationSent", [4, 5, 6], "notifications-queue"),
		};

		DispatchRequestDelegate stagingDelegate = (msg, ctx, ct) =>
		{
			// Simulate handler adding outbound messages to context
			ctx.SetItem("OutboundMessages", outboundMessages);
			return new ValueTask<IMessageResult>(TestFakes.MessageResult.Success());
		};

		// Act
		var result = await middleware.InvokeAsync(
			message, context, stagingDelegate, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		_ = A.CallTo(() => _outboxStore.StageMessageAsync(
			A<OutboundMessage>._, A<CancellationToken>._))
			.MustHaveHappened(2, Times.Exactly);
	}

	[Fact]
	public async Task NotStageMessages_WhenHandlerFails()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchActionMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };

		// Act
		var result = await middleware.InvokeAsync(
			message, context, CreateFailureDelegate("Handler failed"), CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeFalse();
		A.CallTo(() => _outboxStore.StageMessageAsync(
			A<OutboundMessage>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task HandleNoOutboundMessages_Gracefully()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchActionMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };
		// No outbound messages set in context
		var wasCalled = new[] { false };

		// Act
		var result = await middleware.InvokeAsync(
			message, context, CreateSuccessDelegate(wasCalled), CancellationToken.None);

		// Assert
		wasCalled[0].ShouldBeTrue();
		result.Succeeded.ShouldBeTrue();
		A.CallTo(() => _outboxStore.StageMessageAsync(
			A<OutboundMessage>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task SetCorrelationIdOnOutboundMessages_FromContext()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchActionMessage();
		var context = new FakeMessageContext
		{
			MessageId = "test-123",
			CorrelationId = "correlation-456"
		};

		var outboundMessage = new OutboundMessage("EventType", [1, 2, 3], "target-queue");
		var outboundMessages = new List<OutboundMessage> { outboundMessage };

		DispatchRequestDelegate stagingDelegate = (msg, ctx, ct) =>
		{
			ctx.SetItem("OutboundMessages", outboundMessages);
			return new ValueTask<IMessageResult>(TestFakes.MessageResult.Success());
		};

		// Act
		_ = await middleware.InvokeAsync(
			message, context, stagingDelegate, CancellationToken.None);

		// Assert - CorrelationId should have been set from context
		outboundMessage.CorrelationId.ShouldBe("correlation-456");
	}

	[Fact]
	public async Task SetTenantIdOnOutboundMessages_FromContext()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchActionMessage();
		var context = new FakeMessageContext
		{
			MessageId = "test-123",
			TenantId = "tenant-789"
		};

		var outboundMessage = new OutboundMessage("EventType", [1, 2, 3], "target-queue");
		var outboundMessages = new List<OutboundMessage> { outboundMessage };

		DispatchRequestDelegate stagingDelegate = (msg, ctx, ct) =>
		{
			ctx.SetItem("OutboundMessages", outboundMessages);
			return new ValueTask<IMessageResult>(TestFakes.MessageResult.Success());
		};

		// Act
		_ = await middleware.InvokeAsync(
			message, context, stagingDelegate, CancellationToken.None);

		// Assert
		outboundMessage.TenantId.ShouldBe("tenant-789");
	}

	[Fact]
	public async Task ApplyDefaultPriority_WhenOutboundMessageHasNoPriority()
	{
		// Arrange
		var options = new OutboxOptions { Enabled = true, DefaultPriority = 5 };
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchActionMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };

		var outboundMessage = new OutboundMessage("EventType", [1, 2, 3], "target-queue");
		// Priority defaults to 0
		var outboundMessages = new List<OutboundMessage> { outboundMessage };

		DispatchRequestDelegate stagingDelegate = (msg, ctx, ct) =>
		{
			ctx.SetItem("OutboundMessages", outboundMessages);
			return new ValueTask<IMessageResult>(TestFakes.MessageResult.Success());
		};

		// Act
		_ = await middleware.InvokeAsync(
			message, context, stagingDelegate, CancellationToken.None);

		// Assert
		outboundMessage.Priority.ShouldBe(5);
	}

	[Fact]
	public async Task NotOverridePriority_WhenOutboundMessageAlreadyHasPriority()
	{
		// Arrange
		var options = new OutboxOptions { Enabled = true, DefaultPriority = 5 };
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchActionMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };

		var outboundMessage = new OutboundMessage("EventType", [1, 2, 3], "target-queue");
		outboundMessage.Priority = 10; // Already has priority set
		var outboundMessages = new List<OutboundMessage> { outboundMessage };

		DispatchRequestDelegate stagingDelegate = (msg, ctx, ct) =>
		{
			ctx.SetItem("OutboundMessages", outboundMessages);
			return new ValueTask<IMessageResult>(TestFakes.MessageResult.Success());
		};

		// Act
		_ = await middleware.InvokeAsync(
			message, context, stagingDelegate, CancellationToken.None);

		// Assert - original priority should be preserved
		outboundMessage.Priority.ShouldBe(10);
	}

	[Fact]
	public async Task ClearOutboundMessages_FromContextAfterStaging()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchActionMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };

		var outboundMessages = new List<OutboundMessage>
		{
			new("EventType", [1, 2, 3], "target-queue")
		};

		DispatchRequestDelegate stagingDelegate = (msg, ctx, ct) =>
		{
			ctx.SetItem("OutboundMessages", outboundMessages);
			return new ValueTask<IMessageResult>(TestFakes.MessageResult.Success());
		};

		// Act
		_ = await middleware.InvokeAsync(
			message, context, stagingDelegate, CancellationToken.None);

		// Assert - outbound messages should have been cleared from context
		context.GetItem<List<OutboundMessage>>("OutboundMessages").ShouldBeNull();
	}

	#endregion

	#region Error Handling Tests

	[Fact]
	public async Task RethrowException_WhenHandlerThrows()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchActionMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };

		ValueTask<IMessageResult> ThrowingDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> throw new InvalidOperationException("Handler exploded");

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(
			middleware.InvokeAsync(message, context, ThrowingDelegate, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task RethrowStagingError_WhenContinueOnStagingErrorIsFalse()
	{
		// Arrange
		var options = new OutboxOptions { Enabled = true, ContinueOnStagingError = false };
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchActionMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };

		var outboundMessages = new List<OutboundMessage>
		{
			new("EventType", [1, 2, 3], "target-queue")
		};

		_ = A.CallTo(() => _outboxStore.StageMessageAsync(
			A<OutboundMessage>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Staging failed"));

		DispatchRequestDelegate stagingDelegate = (msg, ctx, ct) =>
		{
			ctx.SetItem("OutboundMessages", outboundMessages);
			return new ValueTask<IMessageResult>(TestFakes.MessageResult.Success());
		};

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(
			middleware.InvokeAsync(message, context, stagingDelegate, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ContinueStaging_WhenContinueOnStagingErrorIsTrue()
	{
		// Arrange
		var options = new OutboxOptions { Enabled = true, ContinueOnStagingError = true };
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchActionMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };

		var outboundMessages = new List<OutboundMessage>
		{
			new("FailingEvent", [1], "queue-a"),
			new("SucceedingEvent", [2], "queue-b"),
		};

		var stageCallCount = 0;
		_ = A.CallTo(() => _outboxStore.StageMessageAsync(
			A<OutboundMessage>._, A<CancellationToken>._))
			.Invokes(() =>
			{
				stageCallCount++;
				if (stageCallCount == 1)
				{
					throw new InvalidOperationException("First staging failed");
				}
			});

		DispatchRequestDelegate stagingDelegate = (msg, ctx, ct) =>
		{
			ctx.SetItem("OutboundMessages", outboundMessages);
			return new ValueTask<IMessageResult>(TestFakes.MessageResult.Success());
		};

		// Act - should NOT throw because ContinueOnStagingError is true
		var result = await middleware.InvokeAsync(
			message, context, stagingDelegate, CancellationToken.None);

		// Assert - both messages should have been attempted
		result.Succeeded.ShouldBeTrue();
		_ = A.CallTo(() => _outboxStore.StageMessageAsync(
			A<OutboundMessage>._, A<CancellationToken>._))
			.MustHaveHappened(2, Times.Exactly);
	}

	#endregion

	#region IDispatchAction and IDispatchEvent Handling Tests

	[Fact]
	public async Task ProcessOutbox_ForDispatchActionMessages()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchActionMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };

		var outboundMessages = new List<OutboundMessage>
		{
			new("EventType", [1, 2, 3], "target-queue")
		};

		DispatchRequestDelegate stagingDelegate = (msg, ctx, ct) =>
		{
			ctx.SetItem("OutboundMessages", outboundMessages);
			return new ValueTask<IMessageResult>(TestFakes.MessageResult.Success());
		};

		// Act
		var result = await middleware.InvokeAsync(
			message, context, stagingDelegate, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		_ = A.CallTo(() => _outboxStore.StageMessageAsync(
			A<OutboundMessage>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ProcessOutbox_ForDispatchEventMessages()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchEventMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };

		var outboundMessages = new List<OutboundMessage>
		{
			new("IntegrationEvent", [7, 8, 9], "events-topic")
		};

		DispatchRequestDelegate stagingDelegate = (msg, ctx, ct) =>
		{
			ctx.SetItem("OutboundMessages", outboundMessages);
			return new ValueTask<IMessageResult>(TestFakes.MessageResult.Success());
		};

		// Act
		var result = await middleware.InvokeAsync(
			message, context, stagingDelegate, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		_ = A.CallTo(() => _outboxStore.StageMessageAsync(
			A<OutboundMessage>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Test Fixtures

	/// <summary>
	/// Test message implementing IDispatchAction for outbox staging tests.
	/// </summary>
	private sealed class FakeDispatchActionMessage : IDispatchAction;

	/// <summary>
	/// Test message implementing IDispatchEvent for outbox staging tests.
	/// </summary>
	private sealed class FakeDispatchEventMessage : IDispatchEvent;

	/// <summary>
	/// Test message implementing only IDispatchMessage (not action or event).
	/// Outbox should NOT process this type.
	/// </summary>
	private sealed class FakeNonActionMessage : IDispatchMessage;

	/// <summary>
	/// Test message with BypassOutbox attribute.
	/// Outbox should skip staging for this type.
	/// </summary>
	[BypassOutbox]
	private sealed class BypassOutboxMessage : IDispatchAction;

	#endregion
}
