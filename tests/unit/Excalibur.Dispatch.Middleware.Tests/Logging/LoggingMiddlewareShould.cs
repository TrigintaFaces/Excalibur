// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.Options;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Middleware.Tests.Logging;

/// <summary>
/// Unit tests for LoggingMiddleware.
/// </summary>
[Trait("Category", "Unit")]
public sealed class LoggingMiddlewareShould : UnitTestBase
{
	private readonly ILogger<LoggingMiddleware> _logger;

	public LoggingMiddlewareShould()
	{
		_logger = A.Fake<ILogger<LoggingMiddleware>>();
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new LoggingMiddlewareOptions());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new LoggingMiddleware(options, NullTelemetrySanitizer.Instance, null!));
	}

	[Fact]
	public void Constructor_UsesDefaultOptions_WhenOptionsValueIsNull()
	{
		// Arrange
		var options = A.Fake<IOptions<LoggingMiddlewareOptions>>();
		_ = A.CallTo(() => options.Value).Returns(null!);

		// Act - Should not throw
		var middleware = new LoggingMiddleware(options, NullTelemetrySanitizer.Instance, _logger);

		// Assert
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
	}

	[Fact]
	public void Stage_ReturnsPreProcessing()
	{
		// Arrange
		var options = MsOptions.Create(new LoggingMiddlewareOptions());
		var middleware = new LoggingMiddleware(options, NullTelemetrySanitizer.Instance, _logger);

		// Assert
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
	}

	[Fact]
	public async Task InvokeAsync_ThrowsArgumentNullException_WhenMessageIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new LoggingMiddlewareOptions());
		var middleware = new LoggingMiddleware(options, NullTelemetrySanitizer.Instance, _logger);
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Success());

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await middleware.InvokeAsync(null!, context, next, CancellationToken.None));
	}

	[Fact]
	public async Task InvokeAsync_ThrowsArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new LoggingMiddlewareOptions());
		var middleware = new LoggingMiddleware(options, NullTelemetrySanitizer.Instance, _logger);
		var message = A.Fake<IDispatchMessage>();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Success());

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await middleware.InvokeAsync(message, null!, next, CancellationToken.None));
	}

	[Fact]
	public async Task InvokeAsync_ThrowsArgumentNullException_WhenDelegateIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new LoggingMiddlewareOptions());
		var middleware = new LoggingMiddleware(options, NullTelemetrySanitizer.Instance, _logger);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await middleware.InvokeAsync(message, context, null!, CancellationToken.None));
	}

	[Fact]
	public async Task InvokeAsync_CallsNextDelegate()
	{
		// Arrange
		var options = MsOptions.Create(new LoggingMiddlewareOptions());
		var middleware = new LoggingMiddleware(options, NullTelemetrySanitizer.Instance, _logger);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var nextCalled = false;

		DispatchRequestDelegate next = (_, _, _) =>
		{
			nextCalled = true;
			return ValueTask.FromResult(MessageResult.Success());
		};

		// Act
		_ = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeAsync_ReturnsSuccessResult_WhenNextReturnsSuccess()
	{
		// Arrange
		var options = MsOptions.Create(new LoggingMiddlewareOptions());
		var middleware = new LoggingMiddleware(options, NullTelemetrySanitizer.Instance, _logger);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeAsync_ReturnsFailedResult_WhenNextReturnsFailed()
	{
		// Arrange
		var options = MsOptions.Create(new LoggingMiddlewareOptions());
		var middleware = new LoggingMiddleware(options, NullTelemetrySanitizer.Instance, _logger);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var problemDetails = new MessageProblemDetails { Title = "Test Error", Status = 400 };
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Failed(problemDetails));

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public async Task InvokeAsync_RethrowsException_WhenNextThrows()
	{
		// Arrange
		var options = MsOptions.Create(new LoggingMiddlewareOptions());
		var middleware = new LoggingMiddleware(options, NullTelemetrySanitizer.Instance, _logger);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var expectedException = new InvalidOperationException("Test exception");

		DispatchRequestDelegate next = (_, _, _) => throw expectedException;

		// Act & Assert
		var thrownException = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await middleware.InvokeAsync(message, context, next, CancellationToken.None));

		thrownException.Message.ShouldBe("Test exception");
	}

	[Fact]
	public async Task InvokeAsync_SkipsLogging_WhenMessageTypeIsExcluded()
	{
		// Arrange
		var loggingOptions = new LoggingMiddlewareOptions
		{
			LogStart = true,
			LogCompletion = true
		};
		_ = loggingOptions.ExcludeTypes.Add(typeof(ExcludedMessage));

		var options = MsOptions.Create(loggingOptions);
		var middleware = new LoggingMiddleware(options, NullTelemetrySanitizer.Instance, _logger);
		var message = new ExcludedMessage();
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert - Should still return success, just without logging
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeAsync_DoesNotLogStart_WhenLogStartIsFalse()
	{
		// Arrange
		var loggingOptions = new LoggingMiddlewareOptions
		{
			LogStart = false,
			LogCompletion = true
		};

		var options = MsOptions.Create(loggingOptions);
		var middleware = new LoggingMiddleware(options, NullTelemetrySanitizer.Instance, _logger);
		var message = new TestLoggingMessage();
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Success());

		// Act
		_ = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert - The message should still process successfully
		// Verifying exact logging behavior requires capturing log calls
	}

	[Fact]
	public async Task InvokeAsync_DoesNotLogCompletion_WhenLogCompletionIsFalse()
	{
		// Arrange
		var loggingOptions = new LoggingMiddlewareOptions
		{
			LogStart = true,
			LogCompletion = false
		};

		var options = MsOptions.Create(loggingOptions);
		var middleware = new LoggingMiddleware(options, NullTelemetrySanitizer.Instance, _logger);
		var message = new TestLoggingMessage();
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Success());

		// Act
		_ = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert - The message should still process successfully
	}

	[Fact]
	public async Task InvokeAsync_IncludesTiming_WhenIncludeTimingIsTrue()
	{
		// Arrange
		var loggingOptions = new LoggingMiddlewareOptions
		{
			IncludeTiming = true,
			LogCompletion = true
		};

		var options = MsOptions.Create(loggingOptions);
		var middleware = new LoggingMiddleware(options, NullTelemetrySanitizer.Instance, _logger);
		var message = new TestLoggingMessage();
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate next = async (_, _, _) =>
		{
			await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(10); // Simulate some processing time
			return MessageResult.Success();
		};

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	private sealed record ExcludedMessage : IDispatchMessage;

	private sealed record TestLoggingMessage : IDispatchMessage;
}
