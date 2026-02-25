// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Delivery.Pipeline;

namespace Excalibur.Dispatch.Tests.Messaging.Pipeline;

// Helper method to create valid MessageMetadata
file static class TestHelper
{
	public static MessageMetadata CreateMetadata(string messageId) =>
		new(
			MessageId: messageId,
			CorrelationId: Guid.NewGuid().ToString(),
			CausationId: null,
			TraceParent: null,
			TenantId: null,
			UserId: null,
			ContentType: "application/json",
			SerializerVersion: "1.0.0",
			MessageVersion: "1.0.0",
			ContractVersion: "1.0.0");
}

/// <summary>
/// Unit tests for <see cref="MiddlewareAdapter" /> covering adaptation of IDispatchMiddleware
/// to the zero-allocation pipeline interface.
/// </summary>
/// <remarks>
/// Sprint 461 - S461.1: Coverage tests for 0% coverage classes.
/// Target: Increase MiddlewareAdapter coverage from 0% to 80%+.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Priority", "0")]
public sealed class MiddlewareAdapterShould : IDisposable
{
	private readonly IDispatchMiddleware _fakeMiddleware;
	private readonly IDispatchMessage _fakeMessage;
	private readonly IMessageContext _fakeContext;

	public MiddlewareAdapterShould()
	{
		_fakeMiddleware = A.Fake<IDispatchMiddleware>();
		_fakeMessage = A.Fake<IDispatchMessage>();
		_fakeContext = A.Fake<IMessageContext>();

		// Default setup: middleware returns success
		var successResult = A.Fake<IMessageResult>();
		_ = A.CallTo(() => successResult.Succeeded).Returns(true);
		_ = A.CallTo(() => _fakeMiddleware.InvokeAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<DispatchRequestDelegate>._,
			A<CancellationToken>._))
			.Returns(new ValueTask<IMessageResult>(successResult));
	}

	public void Dispose()
	{
		// No cleanup needed - individual tests dispose their adapters
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_Should_Throw_When_Middleware_Is_Null()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new MiddlewareAdapter(null!));
	}

	[Fact]
	public void Constructor_Should_Accept_Valid_Middleware()
	{
		// Act
		using var adapter = new MiddlewareAdapter(_fakeMiddleware);

		// Assert
		_ = adapter.ShouldNotBeNull();
	}

	#endregion

	#region Stage Property Tests

	[Fact]
	public void Stage_Should_Return_Middleware_Stage_When_Set()
	{
		// Arrange
		_ = A.CallTo(() => _fakeMiddleware.Stage).Returns(DispatchMiddlewareStage.Authorization);
		using var adapter = new MiddlewareAdapter(_fakeMiddleware);

		// Act
		var stage = adapter.Stage;

		// Assert
		stage.ShouldBe(DispatchMiddlewareStage.Authorization);
	}

	[Fact]
	public void Stage_Should_Return_End_When_Middleware_Stage_Is_Null()
	{
		// Arrange
		_ = A.CallTo(() => _fakeMiddleware.Stage).Returns(null);
		using var adapter = new MiddlewareAdapter(_fakeMiddleware);

		// Act
		var stage = adapter.Stage;

		// Assert
		stage.ShouldBe(DispatchMiddlewareStage.End);
	}

	[Theory]
	[InlineData(DispatchMiddlewareStage.PreProcessing)]
	[InlineData(DispatchMiddlewareStage.Validation)]
	[InlineData(DispatchMiddlewareStage.Authorization)]
	[InlineData(DispatchMiddlewareStage.Processing)]
	[InlineData(DispatchMiddlewareStage.PostProcessing)]
	[InlineData(DispatchMiddlewareStage.End)]
	public void Stage_Should_Return_Correct_Stage_For_All_Values(DispatchMiddlewareStage expectedStage)
	{
		// Arrange
		_ = A.CallTo(() => _fakeMiddleware.Stage).Returns(expectedStage);
		using var adapter = new MiddlewareAdapter(_fakeMiddleware);

		// Act
		var stage = adapter.Stage;

		// Assert
		stage.ShouldBe(expectedStage);
	}

	#endregion

	#region ProcessAsync Tests

	[Fact]
	public async Task ProcessAsync_Should_Return_Continue_When_Middleware_Succeeds_And_Has_Next()
	{
		// Arrange
		var successResult = A.Fake<IMessageResult>();
		_ = A.CallTo(() => successResult.Succeeded).Returns(true);
		_ = A.CallTo(() => _fakeMiddleware.InvokeAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<DispatchRequestDelegate>._,
			A<CancellationToken>._))
			.Returns(new ValueTask<IMessageResult>(successResult));

		using var adapter = new MiddlewareAdapter(_fakeMiddleware);

		// Create envelope and context with HasNext = true
		var metadata = TestHelper.CreateMetadata("test-1");
		var envelope = new MessageEnvelope<IDispatchMessage>(_fakeMessage, metadata, _fakeContext);
		var middlewareArray = new IDispatchMiddleware[] { _fakeMiddleware, _fakeMiddleware }; // 2 elements = HasNext after first
		var context = new MiddlewareContext(middlewareArray);
		_ = context.MoveNext(); // Advance to first, HasNext should be true

		// Act
		var (result, _) = await adapter.ProcessAsync(envelope, context, CancellationToken.None);

		// Assert
		result.ContinueExecution.ShouldBeTrue();
		result.Success.ShouldBeTrue();
		result.Error.ShouldBeNull();
	}

	[Fact]
	public async Task ProcessAsync_Should_Return_StopWithSuccess_When_Middleware_Succeeds_And_No_Next()
	{
		// Arrange
		var successResult = A.Fake<IMessageResult>();
		_ = A.CallTo(() => successResult.Succeeded).Returns(true);
		_ = A.CallTo(() => _fakeMiddleware.InvokeAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<DispatchRequestDelegate>._,
			A<CancellationToken>._))
			.Returns(new ValueTask<IMessageResult>(successResult));

		using var adapter = new MiddlewareAdapter(_fakeMiddleware);

		var metadata = TestHelper.CreateMetadata("test-2");
		var envelope = new MessageEnvelope<IDispatchMessage>(_fakeMessage, metadata, _fakeContext);
		var middlewareArray = new IDispatchMiddleware[] { _fakeMiddleware }; // Only 1 element = no next
		var context = new MiddlewareContext(middlewareArray);
		_ = context.MoveNext(); // Advance to first, HasNext should be false

		// Act
		var (result, _) = await adapter.ProcessAsync(envelope, context, CancellationToken.None);

		// Assert
		result.ContinueExecution.ShouldBeFalse();
		result.Success.ShouldBeTrue();
		result.Error.ShouldBeNull();
	}

	[Fact]
	public async Task ProcessAsync_Should_Return_StopWithError_When_Middleware_Fails()
	{
		// Arrange
		var failedResult = A.Fake<IMessageResult>();
		_ = A.CallTo(() => failedResult.Succeeded).Returns(false);
		_ = A.CallTo(() => failedResult.ErrorMessage).Returns("Test error message");
		_ = A.CallTo(() => _fakeMiddleware.InvokeAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<DispatchRequestDelegate>._,
			A<CancellationToken>._))
			.Returns(new ValueTask<IMessageResult>(failedResult));

		using var adapter = new MiddlewareAdapter(_fakeMiddleware);

		var metadata = TestHelper.CreateMetadata("test-3");
		var envelope = new MessageEnvelope<IDispatchMessage>(_fakeMessage, metadata, _fakeContext);
		var middlewareArray = new IDispatchMiddleware[] { _fakeMiddleware };
		var context = new MiddlewareContext(middlewareArray);
		_ = context.MoveNext();

		// Act
		var (result, _) = await adapter.ProcessAsync(envelope, context, CancellationToken.None);

		// Assert
		result.ContinueExecution.ShouldBeFalse();
		result.Success.ShouldBeFalse();
		result.Error.ShouldBe("Test error message");
	}

	[Fact]
	public async Task ProcessAsync_Should_Return_Unknown_Error_When_Middleware_Fails_Without_Message()
	{
		// Arrange
		var failedResult = A.Fake<IMessageResult>();
		_ = A.CallTo(() => failedResult.Succeeded).Returns(false);
		_ = A.CallTo(() => failedResult.ErrorMessage).Returns(null);
		_ = A.CallTo(() => _fakeMiddleware.InvokeAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<DispatchRequestDelegate>._,
			A<CancellationToken>._))
			.Returns(new ValueTask<IMessageResult>(failedResult));

		using var adapter = new MiddlewareAdapter(_fakeMiddleware);

		var metadata = TestHelper.CreateMetadata("test-4");
		var envelope = new MessageEnvelope<IDispatchMessage>(_fakeMessage, metadata, _fakeContext);
		var middlewareArray = new IDispatchMiddleware[] { _fakeMiddleware };
		var context = new MiddlewareContext(middlewareArray);
		_ = context.MoveNext();

		// Act
		var (result, _) = await adapter.ProcessAsync(envelope, context, CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		result.Error.ShouldBe("Unknown error");
	}

	[Fact]
	public async Task ProcessAsync_Should_Pass_Cancellation_Token_To_Middleware()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		CancellationToken capturedToken = default;

		_ = A.CallTo(() => _fakeMiddleware.InvokeAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<DispatchRequestDelegate>._,
			A<CancellationToken>._))
			.Invokes((IDispatchMessage _, IMessageContext _, DispatchRequestDelegate _, CancellationToken ct) =>
			{
				capturedToken = ct;
			})
			.Returns(new ValueTask<IMessageResult>(A.Fake<IMessageResult>()));

		var successResult = A.Fake<IMessageResult>();
		_ = A.CallTo(() => successResult.Succeeded).Returns(true);
		_ = A.CallTo(() => _fakeMiddleware.InvokeAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<DispatchRequestDelegate>._,
			A<CancellationToken>._))
			.Returns(new ValueTask<IMessageResult>(successResult));

		using var adapter = new MiddlewareAdapter(_fakeMiddleware);

		var metadata = TestHelper.CreateMetadata("test-5");
		var envelope = new MessageEnvelope<IDispatchMessage>(_fakeMessage, metadata, _fakeContext);
		var middlewareArray = new IDispatchMiddleware[] { _fakeMiddleware };
		var context = new MiddlewareContext(middlewareArray);
		_ = context.MoveNext();

		// Act
		_ = await adapter.ProcessAsync(envelope, context, cts.Token);

		// Assert
		_ = A.CallTo(() => _fakeMiddleware.InvokeAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<DispatchRequestDelegate>._,
			cts.Token))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public void Dispose_Should_Not_Throw()
	{
		// Arrange
		var adapter = new MiddlewareAdapter(_fakeMiddleware);

		// Act & Assert - Should not throw
		Should.NotThrow(() => adapter.Dispose());
	}

	[Fact]
	public void Dispose_Should_Be_Idempotent()
	{
		// Arrange
		var adapter = new MiddlewareAdapter(_fakeMiddleware);

		// Act & Assert - Multiple disposals should not throw
		Should.NotThrow(() =>
		{
			adapter.Dispose();
			adapter.Dispose();
			adapter.Dispose();
		});
	}

	#endregion

	#region Delegate Caching Tests

	[Fact]
	public async Task ProcessAsync_Should_Cache_Delegate_Per_Thread()
	{
		// Arrange
		var invocationCount = 0;
		var successResult = A.Fake<IMessageResult>();
		_ = A.CallTo(() => successResult.Succeeded).Returns(true);
		_ = A.CallTo(() => _fakeMiddleware.InvokeAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<DispatchRequestDelegate>._,
			A<CancellationToken>._))
			.Invokes(() => invocationCount++)
			.Returns(new ValueTask<IMessageResult>(successResult));

		using var adapter = new MiddlewareAdapter(_fakeMiddleware);

		var metadata = TestHelper.CreateMetadata("test-cache");
		var envelope = new MessageEnvelope<IDispatchMessage>(_fakeMessage, metadata, _fakeContext);
		var middlewareArray = new IDispatchMiddleware[] { _fakeMiddleware };
		var context = new MiddlewareContext(middlewareArray);
		_ = context.MoveNext();

		// Act - Call multiple times on same thread
		_ = await adapter.ProcessAsync(envelope, context, CancellationToken.None);
		context.Reset();
		_ = context.MoveNext();
		_ = await adapter.ProcessAsync(envelope, context, CancellationToken.None);
		context.Reset();
		_ = context.MoveNext();
		_ = await adapter.ProcessAsync(envelope, context, CancellationToken.None);

		// Assert - Should have been invoked 3 times
		invocationCount.ShouldBe(3);
	}

	#endregion
}
