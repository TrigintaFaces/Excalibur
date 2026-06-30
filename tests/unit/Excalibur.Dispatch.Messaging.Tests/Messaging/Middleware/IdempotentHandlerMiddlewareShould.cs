// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Middleware.Inbox;
using Excalibur.Dispatch.Options.Delivery;
using Tests.Shared.TestFakes;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MessageResult = Excalibur.Dispatch.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for the <see cref="IdempotentHandlerMiddleware"/> class.
/// Validates deduplication behavior, message ID strategies, and configuration handling.
/// </summary>
/// <remarks>
/// Sprint 441 S441.6: Unit tests for IdempotentHandlerMiddleware.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class IdempotentHandlerMiddlewareShould
{
	private readonly IInMemoryDeduplicator _inMemoryDeduplicator;
	private readonly IClaimableDeduplicator _claimableDedup;
	private readonly IInboxStore _inboxStore;
	private readonly IMessageIdProvider _messageIdProvider;
	private readonly IInboxConfigurationProvider _configurationProvider;
	private readonly ILogger<IdempotentHandlerMiddleware> _logger;

	public IdempotentHandlerMiddlewareShould()
	{
		// bd-53hho5: the in-memory idempotency path now requires an atomic claim (use-site fail-loud,
		// ADR-336 cl.2). The default InMemoryDeduplicator is claim-capable, so the fixture's deduplicator
		// implements IClaimableDeduplicator and the dedup-path tests assert the claim contract
		// (TryClaimAsync first-writer-wins; a successful claim IS the dedup marker; ReleaseAsync on failure).
		_inMemoryDeduplicator = A.Fake<IInMemoryDeduplicator>(o => o.Implements<IClaimableDeduplicator>());
		_claimableDedup = (IClaimableDeduplicator)_inMemoryDeduplicator;
		_inboxStore = A.Fake<IInboxStore>();
		_messageIdProvider = A.Fake<IMessageIdProvider>();
		_configurationProvider = A.Fake<IInboxConfigurationProvider>();
		_logger = NullLoggerFactory.Instance.CreateLogger<IdempotentHandlerMiddleware>();
	}

	private static IOptions<InboxOptions> CreateInboxOptions(
		Excalibur.Dispatch.Messaging.SkipBehavior duplicateBehavior = Excalibur.Dispatch.Messaging.SkipBehavior.Silent)
	{
		return Microsoft.Extensions.Options.Options.Create(new InboxOptions
		{
			DuplicateBehavior = duplicateBehavior,
		});
	}

	private IdempotentHandlerMiddleware CreateMiddleware(
		IInMemoryDeduplicator? deduplicator = null,
		IInboxStore? inboxStore = null,
		IMessageIdProvider? messageIdProvider = null,
		IInboxConfigurationProvider? configurationProvider = null,
		bool omitInboxStore = false,
		Excalibur.Dispatch.Messaging.SkipBehavior duplicateBehavior = Excalibur.Dispatch.Messaging.SkipBehavior.Silent)
	{
		return new IdempotentHandlerMiddleware(
			CreateInboxOptions(duplicateBehavior),
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
	public void ThrowArgumentNullException_WhenInboxOptionsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new IdempotentHandlerMiddleware(null!, _inMemoryDeduplicator, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenDeduplicatorIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new IdempotentHandlerMiddleware(CreateInboxOptions(), null!, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new IdempotentHandlerMiddleware(CreateInboxOptions(), _inMemoryDeduplicator, null!));
	}

	[Fact]
	public void AllowOptionalDependencies()
	{
		// Act - all optional dependencies are null
		var middleware = new IdempotentHandlerMiddleware(
			CreateInboxOptions(), _inMemoryDeduplicator, _logger);

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

		// Claim fails (false) -> the message is a duplicate already claimed/processed -> skip.
		_ = A.CallTo(() => _claimableDedup.TryClaimAsync("duplicate-123", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(false));

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

		// Claim succeeds (true = first writer) -> not a duplicate -> handler runs.
		_ = A.CallTo(() => _claimableDedup.TryClaimAsync("new-123", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));

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
	public async Task KeepClaimAsMarker_WhenHandlerSucceeds()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "new-123" };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(IdempotentInMemoryHandler);

		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(IdempotentInMemoryHandler)))
			.Returns(null);

		_ = A.CallTo(() => _claimableDedup.TryClaimAsync("new-123", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert — the successful claim IS the dedup marker (atomic claim-before-execute): the claim was
		// taken and, on success, NOT released (a redelivery must now see it as a duplicate). This is the
		// new contract; the old separate MarkProcessedAsync finalize no longer applies to the claim path.
		result.IsSuccess.ShouldBeTrue();
		_ = A.CallTo(() => _claimableDedup.TryClaimAsync("new-123", A<TimeSpan>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _claimableDedup.ReleaseAsync(A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ReleaseClaim_WhenHandlerFails()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "new-123" };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(IdempotentInMemoryHandler);

		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(IdempotentInMemoryHandler)))
			.Returns(null);

		_ = A.CallTo(() => _claimableDedup.TryClaimAsync("new-123", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));

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

		// Assert — on handler failure the claim MUST be released so a redelivery can re-admit the message
		// (preserve at-least-once-until-success; never silently drop a failed message).
		result.IsSuccess.ShouldBeFalse();
		_ = A.CallTo(() => _claimableDedup.ReleaseAsync("new-123", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
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

		_ = A.CallTo(() => _claimableDedup.TryClaimAsync("fallback-123", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert - should use in-memory fallback (atomic claim path)
		_ = A.CallTo(() => _claimableDedup.TryClaimAsync("fallback-123", A<TimeSpan>._, A<CancellationToken>._))
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

		_ = A.CallTo(() => _claimableDedup.TryClaimAsync("correlation-id-123", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _claimableDedup.TryClaimAsync("correlation-id-123", A<TimeSpan>._, A<CancellationToken>._))
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
		_ = A.CallTo(() => _claimableDedup.TryClaimAsync(expectedKey, A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _claimableDedup.TryClaimAsync(expectedKey, A<TimeSpan>._, A<CancellationToken>._))
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

		_ = A.CallTo(() => _claimableDedup.TryClaimAsync("custom-id-123", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _messageIdProvider.GetMessageId(message, context))
			.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => _claimableDedup.TryClaimAsync("custom-id-123", A<TimeSpan>._, A<CancellationToken>._))
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

		_ = A.CallTo(() => _claimableDedup.TryClaimAsync("provider-123", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert - should use in-memory (from provider) not persistent (from attribute default)
		_ = A.CallTo(() => _claimableDedup.TryClaimAsync("provider-123", A<TimeSpan>._, A<CancellationToken>._))
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

		_ = A.CallTo(() => _claimableDedup.TryClaimAsync(A<string>._, A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		_ = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert - ShortRetentionHandler has RetentionMinutes = 30; the claim retention must carry it through.
		_ = A.CallTo(() => _claimableDedup.TryClaimAsync(
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

		_ = A.CallTo(() => _claimableDedup.TryClaimAsync("custom-header-value-123", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _claimableDedup.TryClaimAsync("custom-header-value-123", A<TimeSpan>._, A<CancellationToken>._))
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

	#region Use-Site Fail-Loud Tests (bd-53hho5)

	// bd-53hho5 (ADR-336 cl.2) — the in-memory idempotency path requires an atomic claim. If the registered
	// deduplicator cannot claim, the middleware MUST fail loud at the point of use rather than silently fall to
	// the non-atomic check-then-act path (under which concurrent duplicates could both run the handler). The
	// guard is conditional: it fires ONLY when the in-memory path is selected AND the deduplicator is non-claim;
	// a claim-capable deduplicator (every flipped test above) runs clean — no false-fail. Non-vacuous: RED on
	// the pre-fix impl that fell through to InvokeLegacyAsync instead of throwing.

	[Fact]
	public async Task FailLoud_WhenAttributeSelectsInMemory_ButDeduplicatorCannotClaim()
	{
		// Arrange — UseInMemory via [Idempotent(UseInMemory = true)] + a deduplicator that is NOT claim-capable.
		var nonClaimDedup = A.Fake<IInMemoryDeduplicator>(); // does NOT implement IClaimableDeduplicator
		var middleware = CreateMiddleware(deduplicator: nonClaimDedup);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "fail-loud-123" };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(IdempotentInMemoryHandler);

		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(IdempotentInMemoryHandler)))
			.Returns(null);

		var wasCalled = false;
		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			wasCalled = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act & Assert — fail loud, never a silent non-atomic fallback; the handler must not run.
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).AsTask());
		ex.Message.ShouldContain("IClaimableDeduplicator");
		wasCalled.ShouldBeFalse();
	}

	[Fact]
	public async Task FailLoud_WhenNoInboxStoreFallbackToInMemory_ButDeduplicatorCannotClaim()
	{
		// Arrange — no inbox store configured (the implicit in-memory fallback else-branch) + non-claim dedup.
		var nonClaimDedup = A.Fake<IInMemoryDeduplicator>();
		var middleware = CreateMiddleware(deduplicator: nonClaimDedup, omitInboxStore: true);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "fail-loud-fallback-123" };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = typeof(IdempotentHandler);

		_ = A.CallTo(() => _configurationProvider.GetConfiguration(typeof(IdempotentHandler)))
			.Returns(null);

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new ValueTask<IMessageResult>(MessageResult.Success());

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).AsTask());
		ex.Message.ShouldContain("IClaimableDeduplicator");
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
