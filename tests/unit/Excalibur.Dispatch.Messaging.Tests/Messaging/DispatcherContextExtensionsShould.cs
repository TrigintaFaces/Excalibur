// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;

using MessageResult = Excalibur.Dispatch.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// Unit tests for DispatcherContextExtensions, including Sprint 70 context-less dispatch functionality
/// and Sprint 455 convenience API improvements.
/// </summary>
/// <remarks>
/// Sprint 455 - S455.5: Unit tests for convenience APIs (S455.4).
/// Tests context-less dispatch, ambient context reuse, and IMessageContextFactory integration.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
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

		// Act — call extension method explicitly to avoid interface method shadowing
		_ = await DispatcherContextExtensions.DispatchAsync(_dispatcher, message, CancellationToken.None);

		// Assert
		_ = capturedContext.ShouldNotBeNull();
		_ = capturedContext.ShouldBeOfType<MessageContext>();
	}

	/// <summary>
	/// Verifies that a context-free DispatchAsync issued under an ambient context auto-childs:
	/// a distinct context whose CausationId links to the parent, with correlation propagated.
	/// </summary>
	[Fact]
	public async Task DispatchAsync_Should_AutoChild_Under_Ambient_Context()
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

		// Act — call extension method explicitly to avoid interface method shadowing
		_ = await DispatcherContextExtensions.DispatchAsync(_dispatcher, message, CancellationToken.None);

		// Assert — a child, NOT the ambient instance: causation linked to the parent, correlation propagated.
		_ = capturedContext.ShouldNotBeNull();
		capturedContext.ShouldNotBeSameAs(ambientContext);
		capturedContext.CausationId.ShouldBe(ambientContext.MessageId);
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
		var result = await _dispatcher.DispatchAsync<IDispatchAction<string>, string>(message, CancellationToken.None);

		// Assert
		_ = capturedContext.ShouldNotBeNull();
		_ = capturedContext.ShouldBeOfType<MessageContext>();
		result.ReturnValue.ShouldBe("test-result");
	}

	/// <summary>
	/// Verifies that a context-free DispatchAsync-with-response issued under an ambient context auto-childs:
	/// a distinct context whose CausationId links to the parent, with correlation propagated.
	/// </summary>
	[Fact]
	public async Task DispatchAsync_With_Response_Should_AutoChild_Under_Ambient_Context()
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
		var result = await _dispatcher.DispatchAsync<IDispatchAction<int>, int>(message, CancellationToken.None);

		// Assert — a child, NOT the ambient instance: causation linked to the parent, correlation propagated.
		_ = capturedContext.ShouldNotBeNull();
		capturedContext.ShouldNotBeSameAs(ambientContext);
		capturedContext.CausationId.ShouldBe(ambientContext.MessageId);
		capturedContext.CorrelationId.ShouldBe("ambient-456");
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

		// Act — call extension method explicitly to avoid interface method shadowing
		_ = await DispatcherContextExtensions.DispatchAsync(_dispatcher, message, CancellationToken.None);

		// Assert
		capturedContext.CorrelationId.ShouldBe(existingCorrelationId);
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

		// Act & Assert — call extension method explicitly to test its null guard
		var exception = await Should.ThrowAsync<ArgumentNullException>(
			async () => await DispatcherContextExtensions.DispatchAsync(nullDispatcher!, message, CancellationToken.None));

		exception.ParamName.ShouldBe("dispatcher");
	}

	#endregion

	#region Context-free auto-child dispatch tests

	/// <summary>
	/// Verifies that a context-free DispatchAsync auto-childs from the ambient context.
	/// </summary>
	[Fact]
	public async Task DispatchAsync_ContextFree_AutoChilds_From_Ambient()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var parentContext = new MessageContext
		{
			MessageId = "parent-message-id",
			CorrelationId = "correlation-123",
		};
		parentContext.GetOrCreateIdentityFeature().TenantId = "tenant-abc";
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
		_ = await DispatcherContextExtensions.DispatchAsync(_dispatcher, message, CancellationToken.None);

		// Assert - Child context should be different but with propagated identifiers
		_ = capturedContext.ShouldNotBeNull();
		capturedContext.ShouldNotBe(parentContext);
		capturedContext.CorrelationId.ShouldBe(parentContext.CorrelationId);
		capturedContext.GetTenantId().ShouldBe(parentContext.GetTenantId());
		capturedContext.CausationId.ShouldBe(parentContext.MessageId);
		capturedContext.MessageId.ShouldNotBe(parentContext.MessageId);
	}

	/// <summary>
	/// Verifies that a context-free DispatchAsync starts a fresh root (does NOT throw) when no ambient context exists.
	/// </summary>
	[Fact]
	public async Task DispatchAsync_ContextFree_CreatesFreshRoot_When_No_Ambient_Context()
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

		MessageContextHolder.Current = null;

		// Act & Assert — no ambient context starts a fresh root; must NOT throw.
		await Should.NotThrowAsync(
			() => DispatcherContextExtensions.DispatchAsync(_dispatcher, message, CancellationToken.None));

		_ = capturedContext.ShouldNotBeNull();
	}

	/// <summary>
	/// Verifies that a context-free DispatchAsync-with-response starts a fresh root (does NOT throw) when no ambient context.
	/// </summary>
	[Fact]
	public async Task DispatchAsync_ContextFree_WithResponse_CreatesFreshRoot_When_No_Ambient_Context()
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
			.Returns(MessageResult.Success("root-result"));

		MessageContextHolder.Current = null;

		// Act — no ambient context starts a fresh root; a direct call must NOT throw (an unexpected
		// throw fails the test), unlike the retired DispatchChildAsync which threw here.
		var result = await _dispatcher.DispatchAsync<IDispatchAction<string>, string>(message, CancellationToken.None);

		// Assert
		_ = capturedContext.ShouldNotBeNull();
		result.Succeeded.ShouldBeTrue();
	}

	/// <summary>
	/// Verifies that a context-free DispatchAsync-with-response auto-childs from the ambient context.
	/// </summary>
	[Fact]
	public async Task DispatchAsync_ContextFree_WithResponse_AutoChilds()
	{
		// Arrange
		var message = A.Fake<IDispatchAction<string>>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var parentContext = new MessageContext
		{
			MessageId = "parent-id",
			CorrelationId = "correlation-xyz",
		};
		parentContext.GetOrCreateIdentityFeature().UserId = "user-123";
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
		var result = await _dispatcher.DispatchAsync<IDispatchAction<string>, string>(message, CancellationToken.None);

		// Assert
		_ = capturedContext.ShouldNotBeNull();
		capturedContext.CorrelationId.ShouldBe(parentContext.CorrelationId);
		capturedContext.GetUserId().ShouldBe(parentContext.GetUserId());
		capturedContext.CausationId.ShouldBe(parentContext.MessageId);
		result.ReturnValue.ShouldBe("child-result");
	}

	/// <summary>
	/// Verifies that a context-free auto-child DispatchAsync preserves parent identifiers and creates the expected causation chain.
	/// </summary>
	[Fact]
	public async Task DispatchAsync_ContextFree_AutoChild_Preserves_Parent_And_Sets_Causation_Chain()
	{
		// Arrange.
		var message = A.Fake<IDispatchMessage>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var parentContext = new MessageContext
		{
			MessageId = "parent-id",
			CorrelationId = "corr-parent",
			CausationId = "upstream-cause",
		};
		parentContext.GetOrCreateIdentityFeature().WorkflowId = "workflow-123";
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
		_ = await DispatcherContextExtensions.DispatchAsync(_dispatcher, message, CancellationToken.None);

		// Assert.
		_ = capturedContext.ShouldNotBeNull();
		capturedContext.ShouldNotBe(parentContext);
		capturedContext.CorrelationId.ShouldBe("corr-parent");
		capturedContext.CausationId.ShouldBe("parent-id");
		capturedContext.GetWorkflowId().ShouldBe("workflow-123");

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

		// Act — call extension method explicitly to avoid interface method shadowing
		_ = await DispatcherContextExtensions.DispatchAsync(dispatcher, message, CancellationToken.None);

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

		// Act — call extension method explicitly to avoid interface method shadowing
		_ = await DispatcherContextExtensions.DispatchAsync(dispatcher, message, CancellationToken.None);

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
		var result = await dispatcher.DispatchAsync<IDispatchAction<int>, int>(message, CancellationToken.None);

		// Assert - context was created via factory with ServiceProvider injected
		_ = capturedContext.ShouldNotBeNull();
		_ = capturedContext.RequestServices.ShouldNotBeNull(); // Factory injects a service provider
		result.ReturnValue.ShouldBe(42);
	}

	/// <summary>
	/// Verifies that the context-free DispatchAsync throws ArgumentNullException when dispatcher is null.
	/// </summary>
	[Fact]
	public async Task DispatchAsync_ContextFree_Should_Throw_When_Dispatcher_Is_Null()
	{
		// Arrange
		IDispatcher? nullDispatcher = null;
		var message = A.Fake<IDispatchMessage>();
		MessageContextHolder.Current = new MessageContext();

		// Act & Assert
		var exception = await Should.ThrowAsync<ArgumentNullException>(
			async () => await DispatcherContextExtensions.DispatchAsync(nullDispatcher!, message, CancellationToken.None));

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

		// Act — call extension method explicitly to avoid interface method shadowing
		_ = await DispatcherContextExtensions.DispatchAsync(_dispatcher, message, token);

		// Assert
		capturedToken.ShouldBe(token);
	}

	#endregion

	#region TResponse Inference Convenience Overloads (Layer 1 - Reflection Fallback)

	/// <summary>
	/// Verifies that the TResponse-inferring DispatchAsync convenience overload delegates to the
	/// fully-typed DispatchAsync&lt;TMessage, TResponse&gt; via the cached delegate.
	/// </summary>
	[Fact]
	public async Task DispatchAsync_InferredTResponse_Should_Dispatch_Correctly()
	{
		// Arrange
		var message = new TestCreateOrderCommand { OrderName = "Test" };
		var expectedResult = MessageResult.Success(Guid.NewGuid());

		_ = A.CallTo(() => _dispatcher.DispatchAsync<TestCreateOrderCommand, Guid>(
				A<TestCreateOrderCommand>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(expectedResult);

		// Configure dispatcher to return null ServiceProvider so fallback path is used
		_ = A.CallTo(() => _dispatcher.ServiceProvider).Returns(null);
		MessageContextHolder.Current = new MessageContext();

		// Act — call the convenience overload explicitly to bypass overload resolution ambiguity
		// (In production, source-generated overloads win; in tests we call the fallback directly)
IDispatchAction<Guid> action = message;
		var result = await DispatcherContextExtensions.DispatchAsync(_dispatcher, action, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	/// <summary>
	/// Verifies that the TResponse-inferring DispatchAsync with explicit context delegates correctly.
	/// </summary>
	[Fact]
	public async Task DispatchAsync_InferredTResponse_WithContext_Should_Dispatch_Correctly()
	{
		// Arrange
		var message = new TestCreateOrderCommand { OrderName = "Test" };
		var context = new MessageContext { CorrelationId = "test-correlation" };
		var expectedResult = MessageResult.Success(Guid.NewGuid());

		_ = A.CallTo(() => _dispatcher.DispatchAsync<TestCreateOrderCommand, Guid>(
				A<TestCreateOrderCommand>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(expectedResult);

		// Act
IDispatchAction<Guid> action = message;
		var result = await DispatcherContextExtensions.DispatchAsync(_dispatcher, action, context, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	/// <summary>
	/// Verifies that the TResponse-inferring context-free DispatchAsync auto-childs correctly under an ambient parent.
	/// </summary>
	[Fact]
	public async Task DispatchAsync_ContextFree_InferredTResponse_AutoChilds_Correctly()
	{
		// Arrange
		var message = new TestCreateOrderCommand { OrderName = "Child" };
		var serviceProvider = A.Fake<IServiceProvider>();
		var parentContext = new MessageContext
		{
			MessageId = "parent-id",
			CorrelationId = "correlation-child",
		};
		parentContext.Initialize(serviceProvider);
		var expectedResult = MessageResult.Success(Guid.NewGuid());

		_ = A.CallTo(() => _dispatcher.DispatchAsync<TestCreateOrderCommand, Guid>(
				A<TestCreateOrderCommand>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(expectedResult);

		MessageContextHolder.Current = parentContext;

		// Act
IDispatchAction<Guid> action = message;
		var result = await DispatcherContextExtensions.DispatchAsync(_dispatcher, action, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	/// <summary>
	/// Verifies that DispatchAsync with inferred TResponse throws when dispatcher is null.
	/// </summary>
	[Fact]
	public async Task DispatchAsync_InferredTResponse_Should_Throw_When_Dispatcher_Is_Null()
	{
		IDispatcher? nullDispatcher = null;
		IDispatchAction<Guid> message = new TestCreateOrderCommand();

var exception = await Should.ThrowAsync<ArgumentNullException>(
			async () => await DispatcherContextExtensions.DispatchAsync(nullDispatcher!, message, CancellationToken.None));

		exception.ParamName.ShouldBe("dispatcher");
	}

	/// <summary>
	/// Verifies that DispatchAsync with context and inferred TResponse throws when context is null.
	/// </summary>
	[Fact]
	public async Task DispatchAsync_InferredTResponse_WithContext_Should_Throw_When_Context_Is_Null()
	{
		IDispatchAction<Guid> message = new TestCreateOrderCommand();

var exception = await Should.ThrowAsync<ArgumentNullException>(
			async () => await DispatcherContextExtensions.DispatchAsync(
				_dispatcher, message, (IMessageContext)null!, CancellationToken.None));

		exception.ParamName.ShouldBe("context");
	}

	/// <summary>
	/// Verifies that the cached delegate approach returns the correct response type.
	/// </summary>
	[Fact]
	public async Task DispatchAsync_InferredTResponse_Should_Return_Correct_Response_Type()
	{
		// Arrange — use int response to verify different type
		var message = new TestGetCountQuery();
		var expectedResult = MessageResult.Success(42);

		_ = A.CallTo(() => _dispatcher.DispatchAsync<TestGetCountQuery, int>(
				A<TestGetCountQuery>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(expectedResult);

		_ = A.CallTo(() => _dispatcher.ServiceProvider).Returns(null);
		MessageContextHolder.Current = new MessageContext();

		// Act
IDispatchAction<int> action = message;
		var result = await DispatcherContextExtensions.DispatchAsync(_dispatcher, action, CancellationToken.None);

		// Assert
		result.ReturnValue.ShouldBe(42);
	}

	/// <summary>
	/// Verifies that calling the convenience overload multiple times uses the cached delegate
	/// (no exceptions on second call).
	/// </summary>
	[Fact]
	public async Task DispatchAsync_InferredTResponse_Should_Cache_Delegate_Across_Calls()
	{
		// Arrange
		var message1 = new TestCreateOrderCommand { OrderName = "First" };
		var message2 = new TestCreateOrderCommand { OrderName = "Second" };

		_ = A.CallTo(() => _dispatcher.DispatchAsync<TestCreateOrderCommand, Guid>(
				A<TestCreateOrderCommand>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(MessageResult.Success(Guid.NewGuid()));

		_ = A.CallTo(() => _dispatcher.ServiceProvider).Returns(null);
		MessageContextHolder.Current = new MessageContext();

		// Act — two successive calls should both succeed (delegate cached on first)
IDispatchAction<Guid> action1 = message1;
		IDispatchAction<Guid> action2 = message2;
		var result1 = await DispatcherContextExtensions.DispatchAsync(_dispatcher, action1, CancellationToken.None);
		var result2 = await DispatcherContextExtensions.DispatchAsync(_dispatcher, action2, CancellationToken.None);

		// Assert
		result1.Succeeded.ShouldBeTrue();
		result2.Succeeded.ShouldBeTrue();
	}

	#endregion

	private sealed record TestCreateOrderCommand : IDispatchAction<Guid>
	{
		public string? OrderName { get; init; }
	}

	private sealed record TestGetCountQuery : IDispatchAction<int>;
}