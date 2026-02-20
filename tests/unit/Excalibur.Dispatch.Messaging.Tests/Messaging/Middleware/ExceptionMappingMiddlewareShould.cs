// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0 - see LICENSE files for details.

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Tests.TestFakes;

using Microsoft.Extensions.Logging.Abstractions;

using DispatchMiddlewareStage = Excalibur.Dispatch.Abstractions.DispatchMiddlewareStage;
using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

#pragma warning disable CA2201 // Do not raise reserved exception types - test code intentionally uses base Exception

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for the <see cref="ExceptionMappingMiddleware"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ExceptionMappingMiddlewareShould
{
	private readonly IExceptionMapper _mapper;
	private readonly ILogger<ExceptionMappingMiddleware> _logger;

	public ExceptionMappingMiddlewareShould()
	{
		_mapper = A.Fake<IExceptionMapper>();
		_logger = NullLoggerFactory.Instance.CreateLogger<ExceptionMappingMiddleware>();
	}

	private ExceptionMappingMiddleware CreateMiddleware(IExceptionMapper? mapper = null)
	{
		return new ExceptionMappingMiddleware(mapper ?? _mapper, _logger);
	}

	[Fact]
	public void HaveCorrectStage()
	{
		// Arrange
		var middleware = CreateMiddleware();

		// Assert
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.ErrorHandling);
	}

	[Fact]
	public void ThrowArgumentNullException_WhenMapperIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new ExceptionMappingMiddleware(null!, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new ExceptionMappingMiddleware(_mapper, null!));
	}

	[Fact]
	public async Task ReturnSuccessResult_WhenNoExceptionIsThrown()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };
		var expectedResult = MessageResult.Success();

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new ValueTask<IMessageResult>(expectedResult);

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task MapException_WhenExceptionIsThrown()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };
		var expectedException = new InvalidOperationException("Test exception");
		var expectedProblemDetails = new MessageProblemDetails
		{
			Type = "test:error",
			Title = "Test Error",
			ErrorCode = 400,
			Status = 400,
			Detail = "Test exception",
		};

		_ = A.CallTo(() => _mapper.MapAsync(expectedException, A<CancellationToken>._))
			.Returns(Task.FromResult<IMessageProblemDetails>(expectedProblemDetails));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> throw expectedException;

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Type.ShouldBe("test:error");
	}

	[Fact]
	public async Task RethrowOperationCanceledException_WhenCancellationIsRequested()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };
		var cts = new CancellationTokenSource();
		cts.Cancel();

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(
			middleware.InvokeAsync(message, context, NextDelegate, cts.Token).AsTask());
	}

	[Fact]
	public async Task RethrowOperationCanceledException_WhenHandlerThrowsCancellation()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> throw new OperationCanceledException("Handler cancelled");

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(
			middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenMessageIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var context = new FakeMessageContext();

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
		var context = new FakeMessageContext();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(message, context, null!, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ReturnFallbackError_WhenMappingFails()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };
		var originalException = new InvalidOperationException("Original error");
		var mappingException = new Exception("Mapping failed");

		_ = A.CallTo(() => _mapper.MapAsync(originalException, A<CancellationToken>._))
			.ThrowsAsync(mappingException);

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> throw originalException;

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Type.ShouldBe("urn:dispatch:error:mapping-failed");
		result.ProblemDetails.ErrorCode.ShouldBe(500);
	}

	[Fact]
	public async Task IntegrateWithRealExceptionMapper()
	{
		// Arrange
		var builder = new ExceptionMappingBuilder();
		_ = builder.Map<ApiException>(ex => ex.ToProblemDetails());
		var realMapper = new ExceptionMapper(builder.Build());
		var middleware = CreateMiddleware(realMapper);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };
		var exception = new ApiException(404, "Not found", null);

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> throw exception;

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.ErrorCode.ShouldBe(404);
		result.ProblemDetails.Detail.ShouldBe("Not found");
	}

	[Fact]
	public async Task IntegrateWithDefaultExceptionMapping()
	{
		// Arrange
		var builder = new ExceptionMappingBuilder();
		_ = builder.UseApiExceptionMapping();
		var realMapper = new ExceptionMapper(builder.Build());
		var middleware = CreateMiddleware(realMapper);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };
		var exception = new Exception("Unknown error");

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> throw exception;

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.ErrorCode.ShouldBe(500); // Default fallback
	}

	[Fact]
	public async Task PassCancellationToken_ToMapper()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };
		var expectedException = new InvalidOperationException("Test");
		var cts = new CancellationTokenSource();
		CancellationToken receivedToken = default;

		_ = A.CallTo(() => _mapper.MapAsync(expectedException, A<CancellationToken>._))
			.Invokes((Exception _, CancellationToken ct) => receivedToken = ct)
			.Returns(Task.FromResult<IMessageProblemDetails>(new MessageProblemDetails()));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> throw expectedException;

		// Act
		_ = await middleware.InvokeAsync(message, context, NextDelegate, cts.Token);

		// Assert
		receivedToken.ShouldBe(cts.Token);
	}

	[Fact]
	public async Task UseDefaultErrorCode_WhenProblemDetailsHasZeroErrorCode()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };
		var expectedException = new InvalidOperationException("Test");
		var problemDetailsWithZeroCode = new MessageProblemDetails
		{
			Type = "test:error",
			Title = "Test Error",
			ErrorCode = 0, // Zero error code
			Status = 0,
			Detail = "Test exception",
		};

		_ = A.CallTo(() => _mapper.MapAsync(expectedException, A<CancellationToken>._))
			.Returns(Task.FromResult<IMessageProblemDetails>(problemDetailsWithZeroCode));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> throw expectedException;

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert - The middleware should log with 500 as default when ErrorCode is 0
		result.IsSuccess.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
	}

	[Fact]
	public async Task HandleNullMessageId_WhenMappingFails()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = null }; // Null message ID
		var originalException = new InvalidOperationException("Original error");
		var mappingException = new Exception("Mapping failed");

		_ = A.CallTo(() => _mapper.MapAsync(originalException, A<CancellationToken>._))
			.ThrowsAsync(mappingException);

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> throw originalException;

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Type.ShouldBe("urn:dispatch:error:mapping-failed");
	}

	[Fact]
	public async Task HandleNullProblemDetailsType()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };
		var expectedException = new InvalidOperationException("Test");
		var problemDetailsWithNullType = new MessageProblemDetails
		{
			Type = null!, // Null type
			Title = "Test Error",
			ErrorCode = 400,
			Status = 400,
			Detail = "Test exception",
		};

		_ = A.CallTo(() => _mapper.MapAsync(expectedException, A<CancellationToken>._))
			.Returns(Task.FromResult<IMessageProblemDetails>(problemDetailsWithNullType));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> throw expectedException;

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert - Should handle null type gracefully (logs "unknown")
		result.IsSuccess.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails!.ErrorCode.ShouldBe(400);
	}

	[Fact]
	public async Task HandleDifferentExceptionTypes()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };
		var aggregateException = new AggregateException("Multiple errors", new InvalidOperationException("Inner"));
		var problemDetails = new MessageProblemDetails
		{
			Type = "test:aggregate",
			Title = "Aggregate Error",
			ErrorCode = 500,
			Status = 500,
		};

		_ = A.CallTo(() => _mapper.MapAsync(aggregateException, A<CancellationToken>._))
			.Returns(Task.FromResult<IMessageProblemDetails>(problemDetails));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> throw aggregateException;

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.ProblemDetails.Type.ShouldBe("test:aggregate");
	}

	[Fact]
	public async Task RethrowTaskCanceledException()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> throw new TaskCanceledException("Task cancelled");

		// Act & Assert - TaskCanceledException derives from OperationCanceledException
		_ = await Should.ThrowAsync<TaskCanceledException>(
			middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task PreserveExceptionInformation_WhenMapping()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-123" };
		Exception? capturedExceptionInMapper = null;
		var expectedException = new InvalidOperationException("Original error with details");
		var problemDetails = new MessageProblemDetails
		{
			Type = "test:error",
			Title = "Error",
			ErrorCode = 400,
			Status = 400,
			Detail = "Mapped error",
		};

		_ = A.CallTo(() => _mapper.MapAsync(A<Exception>._, A<CancellationToken>._))
			.Invokes((Exception ex, CancellationToken _) => capturedExceptionInMapper = ex)
			.Returns(Task.FromResult<IMessageProblemDetails>(problemDetails));

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> throw expectedException;

		// Act
		_ = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert - Verify the same exception instance was passed to mapper
		capturedExceptionInMapper.ShouldBe(expectedException);
	}
}
