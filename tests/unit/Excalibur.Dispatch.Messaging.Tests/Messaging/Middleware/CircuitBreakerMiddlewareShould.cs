// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Options.Resilience;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for the <see cref="CircuitBreakerMiddleware"/> class.
/// </summary>
/// <remarks>
/// Sprint 414 - Task T414.3: CircuitBreakerMiddleware tests (0% → 50%+).
/// Tests circuit breaker pattern implementation for resilience.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
public sealed class CircuitBreakerMiddlewareShould
{
	private readonly ILogger<CircuitBreakerMiddleware> _logger;
	private readonly IDispatchMessage _message;
	private readonly IMessageContext _context;
	private readonly DispatchRequestDelegate _successDelegate;
	private readonly DispatchRequestDelegate _failureDelegate;
	private readonly DispatchRequestDelegate _exceptionDelegate;

	public CircuitBreakerMiddlewareShould()
	{
		_logger = A.Fake<ILogger<CircuitBreakerMiddleware>>();
		_message = A.Fake<IDispatchMessage>();
		_context = A.Fake<IMessageContext>();

		_ = A.CallTo(() => _context.MessageId).Returns("test-message-id");

		_successDelegate = (msg, ctx, ct) => new ValueTask<IMessageResult>(MessageResult.Success());
		_failureDelegate = (msg, ctx, ct) => new ValueTask<IMessageResult>(MessageResult.Failed(new MessageProblemDetails
		{
			Type = "TestFailure",
			Title = "Test Failure",
			Status = 500,
			Detail = "Test failure detail"
		}));
		_exceptionDelegate = (msg, ctx, ct) => throw new InvalidOperationException("Test exception");
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new CircuitBreakerMiddleware(null!, NullTelemetrySanitizer.Instance, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new CircuitBreakerOptions());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new CircuitBreakerMiddleware(options, NullTelemetrySanitizer.Instance, null!));
	}

	#endregion

	#region Stage Tests

	[Fact]
	public void HaveErrorHandlingStage()
	{
		// Arrange
		var options = MsOptions.Create(new CircuitBreakerOptions());
		var middleware = new CircuitBreakerMiddleware(options, NullTelemetrySanitizer.Instance, _logger);

		// Assert
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.ErrorHandling);
	}

	#endregion

	#region InvokeAsync Parameter Validation Tests

	[Fact]
	public async Task ThrowArgumentNullException_WhenMessageIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new CircuitBreakerOptions());
		var middleware = new CircuitBreakerMiddleware(options, NullTelemetrySanitizer.Instance, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(null!, _context, _successDelegate, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new CircuitBreakerOptions());
		var middleware = new CircuitBreakerMiddleware(options, NullTelemetrySanitizer.Instance, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(_message, null!, _successDelegate, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenNextDelegateIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new CircuitBreakerOptions());
		var middleware = new CircuitBreakerMiddleware(options, NullTelemetrySanitizer.Instance, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(_message, _context, null!, CancellationToken.None).AsTask());
	}

	#endregion

	#region Closed Circuit Tests

	[Fact]
	public async Task PassThroughSuccessfully_WhenCircuitIsClosed()
	{
		// Arrange
		var options = MsOptions.Create(new CircuitBreakerOptions());
		var middleware = new CircuitBreakerMiddleware(options, NullTelemetrySanitizer.Instance, _logger);

		// Act
		var result = await middleware.InvokeAsync(_message, _context, _successDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task RecordFailure_WhenDelegateReturnsFailed()
	{
		// Arrange
		var options = MsOptions.Create(new CircuitBreakerOptions { FailureThreshold = 5 });
		var middleware = new CircuitBreakerMiddleware(options, NullTelemetrySanitizer.Instance, _logger);

		// Act
		var result = await middleware.InvokeAsync(_message, _context, _failureDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public async Task RecordFailure_WhenDelegateThrowsException()
	{
		// Arrange
		var options = MsOptions.Create(new CircuitBreakerOptions { FailureThreshold = 5 });
		var middleware = new CircuitBreakerMiddleware(options, NullTelemetrySanitizer.Instance, _logger);

		// Act
		var result = await middleware.InvokeAsync(_message, _context, _exceptionDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Type.ShouldBe("CircuitBreakerFailure");
	}

	#endregion

	#region Circuit Opening Tests

	[Fact]
	public async Task OpenCircuit_AfterFailureThresholdIsReached()
	{
		// Arrange
		var options = MsOptions.Create(new CircuitBreakerOptions
		{
			FailureThreshold = 3,
			OpenDuration = TimeSpan.FromSeconds(30)
		});
		var middleware = new CircuitBreakerMiddleware(options, NullTelemetrySanitizer.Instance, _logger);

		// Act - Cause failures to reach threshold
		for (var i = 0; i < 3; i++)
		{
			_ = await middleware.InvokeAsync(_message, _context, _failureDelegate, CancellationToken.None);
		}

		// Next call should be rejected due to open circuit
		var result = await middleware.InvokeAsync(_message, _context, _successDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Type.ShouldBe("CircuitBreakerOpen");
		result.ProblemDetails.ErrorCode.ShouldBe(503);
	}

	[Fact]
	public async Task OpenCircuit_AfterExceptionThresholdIsReached()
	{
		// Arrange
		var options = MsOptions.Create(new CircuitBreakerOptions
		{
			FailureThreshold = 3,
			OpenDuration = TimeSpan.FromSeconds(30)
		});
		var middleware = new CircuitBreakerMiddleware(options, NullTelemetrySanitizer.Instance, _logger);

		// Act - Cause exceptions to reach threshold
		for (var i = 0; i < 3; i++)
		{
			_ = await middleware.InvokeAsync(_message, _context, _exceptionDelegate, CancellationToken.None);
		}

		// Next call should be rejected due to open circuit
		var result = await middleware.InvokeAsync(_message, _context, _successDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.ProblemDetails.Type.ShouldBe("CircuitBreakerOpen");
	}

	#endregion

	#region Circuit Key Tests

	[Fact]
	public async Task IsolateDifferentCircuitsWithCustomKeySelector()
	{
		// Arrange - Use custom key selector to simulate different message types/circuits
		var counter = 0;
		var options = MsOptions.Create(new CircuitBreakerOptions
		{
			FailureThreshold = 3,
			// Use a custom key selector that assigns different circuit keys
			CircuitKeySelector = _ => counter < 3 ? "circuit1" : "circuit2"
		});
		var middleware = new CircuitBreakerMiddleware(options, NullTelemetrySanitizer.Instance, _logger);

		// Act - Fail 3 times on circuit1 to open it
		for (var i = 0; i < 3; i++)
		{
			_ = await middleware.InvokeAsync(_message, _context, _failureDelegate, CancellationToken.None);
		}

		// Now switch to circuit2
		counter = 3;

		// circuit2 should still work (different circuit key)
		var result = await middleware.InvokeAsync(_message, _context, _successDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task UseCustomCircuitKeySelector_WhenProvided()
	{
		// Arrange
		var options = MsOptions.Create(new CircuitBreakerOptions
		{
			FailureThreshold = 3,
			CircuitKeySelector = msg => "shared-circuit"
		});
		var middleware = new CircuitBreakerMiddleware(options, NullTelemetrySanitizer.Instance, _logger);

		var message1 = A.Fake<IDispatchMessage>();
		var message2 = A.Fake<IDispatchMessage>();

		// Act - Fail message1 to open the shared circuit
		for (var i = 0; i < 3; i++)
		{
			_ = await middleware.InvokeAsync(message1, _context, _failureDelegate, CancellationToken.None);
		}

		// message2 should also be rejected (same circuit key)
		var result = await middleware.InvokeAsync(message2, _context, _successDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.ProblemDetails.Type.ShouldBe("CircuitBreakerOpen");
	}

	#endregion

	#region Success Reset Tests

	[Fact]
	public async Task ResetFailureCount_OnSuccess()
	{
		// Arrange
		var options = MsOptions.Create(new CircuitBreakerOptions { FailureThreshold = 3 });
		var middleware = new CircuitBreakerMiddleware(options, NullTelemetrySanitizer.Instance, _logger);

		// Act - Cause 2 failures then 1 success
		_ = await middleware.InvokeAsync(_message, _context, _failureDelegate, CancellationToken.None);
		_ = await middleware.InvokeAsync(_message, _context, _failureDelegate, CancellationToken.None);
		_ = await middleware.InvokeAsync(_message, _context, _successDelegate, CancellationToken.None);

		// Another 2 failures should not open circuit (was reset by success)
		_ = await middleware.InvokeAsync(_message, _context, _failureDelegate, CancellationToken.None);
		_ = await middleware.InvokeAsync(_message, _context, _failureDelegate, CancellationToken.None);

		// Circuit should still be closed
		var result = await middleware.InvokeAsync(_message, _context, _successDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	#endregion

	#region Problem Details Tests

	[Fact]
	public async Task ReturnCircuitBreakerOpenProblemDetails_WhenCircuitIsOpen()
	{
		// Arrange
		var options = MsOptions.Create(new CircuitBreakerOptions { FailureThreshold = 1 });
		var middleware = new CircuitBreakerMiddleware(options, NullTelemetrySanitizer.Instance, _logger);

		// Open the circuit
		_ = await middleware.InvokeAsync(_message, _context, _failureDelegate, CancellationToken.None);

		// Act
		var result = await middleware.InvokeAsync(_message, _context, _successDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Type.ShouldBe("CircuitBreakerOpen");
		result.ProblemDetails.Title.ShouldBe("Circuit Breaker Open");
		result.ProblemDetails.ErrorCode.ShouldBe(503);
		result.ProblemDetails.Detail.ShouldContain("Circuit breaker is open");
		result.ProblemDetails.Instance.ShouldBe("test-message-id");
	}

	[Fact]
	public async Task ReturnCircuitBreakerFailureProblemDetails_WhenExceptionOccurs()
	{
		// Arrange
		var options = MsOptions.Create(new CircuitBreakerOptions());
		var middleware = new CircuitBreakerMiddleware(options, NullTelemetrySanitizer.Instance, _logger);

		// Act
		var result = await middleware.InvokeAsync(_message, _context, _exceptionDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Type.ShouldBe("CircuitBreakerFailure");
		result.ProblemDetails.Title.ShouldBe("Circuit Breaker Failure");
		result.ProblemDetails.ErrorCode.ShouldBe(500);
		result.ProblemDetails.Detail.ShouldContain("Circuit breaker recorded failure");
	}

	#endregion

	#region Options Configuration Tests

	[Fact]
	public async Task RespectFailureThresholdConfiguration()
	{
		// Arrange
		var options = MsOptions.Create(new CircuitBreakerOptions { FailureThreshold = 10 });
		var middleware = new CircuitBreakerMiddleware(options, NullTelemetrySanitizer.Instance, _logger);

		// Act - Cause 9 failures (one less than threshold)
		for (var i = 0; i < 9; i++)
		{
			_ = await middleware.InvokeAsync(_message, _context, _failureDelegate, CancellationToken.None);
		}

		// Circuit should still be closed
		var result = await middleware.InvokeAsync(_message, _context, _successDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	#endregion
}
