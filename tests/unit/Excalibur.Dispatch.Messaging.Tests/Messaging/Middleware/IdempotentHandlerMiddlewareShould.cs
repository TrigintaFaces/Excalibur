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
/// Unit tests for the <see cref="IdempotentHandlerMiddleware"/> class.
/// Validates deduplication behavior, message ID strategies, and configuration handling.
/// </summary>
/// <remarks>
/// Sprint 441 S441.6: Unit tests for IdempotentHandlerMiddleware.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class IdempotentHandlerMiddlewareShould
{
	private readonly IInMemoryDeduplicator _inMemoryDeduplicator;
	private readonly IInboxStore _inboxStore;
	private readonly IMessageIdProvider _messageIdProvider;
	private readonly IInboxConfigurationProvider _configurationProvider;
	private readonly ILogger<IdempotentHandlerMiddleware> _logger;

	public IdempotentHandlerMiddlewareShould()
	{
		_inMemoryDeduplicator = A.Fake<IInMemoryDeduplicator>();
		_inboxStore = A.Fake<IInboxStore>();
		_messageIdProvider = A.Fake<IMessageIdProvider>();
		_configurationProvider = A.Fake<IInboxConfigurationProvider>();
		_logger = NullLoggerFactory.Instance.CreateLogger<IdempotentHandlerMiddleware>();
	}

	private IdempotentHandlerMiddleware CreateMiddleware(
		IInMemoryDeduplicator? deduplicator = null,
		IInboxStore? inboxStore = null,
		IMessageIdProvider? messageIdProvider = null,
		IInboxConfigurationProvider? configurationProvider = null,
		bool omitInboxStore = false)
	{
		return new IdempotentHandlerMiddleware(
			deduplicator ?? _inMemoryDeduplicator,
			_logger,
			omitInboxStore ? null : (inboxStore ?? _inboxStore),
			messageIdProvider ?? _messageIdProvider,
			configurationProvider ?? _configurationProvider);
	}

	#region Constructor and Stage Tests

	[Fact]
	public void HaveCorrectStage()
	{
		// Arrange
		var middleware = CreateMiddleware();

		// Assert
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.Processing - 1);
	}

	[Fact]
	public void ThrowArgumentNullException_WhenDeduplicatorIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new IdempotentHandlerMiddleware(null!, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new IdempotentHandlerMiddleware(_inMemoryDeduplicator, null!));
	}

	[Fact]
	public void AllowOptionalDependencies()
	{
		// Act - all optional dependencies are null
		var middleware = new IdempotentHandlerMiddleware(_inMemoryDeduplicator, _logger);

		// Assert
		_ = middleware.ShouldNotBeNull();
	}

	#endregion

	#region Pass-Through Behavior Tests

	[Fact]
	public async Task PassThrough_WhenNoHandlerTypeInContext()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };
		var expectedResult = MessageResult.Success();
		var wasCalled = false;

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			wasCalled = true;
			return new ValueTask<IMessageResult>(expectedResult);
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		wasCalled.ShouldBeTrue();
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task PassThrough_WhenHandlerHasNoIdempotentAttribute()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(NonIdempotentHandler);
		var expectedResult = MessageResult.Success();
		var wasCalled = false;

		// No configuration from provider
		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(NonIdempotentHandler)))
			.Returns(null);

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			wasCalled = true;
			return new ValueTask<IMessageResult>(expectedResult);
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		wasCalled.ShouldBeTrue();
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task PassThrough_WhenMessageIdCannotBeExtracted()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = null }; // No MessageId
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(IdempotentHandler);
		var expectedResult = MessageResult.Success();
		var wasCalled = false;

		// No configuration from provider, will use attribute
		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(IdempotentHandler)))
			.Returns(null);

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			wasCalled = true;
			return new ValueTask<IMessageResult>(expectedResult);
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		wasCalled.ShouldBeTrue();
		result.ShouldBe(expectedResult);
	}

	#endregion

	#region Duplicate Detection with Attribute Tests

	[Fact]
	public async Task SkipHandler_WhenDuplicateDetected_WithInMemoryDeduplicator()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "duplicate-123" };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(IdempotentInMemoryHandler);
		var wasCalled = false;

		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(IdempotentInMemoryHandler)))
			.Returns(null);

		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync("duplicate-123", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			wasCalled = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		wasCalled.ShouldBeFalse(); // Handler should NOT be called for duplicate
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task ProcessMessage_WhenNotDuplicate_WithInMemoryDeduplicator()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "new-123" };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(IdempotentInMemoryHandler);
		var wasCalled = false;

		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(IdempotentInMemoryHandler)))
			.Returns(null);

		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync("new-123", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(false));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			wasCalled = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		wasCalled.ShouldBeTrue();
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task MarkAsProcessed_WhenHandlerSucceeds()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "new-123" };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(IdempotentInMemoryHandler);

		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(IdempotentInMemoryHandler)))
			.Returns(null);

		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync("new-123", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(false));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		_ = A.CallTo(() => _inMemoryDeduplicator.MarkProcessedAsync("new-123", A<TimeSpan>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotMarkAsProcessed_WhenHandlerFails()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "new-123" };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(IdempotentInMemoryHandler);

		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(IdempotentInMemoryHandler)))
			.Returns(null);

		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync("new-123", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(false));

		var failedResult = MessageResult.Failed(new MessageProblemDetails
		{
			Type = "test:error",
			Title = "Test Error",
			Status = 500,
			Detail = "Test failure",
		});

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new ValueTask<IMessageResult>(failedResult);

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		A.CallTo(() => _inMemoryDeduplicator.MarkProcessedAsync(A<string>._, A<TimeSpan>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	#endregion

	#region Persistent Inbox Store Tests

	[Fact]
	public async Task UsePersistentStore_WhenUseInMemoryIsFalse()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "persistent-123" };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(IdempotentHandler);

		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(IdempotentHandler)))
			.Returns(null);

		_ = A.CallTo(() => _inboxStore.IsProcessedAsync("persistent-123", A<string>._, A<CancellationToken>._))
			.Returns(new ValueTask<bool>(false));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _inboxStore.IsProcessedAsync("persistent-123", A<string>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MarkAsProcessedInPersistentStore_WhenHandlerSucceeds()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "persistent-123" };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(IdempotentHandler);

		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(IdempotentHandler)))
			.Returns(null);

		_ = A.CallTo(() => _inboxStore.IsProcessedAsync("persistent-123", A<string>._, A<CancellationToken>._))
			.Returns(new ValueTask<bool>(false));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _inboxStore.TryMarkAsProcessedAsync("persistent-123", A<string>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task FallbackToInMemory_WhenNoInboxStoreConfigured()
	{
		// Arrange - create middleware without inbox store
		var middleware = CreateMiddleware(omitInboxStore: true);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "fallback-123" };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(IdempotentHandler);

		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(IdempotentHandler)))
			.Returns(null);

		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync("fallback-123", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(false));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert - should use in-memory fallback
		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync("fallback-123", A<TimeSpan>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Message ID Strategy Tests

	[Fact]
	public async Task UseFromHeaderStrategy_WhenConfigured()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "header-id-123" };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(IdempotentHandler);

		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(IdempotentHandler)))
			.Returns(null);

		_ = A.CallTo(() => _inboxStore.IsProcessedAsync("header-id-123", A<string>._, A<CancellationToken>._))
			.Returns(new ValueTask<bool>(false));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _inboxStore.IsProcessedAsync("header-id-123", A<string>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task UseFromCorrelationIdStrategy_WhenConfigured()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext
		{
			MessageId = "header-id",
			CorrelationId = "correlation-id-123"
		};
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(CorrelationIdHandler);

		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(CorrelationIdHandler)))
			.Returns(null);

		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync("correlation-id-123", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(false));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync("correlation-id-123", A<TimeSpan>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task UseCompositeKeyStrategy_WhenConfigured()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext
		{
			MessageId = "header-id",
			CorrelationId = "correlation-id-123"
		};
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(CompositeKeyHandler);

		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(CompositeKeyHandler)))
			.Returns(null);

		var expectedKey = $"{nameof(CompositeKeyHandler)}:correlation-id-123";
		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync(expectedKey, A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(false));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync(expectedKey, A<TimeSpan>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task UseCustomStrategy_WhenConfigured()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "header-id" };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(CustomStrategyHandler);

		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(CustomStrategyHandler)))
			.Returns(null);

		_ = A.CallTo(() => _messageIdProvider.GetMessageId(message, context))
			.Returns("custom-id-123");

		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync("custom-id-123", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(false));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _messageIdProvider.GetMessageId(message, context))
			.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync("custom-id-123", A<TimeSpan>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Configuration Provider Precedence Tests

	[Fact]
	public async Task UseConfigurationProvider_WhenBothProviderAndAttributeExist()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "provider-123" };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(IdempotentHandler);

		// Provider returns custom settings
		var providerSettings = new InboxHandlerSettings
		{
			Retention = TimeSpan.FromMinutes(30),
			UseInMemory = true,
			Strategy = MessageIdStrategy.FromHeader
		};
		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(IdempotentHandler)))
			.Returns(providerSettings);

		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync("provider-123", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(false));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert - should use in-memory (from provider) not persistent (from attribute default)
		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync("provider-123", A<TimeSpan>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _inboxStore.IsProcessedAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	#endregion

	#region Retention Period Tests

	[Fact]
	public async Task UseCorrectRetentionPeriod_FromAttribute()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "retention-123" };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(ShortRetentionHandler);

		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(ShortRetentionHandler)))
			.Returns(null);

		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync(A<string>._, A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(false));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		_ = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert - ShortRetentionHandler has RetentionMinutes = 30
		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync(
			"retention-123",
			TimeSpan.FromMinutes(30),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Custom Header Name Tests

	[Fact]
	public async Task UseCustomHeaderName_WhenConfigured()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "header-id" };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(CustomHeaderHandler);
		context.Items["X-Custom-Id"] = "custom-header-value-123";

		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(CustomHeaderHandler)))
			.Returns(null);

		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync("custom-header-value-123", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(false));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _inMemoryDeduplicator.IsDuplicateAsync("custom-header-value-123", A<TimeSpan>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Null Guard Tests

	[Fact]
	public async Task ThrowArgumentNullException_WhenMessageIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var context = new FakeMessageContext { MessageId = "test-123" };

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new ValueTask<IMessageResult>(MessageResult.Success());

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(null!, context, NextDelegate, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new ValueTask<IMessageResult>(MessageResult.Success());

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(message, null!, NextDelegate, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenNextDelegateIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(message, context, null!, CancellationToken.None).AsTask());
	}

	#endregion

	#region Test Handlers

	/// <summary>
	/// Handler without [Idempotent] attribute.
	/// </summary>
	private sealed class NonIdempotentHandler { }

	/// <summary>
	/// Handler with [Idempotent] using default settings (UseInMemory = false).
	/// </summary>
	[Idempotent]
	private sealed class IdempotentHandler { }

	/// <summary>
	/// Handler with [Idempotent] using in-memory deduplication.
	/// </summary>
	[Idempotent(UseInMemory = true)]
	private sealed class IdempotentInMemoryHandler { }

	/// <summary>
	/// Handler using FromCorrelationId strategy.
	/// </summary>
	[Idempotent(UseInMemory = true, Strategy = MessageIdStrategy.FromCorrelationId)]
	private sealed class CorrelationIdHandler { }

	/// <summary>
	/// Handler using CompositeKey strategy.
	/// </summary>
	[Idempotent(UseInMemory = true, Strategy = MessageIdStrategy.CompositeKey)]
	private sealed class CompositeKeyHandler { }

	/// <summary>
	/// Handler using Custom strategy.
	/// </summary>
	[Idempotent(UseInMemory = true, Strategy = MessageIdStrategy.Custom)]
	private sealed class CustomStrategyHandler { }

	/// <summary>
	/// Handler with short retention period.
	/// </summary>
	[Idempotent(UseInMemory = true, RetentionMinutes = 30)]
	private sealed class ShortRetentionHandler { }

	/// <summary>
	/// Handler with custom header name.
	/// </summary>
	[Idempotent(UseInMemory = true, HeaderName = "X-Custom-Id")]
	private sealed class CustomHeaderHandler { }

	#endregion
}
