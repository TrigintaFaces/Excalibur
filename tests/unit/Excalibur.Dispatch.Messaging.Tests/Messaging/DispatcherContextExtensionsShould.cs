// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.DependencyInjection;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// Unit tests for DispatcherContextExtensions, including Sprint 70 context-less dispatch functionality
/// and Sprint 455 convenience API improvements.
/// </summary>
/// <remarks>
/// Sprint 455 - S455.5: Unit tests for convenience APIs (S455.4).
/// Tests context-less dispatch, ambient context reuse, and IMessageContextFactory integration.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Priority", "0")]
public sealed class DispatcherContextExtensionsShould : IDisposable
{
	private readonly IDispatcher _dispatcher = A.Fake<IDispatcher>();

	public DispatcherContextExtensionsShould()
	{
		// Ensure clean ambient context before each test
		MessageContextHolder.Current = null;
	}

	public void Dispose()
	{
		// Clean up ambient context after each test
		MessageContextHolder.Current = null;
	}

	#region Sprint 70 - Context-less Dispatch Tests (Task gtuc)

	/// <summary>
	/// Verifies that DispatchAsync creates a new context when MessageContextHolder.Current is null.
	/// </summary>
	[Fact]
	public async Task DispatchAsync_Should_Create_New_Context_When_No_Ambient_Context()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		IMessageContext? capturedContext = null;

		// Configure dispatcher to return null ServiceProvider so fallback path is used
		_ = A.CallTo(() => _dispatcher.ServiceProvider).Returns(null);

		_ = A.CallTo(() => _dispatcher.DispatchAsync(
				message,
				A<IMessageContext>._,
				A<CancellationToken>._))
			.Invokes((IDispatchMessage _, IMessageContext ctx, CancellationToken _) => capturedContext = ctx)
			.Returns(MessageResult.Success());

		// Ensure no ambient context
		MessageContextHolder.Current = null;

		// Act
		_ = await _dispatcher.DispatchAsync(message, CancellationToken.None).ConfigureAwait(true);

