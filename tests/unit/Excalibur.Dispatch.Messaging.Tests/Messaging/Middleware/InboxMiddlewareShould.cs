// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Tests.TestFakes;

using Microsoft.Extensions.Logging.Abstractions;

using InboxOptions = Excalibur.Dispatch.Options.Configuration.InboxOptions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for the <see cref="InboxMiddleware"/> class.
/// Validates inbox deduplication, full inbox mode, light mode, and error handling.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InboxMiddlewareShould
{
	private readonly IInboxStore _inboxStore;
	private readonly IInMemoryDeduplicator _deduplicator;
	private readonly ILogger<InboxMiddleware> _logger;

	public InboxMiddlewareShould()
	{
		_inboxStore = A.Fake<IInboxStore>();
		_deduplicator = A.Fake<IInMemoryDeduplicator>();
		_logger = NullLoggerFactory.Instance.CreateLogger<InboxMiddleware>();
	}

	private InboxMiddleware CreateMiddleware(
		InboxOptions? options = null,
		IInboxStore? inboxStore = null,
		IInMemoryDeduplicator? deduplicator = null,
		bool omitInboxStore = false,
		bool omitDeduplicator = false)
	{
		var opts = options ?? new InboxOptions { Enabled = true };
		return new InboxMiddleware(
			MsOptions.Create(opts),
			omitInboxStore ? null : (inboxStore ?? _inboxStore),
			omitDeduplicator ? null : (deduplicator ?? _deduplicator),
			_logger);
	}

	private static DispatchRequestDelegate SuccessDelegate()
	{
		return (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(TestFakes.MessageResult.Success());
	}

	private static DispatchRequestDelegate TrackingSuccessDelegate(bool[] wasCalled)
	{
		return (msg, ctx, ct) =>
		{
			wasCalled[0] = true;
			return new ValueTask<IMessageResult>(TestFakes.MessageResult.Success());
		};
	}

	private static DispatchRequestDelegate FailureDelegate(string errorMessage)
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
			new InboxMiddleware(null!, _inboxStore, _deduplicator, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new InboxOptions());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new InboxMiddleware(options, _inboxStore, _deduplicator, null!));
	}

	[Fact]
	public void ThrowInvalidOperationException_WhenEnabledButNoStoreOrDeduplicator()
	{
		// Arrange
		var options = MsOptions.Create(new InboxOptions { Enabled = true });

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			new InboxMiddleware(options, inboxStore: null, deduplicator: null, _logger));
	}

	[Fact]
	public void ConstructSuccessfully_WhenDisabledWithoutStoreOrDeduplicator()
	{
		// Arrange
		var options = MsOptions.Create(new InboxOptions { Enabled = false });

		// Act
		var middleware = new InboxMiddleware(options, inboxStore: null, deduplicator: null, _logger);

		// Assert
		_ = middleware.ShouldNotBeNull();
	}

	[Fact]
	public void ConstructSuccessfully_WhenEnabledWithInboxStore()
	{
		// Arrange
		var options = MsOptions.Create(new InboxOptions { Enabled = true });

		// Act
		var middleware = new InboxMiddleware(options, _inboxStore, deduplicator: null, _logger);

		// Assert
		_ = middleware.ShouldNotBeNull();
	}

	[Fact]
	public void ConstructSuccessfully_WhenEnabledWithDeduplicator()
	{
		// Arrange
		var options = MsOptions.Create(new InboxOptions { Enabled = true });

		// Act
		var middleware = new InboxMiddleware(options, inboxStore: null, _deduplicator, _logger);

		// Assert
		_ = middleware.ShouldNotBeNull();
	}

	#endregion

	#region Stage and ApplicableMessageKinds Tests

	[Fact]
	public void HavePreProcessingStage()
	{
		// Arrange
		var middleware = CreateMiddleware();

		// Assert
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
	}

	[Fact]
	public void ApplyToAllMessageKinds()
	{
		// Arrange
		var middleware = CreateMiddleware();

		// Assert
		middleware.ApplicableMessageKinds.ShouldBe(MessageKinds.All);
	}

	#endregion

	#region InvokeAsync Parameter Validation Tests

	[Fact]
	public async Task ThrowArgumentNullException_WhenMessageIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var context = new FakeMessageContext { MessageId = "test-123" };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(null!, context, SuccessDelegate(), CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(message, null!, SuccessDelegate(), CancellationToken.None).AsTask());
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

	#region Disabled Mode Tests

	[Fact]
	public async Task PassThroughDirectly_WhenDisabled()
	{
		// Arrange
		var middleware = CreateMiddleware(new InboxOptions { Enabled = false });
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };
		var wasCalled = new[] { false };

		// Act
		var result = await middleware.InvokeAsync(
			message, context, TrackingSuccessDelegate(wasCalled), CancellationToken.None);

		// Assert
		wasCalled[0].ShouldBeTrue();
		result.Succeeded.ShouldBeTrue();
	}

	#endregion

	#region Message ID Extraction Tests

	[Fact]
	public async Task PassThroughDirectly_WhenNoMessageIdCanBeExtracted()
	{
		// Arrange - InboxMiddleware looks for context items "MessageId" and "MessageEnvelope",
		// then reflection on message type. Here we ensure none of those are set.
		var middleware = CreateMiddleware();
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.GetItem<object>("MessageId")).Returns(null);
		_ = A.CallTo(() => context.GetItem<object>("MessageEnvelope")).Returns(null);
		var wasCalled = new[] { false };

		// Act
		var result = await middleware.InvokeAsync(
			message, context, TrackingSuccessDelegate(wasCalled), CancellationToken.None);

		// Assert - should pass through without inbox processing
		wasCalled[0].ShouldBeTrue();
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task ExtractMessageIdFromContextItems()
	{
		// Arrange
		var middleware = CreateMiddleware(omitInboxStore: true);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		context.SetItem<object>("MessageId", "context-msg-id-123");

		_ = A.CallTo(() => _deduplicator.IsDuplicateAsync(
			"context-msg-id-123", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(false));

		var wasCalled = new[] { false };

		// Act
		var result = await middleware.InvokeAsync(
			message, context, TrackingSuccessDelegate(wasCalled), CancellationToken.None);

		// Assert
		wasCalled[0].ShouldBeTrue();
		_ = A.CallTo(() => _deduplicator.IsDuplicateAsync(
			"context-msg-id-123", A<TimeSpan>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Light Mode (In-Memory Deduplicator) Tests

	[Fact]
	public async Task CallNextDelegate_WhenMessageIsNotDuplicate_LightMode()
	{
		// Arrange
		var middleware = CreateMiddleware(omitInboxStore: true);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		context.SetItem<object>("MessageId", "new-msg-123");

		_ = A.CallTo(() => _deduplicator.IsDuplicateAsync(
			"new-msg-123", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(false));

		var wasCalled = new[] { false };

		// Act
		var result = await middleware.InvokeAsync(
			message, context, TrackingSuccessDelegate(wasCalled), CancellationToken.None);

		// Assert
		wasCalled[0].ShouldBeTrue();
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task SkipProcessing_WhenMessageIsDuplicate_LightMode()
	{
		// Arrange
		var middleware = CreateMiddleware(omitInboxStore: true);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		context.SetItem<object>("MessageId", "duplicate-msg-123");

		_ = A.CallTo(() => _deduplicator.IsDuplicateAsync(
			"duplicate-msg-123", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));

		var wasCalled = new[] { false };

		// Act
		var result = await middleware.InvokeAsync(
			message, context, TrackingSuccessDelegate(wasCalled), CancellationToken.None);

		// Assert
		wasCalled[0].ShouldBeFalse();
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task MarkAsProcessed_AfterSuccessfulHandling_LightMode()
	{
		// Arrange
		var middleware = CreateMiddleware(omitInboxStore: true);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		context.SetItem<object>("MessageId", "new-msg-456");

		_ = A.CallTo(() => _deduplicator.IsDuplicateAsync(
			"new-msg-456", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(false));

		// Act
		var result = await middleware.InvokeAsync(
			message, context, SuccessDelegate(), CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		_ = A.CallTo(() => _deduplicator.MarkProcessedAsync(
			"new-msg-456", A<TimeSpan>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotMarkAsProcessed_WhenHandlerFails_LightMode()
	{
		// Arrange
		var middleware = CreateMiddleware(omitInboxStore: true);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		context.SetItem<object>("MessageId", "fail-msg-789");

		_ = A.CallTo(() => _deduplicator.IsDuplicateAsync(
			"fail-msg-789", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(false));

		// Act
		var result = await middleware.InvokeAsync(
			message, context, FailureDelegate("Processing failed"), CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeFalse();
		A.CallTo(() => _deduplicator.MarkProcessedAsync(
			A<string>._, A<TimeSpan>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task RethrowException_WhenHandlerThrows_LightMode()
	{
		// Arrange
		var middleware = CreateMiddleware(omitInboxStore: true);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		context.SetItem<object>("MessageId", "throw-msg-101");

		_ = A.CallTo(() => _deduplicator.IsDuplicateAsync(
			"throw-msg-101", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(false));

		ValueTask<IMessageResult> ThrowingDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> throw new InvalidOperationException("Handler exploded");

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(
			middleware.InvokeAsync(message, context, ThrowingDelegate, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task UseConfiguredDeduplicationExpiry_LightMode()
	{
		// Arrange
		var options = new InboxOptions { Enabled = true, DeduplicationExpiryHours = 12 };
		var middleware = CreateMiddleware(options, omitInboxStore: true);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		context.SetItem<object>("MessageId", "expiry-msg-202");

		_ = A.CallTo(() => _deduplicator.IsDuplicateAsync(
			"expiry-msg-202", TimeSpan.FromHours(12), A<CancellationToken>._))
			.Returns(Task.FromResult(false));

		// Act
		_ = await middleware.InvokeAsync(
			message, context, SuccessDelegate(), CancellationToken.None);

		// Assert - verify the correct expiry TimeSpan was used
		_ = A.CallTo(() => _deduplicator.IsDuplicateAsync(
			"expiry-msg-202", TimeSpan.FromHours(12), A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Full Inbox Mode (IInboxStore) Tests

	[Fact]
	public async Task SkipProcessing_WhenAlreadyProcessed_FullInboxMode()
	{
		// Arrange
		var middleware = CreateMiddleware(omitDeduplicator: true);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		context.SetItem<object>("MessageId", "processed-msg-301");

		_ = A.CallTo(() => _inboxStore.IsProcessedAsync(
			"processed-msg-301", A<string>._, A<CancellationToken>._))
			.Returns(new ValueTask<bool>(true));

		var wasCalled = new[] { false };

		// Act
		var result = await middleware.InvokeAsync(
			message, context, TrackingSuccessDelegate(wasCalled), CancellationToken.None);

		// Assert
		wasCalled[0].ShouldBeFalse();
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task SkipProcessing_WhenEntryIsInProcessingState_FullInboxMode()
	{
		// Arrange
		var middleware = CreateMiddleware(omitDeduplicator: true);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		context.SetItem<object>("MessageId", "in-progress-msg-302");

		_ = A.CallTo(() => _inboxStore.IsProcessedAsync(
			"in-progress-msg-302", A<string>._, A<CancellationToken>._))
			.Returns(new ValueTask<bool>(false));

		var existingEntry = new InboxEntry
		{
			MessageId = "in-progress-msg-302",
			Status = InboxStatus.Processing
		};
		_ = A.CallTo(() => _inboxStore.GetEntryAsync(
			"in-progress-msg-302", A<string>._, A<CancellationToken>._))
			.Returns(new ValueTask<InboxEntry?>(existingEntry));

		var wasCalled = new[] { false };

		// Act
		var result = await middleware.InvokeAsync(
			message, context, TrackingSuccessDelegate(wasCalled), CancellationToken.None);

		// Assert
		wasCalled[0].ShouldBeFalse();
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task RetryProcessing_WhenEntryIsInFailedState_FullInboxMode()
	{
		// Arrange
		var middleware = CreateMiddleware(omitDeduplicator: true);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		context.SetItem<object>("MessageId", "failed-msg-303");

		_ = A.CallTo(() => _inboxStore.IsProcessedAsync(
			"failed-msg-303", A<string>._, A<CancellationToken>._))
			.Returns(new ValueTask<bool>(false));

		var failedEntry = new InboxEntry
		{
			MessageId = "failed-msg-303",
			Status = InboxStatus.Failed
		};
		_ = A.CallTo(() => _inboxStore.GetEntryAsync(
			"failed-msg-303", A<string>._, A<CancellationToken>._))
			.Returns(new ValueTask<InboxEntry?>(failedEntry));

		var wasCalled = new[] { false };

		// Act
		var result = await middleware.InvokeAsync(
			message, context, TrackingSuccessDelegate(wasCalled), CancellationToken.None);

		// Assert - failed entries should be retried (handler called)
		wasCalled[0].ShouldBeTrue();
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task CreateNewEntry_WhenNoExistingEntry_FullInboxMode()
	{
		// Arrange
		var middleware = CreateMiddleware(omitDeduplicator: true);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		context.SetItem<object>("MessageId", "new-msg-304");

		_ = A.CallTo(() => _inboxStore.IsProcessedAsync(
			"new-msg-304", A<string>._, A<CancellationToken>._))
			.Returns(new ValueTask<bool>(false));

		_ = A.CallTo(() => _inboxStore.GetEntryAsync(
			"new-msg-304", A<string>._, A<CancellationToken>._))
			.Returns(new ValueTask<InboxEntry?>((InboxEntry?)null));

		var createdEntry = new InboxEntry { MessageId = "new-msg-304" };
		_ = A.CallTo(() => _inboxStore.CreateEntryAsync(
			"new-msg-304",
			A<string>._,
			A<string>._,
			A<byte[]>._,
			A<IDictionary<string, object>>._,
			A<CancellationToken>._))
			.Returns(new ValueTask<InboxEntry>(createdEntry));

		// Act
		var result = await middleware.InvokeAsync(
			message, context, SuccessDelegate(), CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		_ = A.CallTo(() => _inboxStore.CreateEntryAsync(
			"new-msg-304",
			A<string>._,
			A<string>._,
			A<byte[]>._,
			A<IDictionary<string, object>>._,
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MarkAsProcessed_AfterSuccessfulHandling_FullInboxMode()
	{
		// Arrange
		var middleware = CreateMiddleware(omitDeduplicator: true);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		context.SetItem<object>("MessageId", "success-msg-305");

		_ = A.CallTo(() => _inboxStore.IsProcessedAsync(
			"success-msg-305", A<string>._, A<CancellationToken>._))
			.Returns(new ValueTask<bool>(false));

		_ = A.CallTo(() => _inboxStore.GetEntryAsync(
			"success-msg-305", A<string>._, A<CancellationToken>._))
			.Returns(new ValueTask<InboxEntry?>((InboxEntry?)null));

		var createdEntry = new InboxEntry { MessageId = "success-msg-305" };
		_ = A.CallTo(() => _inboxStore.CreateEntryAsync(
			"success-msg-305",
			A<string>._,
			A<string>._,
			A<byte[]>._,
			A<IDictionary<string, object>>._,
			A<CancellationToken>._))
			.Returns(new ValueTask<InboxEntry>(createdEntry));

		// Act
		var result = await middleware.InvokeAsync(
			message, context, SuccessDelegate(), CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		_ = A.CallTo(() => _inboxStore.MarkProcessedAsync(
			"success-msg-305", A<string>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MarkAsFailed_WhenHandlerReturnsFailure_FullInboxMode()
	{
		// Arrange
		var middleware = CreateMiddleware(omitDeduplicator: true);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		context.SetItem<object>("MessageId", "fail-msg-306");

		_ = A.CallTo(() => _inboxStore.IsProcessedAsync(
			"fail-msg-306", A<string>._, A<CancellationToken>._))
			.Returns(new ValueTask<bool>(false));

		_ = A.CallTo(() => _inboxStore.GetEntryAsync(
			"fail-msg-306", A<string>._, A<CancellationToken>._))
			.Returns(new ValueTask<InboxEntry?>((InboxEntry?)null));

		var createdEntry = new InboxEntry { MessageId = "fail-msg-306" };
		_ = A.CallTo(() => _inboxStore.CreateEntryAsync(
			"fail-msg-306",
			A<string>._,
			A<string>._,
			A<byte[]>._,
			A<IDictionary<string, object>>._,
			A<CancellationToken>._))
			.Returns(new ValueTask<InboxEntry>(createdEntry));

		// Act
		var result = await middleware.InvokeAsync(
			message, context, FailureDelegate("Processing failed"), CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeFalse();
		_ = A.CallTo(() => _inboxStore.MarkFailedAsync(
			"fail-msg-306", A<string>._, A<string>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MarkAsFailed_WhenHandlerThrowsException_FullInboxMode()
	{
		// Arrange
		var middleware = CreateMiddleware(omitDeduplicator: true);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		context.SetItem<object>("MessageId", "throw-msg-307");

		_ = A.CallTo(() => _inboxStore.IsProcessedAsync(
			"throw-msg-307", A<string>._, A<CancellationToken>._))
			.Returns(new ValueTask<bool>(false));

		_ = A.CallTo(() => _inboxStore.GetEntryAsync(
			"throw-msg-307", A<string>._, A<CancellationToken>._))
			.Returns(new ValueTask<InboxEntry?>((InboxEntry?)null));

		var createdEntry = new InboxEntry { MessageId = "throw-msg-307" };
		_ = A.CallTo(() => _inboxStore.CreateEntryAsync(
			"throw-msg-307",
			A<string>._,
			A<string>._,
			A<byte[]>._,
			A<IDictionary<string, object>>._,
			A<CancellationToken>._))
			.Returns(new ValueTask<InboxEntry>(createdEntry));

		ValueTask<IMessageResult> ThrowingDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> throw new InvalidOperationException("Handler exploded");

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(
			middleware.InvokeAsync(message, context, ThrowingDelegate, CancellationToken.None).AsTask());

		_ = A.CallTo(() => _inboxStore.MarkFailedAsync(
			"throw-msg-307", A<string>._, A<string>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Priority Resolution Tests (Full Inbox over Light Mode)

	[Fact]
	public async Task PreferFullInboxMode_WhenBothStoreAndDeduplicatorAvailable()
	{
		// Arrange - both store and deduplicator provided, should use full inbox mode
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		context.SetItem<object>("MessageId", "priority-msg-401");

		_ = A.CallTo(() => _inboxStore.IsProcessedAsync(
			"priority-msg-401", A<string>._, A<CancellationToken>._))
			.Returns(new ValueTask<bool>(true));

		var wasCalled = new[] { false };

		// Act
		var result = await middleware.InvokeAsync(
			message, context, TrackingSuccessDelegate(wasCalled), CancellationToken.None);

		// Assert - full inbox mode was used (IsProcessedAsync was called)
		result.Succeeded.ShouldBeTrue();
		_ = A.CallTo(() => _inboxStore.IsProcessedAsync(
			"priority-msg-401", A<string>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();

		// Deduplicator should NOT have been called
		A.CallTo(() => _deduplicator.IsDuplicateAsync(
			A<string>._, A<TimeSpan>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	#endregion
}
