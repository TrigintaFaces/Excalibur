// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Tests.TestFakes;

using Microsoft.Extensions.Logging.Abstractions;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for the <see cref="IdempotentHandlerMiddleware"/> deduplication behavior.
/// Sprint 561 S561.39: Validates idempotency key extraction, duplicate detection, and cache integration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
public sealed class DeduplicationMiddlewareShould
{
	private readonly IInMemoryDeduplicator _inMemoryDeduplicator;
	private readonly IInboxStore _inboxStore;
	private readonly IMessageIdProvider _messageIdProvider;
	private readonly IInboxConfigurationProvider _configurationProvider;
	private readonly ILogger<IdempotentHandlerMiddleware> _logger;

	public DeduplicationMiddlewareShould()
	{
		_inMemoryDeduplicator = A.Fake<IInMemoryDeduplicator>();
		_inboxStore = A.Fake<IInboxStore>();
		_messageIdProvider = A.Fake<IMessageIdProvider>();
		_configurationProvider = A.Fake<IInboxConfigurationProvider>();
		_logger = NullLoggerFactory.Instance.CreateLogger<IdempotentHandlerMiddleware>();
	}

	private IdempotentHandlerMiddleware CreateMiddleware(
		IInboxStore? inboxStore = null,
		bool omitInboxStore = false)
	{
		return new IdempotentHandlerMiddleware(
			_inMemoryDeduplicator,
			_logger,
			omitInboxStore ? null : (inboxStore ?? _inboxStore),
			_messageIdProvider,
			_configurationProvider);
	}

	#region Idempotency Key Extraction Tests