		// Assert
		_ = capturedContext.ShouldNotBeNull();
		_ = capturedContext.ShouldBeOfType<MessageContext>();
	}

	/// <summary>
	/// Verifies that DispatchAsync reuses the current ambient context when one exists.
	/// </summary>
	[Fact]
	public async Task DispatchAsync_Should_Reuse_Ambient_Context_When_Available()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var ambientContext = new MessageContext { CorrelationId = "ambient-correlation-123" };
		IMessageContext? capturedContext = null;

		_ = A.CallTo(() => _dispatcher.DispatchAsync(
				message,
				A<IMessageContext>._,
				A<CancellationToken>._))
			.Invokes((IDispatchMessage _, IMessageContext ctx, CancellationToken _) => capturedContext = ctx)
			.Returns(MessageResult.Success());

		// Set ambient context
		MessageContextHolder.Current = ambientContext;

		// Act
		_ = await _dispatcher.DispatchAsync(message, CancellationToken.None).ConfigureAwait(true);

		// Assert
		capturedContext.ShouldBe(ambientContext);
		capturedContext.CorrelationId.ShouldBe("ambient-correlation-123");
	}

	/// <summary>
	/// Verifies that DispatchAsync with response works similarly - creates new context when no ambient.
	/// </summary>
	[Fact]
	public async Task DispatchAsync_With_Response_Should_Create_New_Context_When_No_Ambient()
	{
		// Arrange
		var message = A.Fake<IDispatchAction<string>>();
		IMessageContext? capturedContext = null;

		// Configure dispatcher to return null ServiceProvider so fallback path is used
		_ = A.CallTo(() => _dispatcher.ServiceProvider).Returns(null);

		_ = A.CallTo(() => _dispatcher.DispatchAsync<IDispatchAction<string>, string>(
				message,
				A<IMessageContext>._,
				A<CancellationToken>._))
			.Invokes((IDispatchAction<string> _, IMessageContext ctx, CancellationToken _) => capturedContext = ctx)
			.Returns(MessageResult.Success("test-result"));

		// Ensure no ambient context
		MessageContextHolder.Current = null;

		// Act
		var result = await _dispatcher.DispatchAsync<IDispatchAction<string>, string>(message, CancellationToken.None).ConfigureAwait(true);

		// Assert
		_ = capturedContext.ShouldNotBeNull();
		_ = capturedContext.ShouldBeOfType<MessageContext>();
		result.ReturnValue.ShouldBe("test-result");
	}

	/// <summary>
	/// Verifies that DispatchAsync with response reuses ambient context when available.
	/// </summary>
	[Fact]
	public async Task DispatchAsync_With_Response_Should_Reuse_Ambient_Context()
	{
		// Arrange
		var message = A.Fake<IDispatchAction<int>>();
		var ambientContext = new MessageContext { CorrelationId = "ambient-456" };
		IMessageContext? capturedContext = null;

		_ = A.CallTo(() => _dispatcher.DispatchAsync<IDispatchAction<int>, int>(
				message,
				A<IMessageContext>._,
				A<CancellationToken>._))
			.Invokes((IDispatchAction<int> _, IMessageContext ctx, CancellationToken _) => capturedContext = ctx)
			.Returns(MessageResult.Success(42));

		// Set ambient context
		MessageContextHolder.Current = ambientContext;

		// Act
		var result = await _dispatcher.DispatchAsync<IDispatchAction<int>, int>(message, CancellationToken.None).ConfigureAwait(true);

		// Assert
		capturedContext.ShouldBe(ambientContext);
		result.ReturnValue.ShouldBe(42);
	}

	/// <summary>
	/// Verifies that reused ambient context keeps its existing CorrelationId unchanged.
	/// </summary>
	[Fact]
	public async Task DispatchAsync_Should_Keep_Existing_CorrelationId_On_Reused_Context()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var existingCorrelationId = "existing-correlation-id-789";
		var ambientContext = new MessageContext { CorrelationId = existingCorrelationId };
		IMessageContext? capturedContext = null;

		_ = A.CallTo(() => _dispatcher.DispatchAsync(
				message,
				A<IMessageContext>._,
				A<CancellationToken>._))
			.Invokes((IDispatchMessage _, IMessageContext ctx, CancellationToken _) => capturedContext = ctx)
			.Returns(MessageResult.Success());

		// Set ambient context
		MessageContextHolder.Current = ambientContext;

		// Act
		_ = await _dispatcher.DispatchAsync(message, CancellationToken.None).ConfigureAwait(true);

		// Assert
		capturedContext.CorrelationId.ShouldBe(existingCorrelationId);
	}

	[Fact]
	public async Task DispatchAsync_Should_Use_UltraLocal_Path_When_Dispatcher_Supports_It()
	{
		// Arrange
		var dispatcher = new DirectLocalTestDispatcher();
		var message = new LocalActionMessage();
		MessageContextHolder.Current = null;

		// Act
		var result = await dispatcher.DispatchAsync(message, CancellationToken.None).ConfigureAwait(true);

		// Assert
		result.Succeeded.ShouldBeTrue();
		dispatcher.LocalActionCalls.ShouldBe(1);
		dispatcher.ContextDispatchCalls.ShouldBe(0);
	}

	[Fact]
	public async Task DispatchAsync_With_Response_Should_Use_UltraLocal_Path_When_Dispatcher_Supports_It()
	{
		// Arrange
		var dispatcher = new DirectLocalTestDispatcher();
		var message = new LocalQueryMessage { Value = 21 };
		MessageContextHolder.Current = null;

		// Act
		var result = await dispatcher.DispatchAsync<LocalQueryMessage, int>(message, CancellationToken.None).ConfigureAwait(true);

		// Assert
		result.Succeeded.ShouldBeTrue();
		result.ReturnValue.ShouldBe(42);
		dispatcher.LocalQueryCalls.ShouldBe(1);
		dispatcher.ContextDispatchCalls.ShouldBe(0);
	}

	/// <summary>
	/// Verifies that DispatchAsync throws ArgumentNullException when dispatcher is null.
	/// </summary>
	[Fact]
	public async Task DispatchAsync_Should_Throw_When_Dispatcher_Is_Null()
	{
		// Arrange
		IDispatcher? nullDispatcher = null;
		var message = A.Fake<IDispatchMessage>();

		// Act & Assert
		var exception = await Should.ThrowAsync<ArgumentNullException>(
			async () => await nullDispatcher.DispatchAsync(message, CancellationToken.None).ConfigureAwait(true));

		exception.ParamName.ShouldBe("dispatcher");
	}

	#endregion

	#region Sprint 70 - DispatchChildAsync Tests

	/// <summary>
	/// Verifies that DispatchChildAsync creates a child context from the ambient context.
	/// </summary>
	[Fact]
	public async Task DispatchChildAsync_Should_Create_Child_Context_From_Ambient()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var parentContext = new MessageContext
		{
			MessageId = "parent-message-id",
			CorrelationId = "correlation-123",
			TenantId = "tenant-abc",
		};
		parentContext.Initialize(serviceProvider);

		IMessageContext? capturedContext = null;

		_ = A.CallTo(() => _dispatcher.DispatchAsync(
				message,
				A<IMessageContext>._,
				A<CancellationToken>._))
			.Invokes((IDispatchMessage _, IMessageContext ctx, CancellationToken _) => capturedContext = ctx)
			.Returns(MessageResult.Success());

		// Set ambient context
		MessageContextHolder.Current = parentContext;

		// Act
		_ = await _dispatcher.DispatchChildAsync(message, CancellationToken.None).ConfigureAwait(true);

		// Assert - Child context should be different but with propagated identifiers
		_ = capturedContext.ShouldNotBeNull();
		capturedContext.ShouldNotBe(parentContext);
		capturedContext.CorrelationId.ShouldBe(parentContext.CorrelationId);
		capturedContext.TenantId.ShouldBe(parentContext.TenantId);
		capturedContext.CausationId.ShouldBe(parentContext.MessageId);
		capturedContext.MessageId.ShouldNotBe(parentContext.MessageId);
	}

	/// <summary>
	/// Verifies that DispatchChildAsync throws InvalidOperationException when no ambient context exists.
	/// </summary>
	[Fact]
	public async Task DispatchChildAsync_Should_Throw_When_No_Ambient_Context()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		MessageContextHolder.Current = null;

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			async () => await _dispatcher.DispatchChildAsync(message, CancellationToken.None).ConfigureAwait(true));

		exception.Message.ShouldContain("Cannot dispatch child message without an active context");
	}

	/// <summary>
	/// Verifies that DispatchChildAsync with response throws when no ambient context.
	/// </summary>
	[Fact]
	public async Task DispatchChildAsync_With_Response_Should_Throw_When_No_Ambient_Context()
	{
		// Arrange
		var message = A.Fake<IDispatchAction<string>>();
		MessageContextHolder.Current = null;

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			async () => await _dispatcher.DispatchChildAsync<IDispatchAction<string>, string>(message, CancellationToken.None).ConfigureAwait(true));

		exception.Message.ShouldContain("Cannot dispatch child action without an active context");
	}

	/// <summary>
	/// Verifies that DispatchChildAsync with response creates proper child context.
	/// </summary>
	[Fact]
	public async Task DispatchChildAsync_With_Response_Should_Create_Child_Context()
	{
		// Arrange
		var message = A.Fake<IDispatchAction<string>>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var parentContext = new MessageContext
		{
			MessageId = "parent-id",
			CorrelationId = "correlation-xyz",
			UserId = "user-123",
		};
		parentContext.Initialize(serviceProvider);

		IMessageContext? capturedContext = null;

		_ = A.CallTo(() => _dispatcher.DispatchAsync<IDispatchAction<string>, string>(
				message,
				A<IMessageContext>._,
				A<CancellationToken>._))
			.Invokes((IDispatchAction<string> _, IMessageContext ctx, CancellationToken _) => capturedContext = ctx)
			.Returns(MessageResult.Success("child-result"));

		// Set ambient context
		MessageContextHolder.Current = parentContext;

		// Act
		var result = await _dispatcher.DispatchChildAsync<IDispatchAction<string>, string>(message, CancellationToken.None).ConfigureAwait(true);

		// Assert
		_ = capturedContext.ShouldNotBeNull();
		capturedContext.CorrelationId.ShouldBe(parentContext.CorrelationId);
		capturedContext.UserId.ShouldBe(parentContext.UserId);
		capturedContext.CausationId.ShouldBe(parentContext.MessageId);
		result.ReturnValue.ShouldBe("child-result");
	}

	/// <summary>
	/// Verifies that DispatchChildAsync preserves parent identifiers and creates the expected causation chain.
	/// </summary>
	[Fact]
	public async Task DispatchChildAsync_Should_Preserve_Parent_And_Set_Expected_Causation_Chain()
	{
		// Arrange.
		var message = A.Fake<IDispatchMessage>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var parentContext = new MessageContext
		{
			MessageId = "parent-id",
			CorrelationId = "corr-parent",
			CausationId = "upstream-cause",
			WorkflowId = "workflow-123",
		};
		parentContext.Initialize(serviceProvider);

		IMessageContext? capturedContext = null;
		_ = A.CallTo(() => _dispatcher.DispatchAsync(
				message,
				A<IMessageContext>._,
				A<CancellationToken>._))
			.Invokes((IDispatchMessage _, IMessageContext ctx, CancellationToken _) => capturedContext = ctx)
			.Returns(MessageResult.Success());

		MessageContextHolder.Current = parentContext;

		// Act.
		_ = await _dispatcher.DispatchChildAsync(message, CancellationToken.None).ConfigureAwait(true);

		// Assert.
		_ = capturedContext.ShouldNotBeNull();
		capturedContext.ShouldNotBe(parentContext);
		capturedContext.CorrelationId.ShouldBe("corr-parent");
		capturedContext.CausationId.ShouldBe("parent-id");
		capturedContext.WorkflowId.ShouldBe("workflow-123");

		// Parent context must remain unchanged.
		parentContext.CausationId.ShouldBe("upstream-cause");
		parentContext.MessageId.ShouldBe("parent-id");
		parentContext.CorrelationId.ShouldBe("corr-parent");
	}

	#endregion

	#region Sprint 455 - IMessageContextFactory Integration Tests (S455.4)

	/// <summary>
	/// Verifies that DispatchAsync uses IMessageContextFactory when available via ServiceProvider.
	/// </summary>
	[Fact]
	public async Task DispatchAsync_Should_Use_MessageContextFactory_When_Available()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddDispatchPipeline();
		var serviceProvider = services.BuildServiceProvider();

		var dispatcher = A.Fake<IDispatcher>();
		_ = A.CallTo(() => dispatcher.ServiceProvider).Returns(serviceProvider);

		var message = A.Fake<IDispatchMessage>();
		IMessageContext? capturedContext = null;

		_ = A.CallTo(() => dispatcher.DispatchAsync(
				message,
				A<IMessageContext>._,
				A<CancellationToken>._))
			.Invokes((IDispatchMessage _, IMessageContext ctx, CancellationToken _) => capturedContext = ctx)
			.Returns(MessageResult.Success());

		// Ensure no ambient context so factory path is used
		MessageContextHolder.Current = null;

		// Act
		_ = await dispatcher.DispatchAsync(message, CancellationToken.None).ConfigureAwait(true);

		// Assert - context was created via factory with ServiceProvider injected
		_ = capturedContext.ShouldNotBeNull();
		_ = capturedContext.ShouldBeAssignableTo<MessageContext>();
		_ = capturedContext.RequestServices.ShouldNotBeNull(); // Factory injects a service provider
	}

	/// <summary>
	/// Verifies that DispatchAsync falls back to new MessageContext when no factory available.
	/// </summary>
	[Fact]
	public async Task DispatchAsync_Should_Fallback_To_New_MessageContext_When_No_Factory()
	{
		// Arrange
		var dispatcher = A.Fake<IDispatcher>();
		_ = A.CallTo(() => dispatcher.ServiceProvider).Returns(null);

		var message = A.Fake<IDispatchMessage>();
		IMessageContext? capturedContext = null;

		_ = A.CallTo(() => dispatcher.DispatchAsync(
				message,
				A<IMessageContext>._,
				A<CancellationToken>._))
			.Invokes((IDispatchMessage _, IMessageContext ctx, CancellationToken _) => capturedContext = ctx)
			.Returns(MessageResult.Success());

		// Ensure no ambient context
		MessageContextHolder.Current = null;

		// Act
		_ = await dispatcher.DispatchAsync(message, CancellationToken.None).ConfigureAwait(true);

		// Assert - context was created without factory (fallback to new MessageContext)
		_ = capturedContext.ShouldNotBeNull();
		_ = capturedContext.ShouldBeOfType<MessageContext>();
	}

	/// <summary>
	/// Verifies that DispatchAsync with response uses IMessageContextFactory when available.
	/// </summary>
	[Fact]
	public async Task DispatchAsync_With_Response_Should_Use_MessageContextFactory_When_Available()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddDispatchPipeline();
		var serviceProvider = services.BuildServiceProvider();

		var dispatcher = A.Fake<IDispatcher>();
		_ = A.CallTo(() => dispatcher.ServiceProvider).Returns(serviceProvider);

		var message = A.Fake<IDispatchAction<int>>();
		IMessageContext? capturedContext = null;

		_ = A.CallTo(() => dispatcher.DispatchAsync<IDispatchAction<int>, int>(
				message,
				A<IMessageContext>._,
				A<CancellationToken>._))
			.Invokes((IDispatchAction<int> _, IMessageContext ctx, CancellationToken _) => capturedContext = ctx)
			.Returns(MessageResult.Success(42));

		// Ensure no ambient context
		MessageContextHolder.Current = null;

		// Act
		var result = await dispatcher.DispatchAsync<IDispatchAction<int>, int>(message, CancellationToken.None).ConfigureAwait(true);

		// Assert - context was created via factory with ServiceProvider injected
		_ = capturedContext.ShouldNotBeNull();
		_ = capturedContext.RequestServices.ShouldNotBeNull(); // Factory injects a service provider
		result.ReturnValue.ShouldBe(42);
	}

	/// <summary>
	/// Verifies that DispatchChildAsync throws ArgumentNullException when dispatcher is null.
	/// </summary>
	[Fact]
	public async Task DispatchChildAsync_Should_Throw_When_Dispatcher_Is_Null()
	{
		// Arrange
		IDispatcher? nullDispatcher = null;
		var message = A.Fake<IDispatchMessage>();
		MessageContextHolder.Current = new MessageContext();

		// Act & Assert
		var exception = await Should.ThrowAsync<ArgumentNullException>(
			async () => await nullDispatcher.DispatchChildAsync(message, CancellationToken.None).ConfigureAwait(true));

		exception.ParamName.ShouldBe("dispatcher");
	}

	/// <summary>
	/// Verifies that DispatchChildAsync with response throws ArgumentNullException when dispatcher is null.
	/// </summary>
	[Fact]
	public async Task DispatchChildAsync_With_Response_Should_Throw_When_Dispatcher_Is_Null()
	{
		// Arrange
		IDispatcher? nullDispatcher = null;
		var message = A.Fake<IDispatchAction<string>>();
		MessageContextHolder.Current = new MessageContext();

		// Act & Assert
		var exception = await Should.ThrowAsync<ArgumentNullException>(
			async () => await nullDispatcher.DispatchChildAsync<IDispatchAction<string>, string>(message, CancellationToken.None).ConfigureAwait(true));

		exception.ParamName.ShouldBe("dispatcher");
	}

	/// <summary>
	/// Verifies that CancellationToken is properly passed through DispatchAsync.
	/// </summary>
	[Fact]
	public async Task DispatchAsync_Should_Pass_CancellationToken()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		using var cts = new CancellationTokenSource();
		var token = cts.Token;
		CancellationToken capturedToken = default;

		// Configure dispatcher to return null ServiceProvider so fallback path is used
		_ = A.CallTo(() => _dispatcher.ServiceProvider).Returns(null);

		_ = A.CallTo(() => _dispatcher.DispatchAsync(
				message,
				A<IMessageContext>._,
				A<CancellationToken>._))
			.Invokes((IDispatchMessage _, IMessageContext _, CancellationToken ct) => capturedToken = ct)
			.Returns(MessageResult.Success());

		MessageContextHolder.Current = null;

		// Act
		_ = await _dispatcher.DispatchAsync(message, token).ConfigureAwait(true);

		// Assert
		capturedToken.ShouldBe(token);
	}

	#endregion

	private sealed record LocalActionMessage : IDispatchAction;

	private sealed record LocalQueryMessage : IDispatchAction<int>
	{
		public int Value { get; init; }
	}

	private sealed class DirectLocalTestDispatcher : IDispatcher, IDirectLocalDispatcher
	{
		public int LocalActionCalls { get; private set; }
		public int LocalQueryCalls { get; private set; }
		public int ContextDispatchCalls { get; private set; }

		public IServiceProvider? ServiceProvider => null;

		public Task<IMessageResult> DispatchAsync<TMessage>(
			TMessage message,
			IMessageContext context,
			CancellationToken cancellationToken)
			where TMessage : IDispatchMessage
		{
			ContextDispatchCalls++;
			return Task.FromResult<IMessageResult>(MessageResult.Success());
		}

		public Task<IMessageResult<TResponse>> DispatchAsync<TMessage, TResponse>(
			TMessage message,
			IMessageContext context,
			CancellationToken cancellationToken)
			where TMessage : IDispatchAction<TResponse>
		{
			ContextDispatchCalls++;
			return Task.FromResult<IMessageResult<TResponse>>(MessageResult.Success(default(TResponse)!));
		}

		public ValueTask DispatchLocalAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
			where TMessage : IDispatchAction
		{
			LocalActionCalls++;
			return ValueTask.CompletedTask;
		}

		public ValueTask<TResponse?> DispatchLocalAsync<TMessage, TResponse>(TMessage message, CancellationToken cancellationToken)
			where TMessage : IDispatchAction<TResponse>
		{
			LocalQueryCalls++;
			object? value = message is LocalQueryMessage query ? query.Value * 2 : default(TResponse);
			return new ValueTask<TResponse?>((TResponse?)value);
		}

		public IAsyncEnumerable<TOutput> DispatchStreamingAsync<TDocument, TOutput>(
			TDocument document,
			IMessageContext context,
			CancellationToken cancellationToken)
			where TDocument : IDispatchDocument
			=> throw new NotSupportedException();

		public Task DispatchStreamAsync<TDocument>(
			IAsyncEnumerable<TDocument> documents,
			IMessageContext context,
			CancellationToken cancellationToken)
			where TDocument : IDispatchDocument
			=> throw new NotSupportedException();

		public IAsyncEnumerable<TOutput> DispatchTransformStreamAsync<TInput, TOutput>(
			IAsyncEnumerable<TInput> input,
			IMessageContext context,
			CancellationToken cancellationToken)
			where TInput : IDispatchDocument
			=> throw new NotSupportedException();

		public Task DispatchWithProgressAsync<TDocument>(
			TDocument document,
			IMessageContext context,
			IProgress<DocumentProgress> progress,
			CancellationToken cancellationToken)
			where TDocument : IDispatchDocument
			=> throw new NotSupportedException();
	}
}
