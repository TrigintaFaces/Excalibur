// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery.Pipeline;

using FakeItEasy;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Pipeline;

/// <summary>
/// Unit tests for zero-allocation middleware implementations.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Pipeline")]
[Trait("Priority", "0")]
public sealed class ZeroAllocationMiddlewareShould
{
	#region ZeroAllocationContextMiddleware Tests

	[Fact]
	public void ZeroAllocationContextMiddleware_Stage_ReturnsPreProcessing()
	{
		// Arrange
		var middleware = new ZeroAllocationContextMiddleware();

		// Act & Assert
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
	}

	[Fact]
	public void ZeroAllocationContextMiddleware_ImplementsIDispatchMiddleware()
	{
		// Arrange
		var middleware = new ZeroAllocationContextMiddleware();

		// Assert
		_ = middleware.ShouldBeAssignableTo<IDispatchMiddleware>();
	}

	[Fact]
	public void ZeroAllocationContextMiddleware_ImplementsIZeroAllocationMiddleware()
	{
		// Arrange
		var middleware = new ZeroAllocationContextMiddleware();

		// Assert
		_ = middleware.ShouldBeAssignableTo<IZeroAllocationMiddleware>();
	}

	[Fact]
	public void ZeroAllocationContextMiddleware_IDispatchMiddleware_Stage_ReturnsCorrectValue()
	{
		// Arrange
		IDispatchMiddleware middleware = new ZeroAllocationContextMiddleware();

		// Act & Assert
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
	}

	[Fact]
	public async Task ZeroAllocationContextMiddleware_InvokeAsync_WithValidMessage_CallsNextDelegate()
	{
		// Arrange
		IDispatchMiddleware middleware = new ZeroAllocationContextMiddleware();
		var message = A.Fake<IDispatchMessage>();
		var context = new Dispatch.Messaging.MessageContext { MessageId = "test-id" };
		var nextCalled = false;

		DispatchRequestDelegate nextDelegate = (msg, ctx, ct) =>
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(A.Fake<IMessageResult>());
		};

		// Act
		_ = await middleware.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task ZeroAllocationContextMiddleware_InvokeAsync_WithEmptyMessageId_GeneratesNewMessageIdAndContinues()
	{
		// Arrange
		// Note: When context.MessageId is empty, MessageMetadata.FromContext generates a new GUID,
		// so the middleware's "Message ID is required" check passes because the envelope gets a valid ID
		IDispatchMiddleware middleware = new ZeroAllocationContextMiddleware();
		var message = A.Fake<IDispatchMessage>();
		var context = new Dispatch.Messaging.MessageContext { MessageId = string.Empty };
		var nextCalled = false;

		DispatchRequestDelegate nextDelegate = (msg, ctx, ct) =>
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(A.Fake<IMessageResult>());
		};

		// Act
		_ = await middleware.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert - Next is called because MessageMetadata.FromContext generates a new GUID when empty
		nextCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task ZeroAllocationContextMiddleware_InvokeAsync_WithNullMessageId_GeneratesNewIdAndContinues()
	{
		// Arrange
		// Note: MessageMetadata.FromContext generates a new GUID when MessageId is null or unparseable
		IDispatchMiddleware middleware = new ZeroAllocationContextMiddleware();
		var message = A.Fake<IDispatchMessage>();
		var context = new Dispatch.Messaging.MessageContext { MessageId = null };
		var nextCalled = false;

		DispatchRequestDelegate nextDelegate = (msg, ctx, ct) =>
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(A.Fake<IMessageResult>());
		};