	[Fact]
	public async Task ExtractKey_UsingDefaultMessageIdStrategy()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "key-from-message-id" };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(DefaultStrategyHandler);

		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(DefaultStrategyHandler)))
			.Returns(null);

		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync("key-from-message-id", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(false));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new(MessageResult.Success());

		// Act
		_ = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert - should use MessageId as the deduplication key
		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync("key-from-message-id", A<TimeSpan>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ExtractKey_UsingFromHeaderStrategy_WithCustomHeaderName()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "msg-id" };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(CustomHeaderHandler);
		context.Items["X-Idempotency-Key"] = "custom-key-abc";

		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(CustomHeaderHandler)))
			.Returns(null);

		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync("custom-key-abc", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(false));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new(MessageResult.Success());

		// Act
		_ = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync("custom-key-abc", A<TimeSpan>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ExtractKey_UsingCorrelationIdStrategy()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "msg-id", CorrelationId = "corr-id-xyz" };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(CorrelationIdHandler);

		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(CorrelationIdHandler)))
			.Returns(null);

		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync("corr-id-xyz", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(false));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new(MessageResult.Success());

		// Act
		_ = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync("corr-id-xyz", A<TimeSpan>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ExtractKey_UsingCompositeKeyStrategy_CombinesHandlerAndCorrelation()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "msg-id", CorrelationId = "corr-composite" };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(CompositeKeyHandler);

		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(CompositeKeyHandler)))
			.Returns(null);

		var expectedKey = $"{nameof(CompositeKeyHandler)}:corr-composite";
		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync(expectedKey, A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(false));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new(MessageResult.Success());

		// Act
		_ = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync(expectedKey, A<TimeSpan>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ExtractKey_UsingCustomStrategy_DelegatesToProvider()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "msg-id" };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(CustomProviderHandler);

		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(CustomProviderHandler)))
			.Returns(null);

		_ = A.CallTo(() => _messageIdProvider.GetMessageId(message, context))
			.Returns("custom-provider-key");

		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync("custom-provider-key", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(false));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new(MessageResult.Success());

		// Act
		_ = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _messageIdProvider.GetMessageId(message, context))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Duplicate Detection Tests

	[Fact]
	public async Task DetectDuplicate_AndSkipProcessing_WithInMemoryStore()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "dup-msg-1" };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(DefaultStrategyHandler);
		var handlerInvoked = false;

		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(DefaultStrategyHandler)))
			.Returns(null);

		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync("dup-msg-1", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			handlerInvoked = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		handlerInvoked.ShouldBeFalse("Handler should not be invoked for duplicate messages");
		result.IsSuccess.ShouldBeTrue("Duplicate detection should return success");
	}

	[Fact]
	public async Task DetectDuplicate_AndSkipProcessing_WithPersistentStore()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "dup-persist-1" };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(PersistentIdempotentHandler);
		var handlerInvoked = false;

		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(PersistentIdempotentHandler)))
			.Returns(null);

		_ = A.CallTo(() => _inboxStore.IsProcessedAsync("dup-persist-1", A<string>._, A<CancellationToken>._))
			.Returns(new ValueTask<bool>(true));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			handlerInvoked = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		handlerInvoked.ShouldBeFalse("Handler should not be invoked for persistent duplicate");
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task AllowProcessing_WhenMessageIsNotDuplicate()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "new-msg-1" };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(DefaultStrategyHandler);
		var handlerInvoked = false;

		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(DefaultStrategyHandler)))
			.Returns(null);

		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync("new-msg-1", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(false));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			handlerInvoked = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		handlerInvoked.ShouldBeTrue("Handler should be invoked for non-duplicate messages");
		result.IsSuccess.ShouldBeTrue();
	}

	#endregion

	#region Cache Integration Tests

	[Fact]
	public async Task MarkProcessed_InMemoryCache_AfterSuccessfulHandling()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "cache-msg-1" };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(DefaultStrategyHandler);

		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(DefaultStrategyHandler)))
			.Returns(null);

		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync("cache-msg-1", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(false));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new(MessageResult.Success());

		// Act
		_ = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _inMemoryDeduplicator.MarkProcessedAsync("cache-msg-1", A<TimeSpan>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotMarkProcessed_InMemoryCache_WhenHandlerFails()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "fail-msg-1" };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(DefaultStrategyHandler);

		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(DefaultStrategyHandler)))
			.Returns(null);

		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync("fail-msg-1", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(false));

		var failedResult = MessageResult.Failed(new MessageProblemDetails
		{
			Type = "test:error",
			Title = "Handler Failed",
			Status = 500,
			Detail = "Simulated failure",
		});

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new(failedResult);

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		A.CallTo(() => _inMemoryDeduplicator.MarkProcessedAsync(A<string>._, A<TimeSpan>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task MarkProcessed_InPersistentStore_AfterSuccessfulHandling()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "persist-cache-1" };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(PersistentIdempotentHandler);

		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(PersistentIdempotentHandler)))
			.Returns(null);

		_ = A.CallTo(() => _inboxStore.IsProcessedAsync("persist-cache-1", A<string>._, A<CancellationToken>._))
			.Returns(new ValueTask<bool>(false));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new(MessageResult.Success());

		// Act
		_ = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _inboxStore.TryMarkAsProcessedAsync("persist-cache-1", A<string>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task FallbackToInMemory_WhenPersistentStoreIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware(omitInboxStore: true);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "fallback-msg-1" };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(PersistentIdempotentHandler);

		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(PersistentIdempotentHandler)))
			.Returns(null);

		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync("fallback-msg-1", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(false));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new(MessageResult.Success());

		// Act
		_ = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert - should fall back to in-memory deduplicator
		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync("fallback-msg-1", A<TimeSpan>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => _inMemoryDeduplicator.MarkProcessedAsync("fallback-msg-1", A<TimeSpan>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RespectRetentionPeriod_FromAttribute()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "retention-msg" };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(ShortRetentionHandler);

		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(ShortRetentionHandler)))
			.Returns(null);

		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync(A<string>._, A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(false));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new(MessageResult.Success());

		// Act
		_ = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert - ShortRetentionHandler specifies 15 minutes retention
		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync(
			"retention-msg",
			TimeSpan.FromMinutes(15),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PreferConfigurationProvider_OverAttribute()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "provider-msg" };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(PersistentIdempotentHandler);

		// Provider returns in-memory config, overriding attribute's persistent setting
		var providerSettings = new InboxHandlerSettings
		{
			Retention = TimeSpan.FromMinutes(10),
			UseInMemory = true,
			Strategy = MessageIdStrategy.FromHeader,
		};
		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(PersistentIdempotentHandler)))
			.Returns(providerSettings);

		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync("provider-msg", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(false));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new(MessageResult.Success());

		// Act
		_ = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert - provider forced in-memory, so persistent store should NOT be called
		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync("provider-msg", A<TimeSpan>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _inboxStore.IsProcessedAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	#endregion

	#region Edge Case Tests

	[Fact]
	public async Task PassThrough_WhenNoHandlerTypeInContext()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "no-handler" };
		var handlerInvoked = false;

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			handlerInvoked = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		_ = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		handlerInvoked.ShouldBeTrue("Should pass through when no handler type is set");
		A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync(A<string>._, A<TimeSpan>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task PassThrough_WhenHandlerHasNoIdempotencyConfig()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "no-config" };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(NoIdempotentHandler);
		var handlerInvoked = false;

		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(NoIdempotentHandler)))
			.Returns(null);

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			handlerInvoked = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		_ = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		handlerInvoked.ShouldBeTrue("Should pass through when handler has no idempotent config");
	}

	[Fact]
	public async Task PassThrough_WhenMessageIdIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = null };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(DefaultStrategyHandler);
		var handlerInvoked = false;

		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(DefaultStrategyHandler)))
			.Returns(null);

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			handlerInvoked = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		_ = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		handlerInvoked.ShouldBeTrue("Should pass through when message ID cannot be extracted");
	}

	#endregion

	#region Test Handlers

	private sealed class NoIdempotentHandler { }

	[Idempotent(UseInMemory = true)]
	private sealed class DefaultStrategyHandler { }

	[Idempotent(UseInMemory = true, HeaderName = "X-Idempotency-Key")]
	private sealed class CustomHeaderHandler { }

	[Idempotent(UseInMemory = true, Strategy = MessageIdStrategy.FromCorrelationId)]
	private sealed class CorrelationIdHandler { }

	[Idempotent(UseInMemory = true, Strategy = MessageIdStrategy.CompositeKey)]
	private sealed class CompositeKeyHandler { }

	[Idempotent(UseInMemory = true, Strategy = MessageIdStrategy.Custom)]
	private sealed class CustomProviderHandler { }

	[Idempotent]
	private sealed class PersistentIdempotentHandler { }

	[Idempotent(UseInMemory = true, RetentionMinutes = 15)]
	private sealed class ShortRetentionHandler { }

	#endregion
}