		// Act
		_ = await middleware.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert - Next is called because MessageMetadata.FromContext generates a new GUID
		nextCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task ZeroAllocationContextMiddleware_InvokeAsync_ReturnsSuccessfulResult()
	{
		// Arrange
		IDispatchMiddleware middleware = new ZeroAllocationContextMiddleware();
		var message = A.Fake<IDispatchMessage>();
		var context = new Dispatch.Messaging.MessageContext { MessageId = "test-id" };
		var expectedResult = A.Fake<IMessageResult>();
		_ = A.CallTo(() => expectedResult.Succeeded).Returns(true);

		DispatchRequestDelegate nextDelegate = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(expectedResult);

		// Act
		var result = await middleware.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task ZeroAllocationContextMiddleware_InvokeAsync_SetsContextMessageIdFromEnvelope()
	{
		// Arrange
		// Note: The middleware sets context.MessageId from envelope.Metadata.MessageId,
		// which is generated by MessageMetadata.FromContext. When context.MessageId is
		// not a valid GUID, a new GUID is generated.
		IDispatchMiddleware middleware = new ZeroAllocationContextMiddleware();
		var message = A.Fake<IDispatchMessage>();
		var context = new Dispatch.Messaging.MessageContext { MessageId = "not-a-valid-guid" };

		DispatchRequestDelegate nextDelegate = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		// Act
		_ = await middleware.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert - The context's message ID is set to the envelope's generated GUID
		Guid.TryParse(context.MessageId, out _).ShouldBeTrue("MessageId should be a valid GUID");
	}

	#endregion

	#region ZeroAllocationValidationMiddleware Tests

	[Fact]
	public void ZeroAllocationValidationMiddleware_Stage_ReturnsValidation()
	{
		// Arrange
		var middleware = new ZeroAllocationValidationMiddleware();

		// Act & Assert
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.Validation);
	}

	[Fact]
	public void ZeroAllocationValidationMiddleware_ImplementsIDispatchMiddleware()
	{
		// Arrange
		var middleware = new ZeroAllocationValidationMiddleware();

		// Assert
		_ = middleware.ShouldBeAssignableTo<IDispatchMiddleware>();
	}

	[Fact]
	public void ZeroAllocationValidationMiddleware_ImplementsIZeroAllocationMiddleware()
	{
		// Arrange
		var middleware = new ZeroAllocationValidationMiddleware();

		// Assert
		_ = middleware.ShouldBeAssignableTo<IZeroAllocationMiddleware>();
	}

	[Fact]
	public void ZeroAllocationValidationMiddleware_IDispatchMiddleware_Stage_ReturnsCorrectValue()
	{
		// Arrange
		IDispatchMiddleware middleware = new ZeroAllocationValidationMiddleware();

		// Act & Assert
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.Validation);
	}

	[Fact]
	public async Task ZeroAllocationValidationMiddleware_InvokeAsync_WithValidMessage_CallsNextDelegate()
	{
		// Arrange
		IDispatchMiddleware middleware = new ZeroAllocationValidationMiddleware();
		var message = A.Fake<IDispatchMessage>();
		var context = new Dispatch.Messaging.MessageContext { MessageId = "test-id" };
		var nextCalled = false;

		DispatchRequestDelegate nextDelegate = (msg, ctx, ct) =>
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(A.Fake<IMessageResult>());
		};

		// Act
		_ = await middleware.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task ZeroAllocationValidationMiddleware_InvokeAsync_WithNullMessage_ThrowsArgumentNullException()
	{
		// Arrange
		IDispatchMiddleware middleware = new ZeroAllocationValidationMiddleware();
		var context = new Dispatch.Messaging.MessageContext { MessageId = "test-id" };

		DispatchRequestDelegate nextDelegate = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		// Act & Assert
		// Note: The null check happens during envelope creation, not in the middleware logic
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			middleware.InvokeAsync(null!, context, nextDelegate, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ZeroAllocationValidationMiddleware_InvokeAsync_WithValidMessage_ReturnsSuccessResult()
	{
		// Arrange
		IDispatchMiddleware middleware = new ZeroAllocationValidationMiddleware();
		var message = A.Fake<IDispatchMessage>();
		var context = new Dispatch.Messaging.MessageContext { MessageId = "test-id" };
		var expectedResult = A.Fake<IMessageResult>();
		_ = A.CallTo(() => expectedResult.Succeeded).Returns(true);

		DispatchRequestDelegate nextDelegate = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(expectedResult);

		// Act
		var result = await middleware.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	#endregion

	#region MiddlewareResult Tests

	[Fact]
	public void MiddlewareResult_Continue_ReturnsContinueExecution()
	{
		// Act
		var result = MiddlewareResult.Continue();

		// Assert
		result.ContinueExecution.ShouldBeTrue();
		result.Success.ShouldBeTrue();
		result.Error.ShouldBeNull();
	}

	[Fact]
	public void MiddlewareResult_StopWithError_ReturnsStopExecution()
	{
		// Act
		var result = MiddlewareResult.StopWithError("Test error");

		// Assert
		result.ContinueExecution.ShouldBeFalse();
		result.Success.ShouldBeFalse();
		result.Error.ShouldBe("Test error");
	}

	[Fact]
	public void MiddlewareResult_StopWithSuccess_ReturnsStopWithSuccess()
	{
		// Act
		var result = MiddlewareResult.StopWithSuccess();

		// Assert
		result.ContinueExecution.ShouldBeFalse();
		result.Success.ShouldBeTrue();
		result.Error.ShouldBeNull();
	}

	[Fact]
	public void MiddlewareResult_Continue_HasNullErrorMessage()
	{
		// Act
		var result = MiddlewareResult.Continue();

		// Assert
		result.Error.ShouldBeNull();
	}

	[Fact]
	public void MiddlewareResult_StopWithError_HasErrorMessage()
	{
		// Arrange
		const string errorMessage = "Custom error message";

		// Act
		var result = MiddlewareResult.StopWithError(errorMessage);

		// Assert
		result.Error.ShouldBe(errorMessage);
	}

	#endregion

	#region MiddlewareContext Tests

	[Fact]
	public void MiddlewareContext_Constructor_InitializesWithMiddlewareArray()
	{
		// Arrange
		var middlewares = new IDispatchMiddleware[] { A.Fake<IDispatchMiddleware>() };

		// Act
		var context = new MiddlewareContext(middlewares);

		// Assert - MiddlewareContext is a struct, so we verify by checking its state
		context.CurrentIndex.ShouldBe(-1);
	}

	[Fact]
	public void MiddlewareContext_Constructor_WithEmptyArray_HasNoNext()
	{
		// Arrange & Act
		var context = new MiddlewareContext([]);

		// Assert
		context.HasNext.ShouldBeFalse();
	}

	[Fact]
	public void MiddlewareContext_MoveNext_ReturnsFirstMiddleware()
	{
		// Arrange
		var middleware1 = A.Fake<IDispatchMiddleware>();
		var middlewares = new[] { middleware1 };
		var context = new MiddlewareContext(middlewares);

		// Act
		var result = context.MoveNext();

		// Assert
		result.ShouldBe(middleware1);
	}

	[Fact]
	public void MiddlewareContext_Reset_RestoresInitialState()
	{
		// Arrange
		var middleware1 = A.Fake<IDispatchMiddleware>();
		var middlewares = new[] { middleware1 };
		var context = new MiddlewareContext(middlewares);
		_ = context.MoveNext();

		// Act
		context.Reset();

		// Assert
		context.CurrentIndex.ShouldBe(-1);
		context.HasNext.ShouldBeTrue();
	}

	#endregion

	#region Edge Cases

	[Fact]
	public async Task ZeroAllocationContextMiddleware_InvokeAsync_WithCancellation_CompletesNormally()
	{
		// Arrange
		IDispatchMiddleware middleware = new ZeroAllocationContextMiddleware();
		var message = A.Fake<IDispatchMessage>();
		var context = new Dispatch.Messaging.MessageContext { MessageId = "test-id" };
		using var cts = new CancellationTokenSource();

		DispatchRequestDelegate nextDelegate = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		// Act - Note: Current implementation doesn't check cancellation token
		var result = await middleware.InvokeAsync(message, context, nextDelegate, cts.Token);

		// Assert - Should complete without throwing
		_ = result.ShouldNotBeNull();
	}

	[Fact]
	public async Task ZeroAllocationValidationMiddleware_InvokeAsync_WithCancellation_CompletesNormally()
	{
		// Arrange
		IDispatchMiddleware middleware = new ZeroAllocationValidationMiddleware();
		var message = A.Fake<IDispatchMessage>();
		var context = new Dispatch.Messaging.MessageContext { MessageId = "test-id" };
		using var cts = new CancellationTokenSource();

		DispatchRequestDelegate nextDelegate = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		// Act
		var result = await middleware.InvokeAsync(message, context, nextDelegate, cts.Token);

		// Assert
		_ = result.ShouldNotBeNull();
	}

	#endregion
}
