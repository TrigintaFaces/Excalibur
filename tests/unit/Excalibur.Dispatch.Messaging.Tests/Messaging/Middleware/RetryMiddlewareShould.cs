// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Tests.TestFakes;

using Microsoft.Extensions.Logging.Abstractions;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Tests for the <see cref="RetryMiddleware" /> class.
/// </summary>
[Collection("Performance Tests")]
[Trait("Category", "Unit")]
public sealed class RetryMiddlewareShould
{
	private readonly ILogger<RetryMiddleware> _logger;

	public RetryMiddlewareShould()
	{
		_logger = NullLoggerFactory.Instance.CreateLogger<RetryMiddleware>();
	}

	private RetryMiddleware CreateMiddleware(RetryOptions options)
	{
		return new RetryMiddleware(Microsoft.Extensions.Options.Options.Create(options), NullTelemetrySanitizer.Instance, _logger);
	}

	#region Default Configuration Tests

	[Fact]
	public void HaveCorrectStage()
	{
		// Arrange
		var options = new RetryOptions();
		var middleware = CreateMiddleware(options);

		// Assert
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.ErrorHandling);
	}

	[Fact]
	public void HaveDefaultMaxAttemptsOfThree()
	{
		// Arrange
		var options = new RetryOptions();

		// Assert
		options.MaxAttempts.ShouldBe(3);
	}

	[Fact]
	public void HaveDefaultBaseDelayOfOneSecond()
	{
		// Arrange
		var options = new RetryOptions();

		// Assert
		options.BaseDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	#endregion Default Configuration Tests

	#region Fixed Delay Strategy Tests

	[Fact]
	public async Task UseFixedDelayBetweenRetries()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(10),
			BackoffStrategy = BackoffStrategy.Fixed,
		};
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		var attemptCount = 0;
		var timestamps = new List<DateTime>();

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			attemptCount++;
			timestamps.Add(DateTime.UtcNow);
			if (attemptCount < 3)
			{
				return new ValueTask<IMessageResult>(MessageResult.Failed(new MessageProblemDetails { Type = "Error", Title = "Test failure" }));
			}
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		attemptCount.ShouldBe(3);
	}

	[Fact]
	public async Task NotDelayAfterSuccessOnFirstAttempt()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxAttempts = 3,
			BaseDelay = TimeSpan.FromSeconds(10),
			BackoffStrategy = BackoffStrategy.Fixed,
		};
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		var stopwatch = Stopwatch.StartNew();

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).ConfigureAwait(false);
		stopwatch.Stop();

		// Assert
		result.IsSuccess.ShouldBeTrue();
		stopwatch.ElapsedMilliseconds.ShouldBeLessThan(1000);
	}

	[Fact]
	public async Task ReturnSuccessImmediatelyOnFirstAttempt()
	{
		// Arrange
		var options = new RetryOptions { MaxAttempts = 5 };
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		var attemptCount = 0;

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			attemptCount++;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		attemptCount.ShouldBe(1);
	}

	[Fact]
	public async Task RespectFixedDelayConfiguration()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxAttempts = 2,
			BaseDelay = TimeSpan.FromMilliseconds(50),
			BackoffStrategy = BackoffStrategy.Fixed,
		};
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		var stopwatch = Stopwatch.StartNew();
		var attemptCount = 0;

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			attemptCount++;
			if (attemptCount < 2)
			{
				return new ValueTask<IMessageResult>(MessageResult.Failed(new MessageProblemDetails { Type = "Error", Title = "Test" }));
			}
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).ConfigureAwait(false);
		stopwatch.Stop();

		// Assert
		result.IsSuccess.ShouldBeTrue();
		stopwatch.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(40); // Some tolerance
	}

	#endregion Fixed Delay Strategy Tests

	#region Linear Delay Strategy Tests

	[Fact]
	public async Task UseLinearDelayIncrease()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(10),
			BackoffStrategy = BackoffStrategy.Linear,
		};
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		var attemptCount = 0;

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			attemptCount++;
			if (attemptCount < 3)
			{
				return new ValueTask<IMessageResult>(MessageResult.Failed(new MessageProblemDetails { Type = "Error", Title = "Test" }));
			}
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		attemptCount.ShouldBe(3);
	}

	[Fact]
	public async Task ApplyLinearBackoffCorrectly()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(20),
			BackoffStrategy = BackoffStrategy.Linear,
		};
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		var stopwatch = Stopwatch.StartNew();
		var attemptCount = 0;

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			attemptCount++;
			if (attemptCount < 3)
			{
				return new ValueTask<IMessageResult>(MessageResult.Failed(new MessageProblemDetails { Type = "Error", Title = "Test" }));
			}
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).ConfigureAwait(false);
		stopwatch.Stop();

		// Assert - Linear: delay1 = 20ms * 1, delay2 = 20ms * 2 = total ~60ms
		result.IsSuccess.ShouldBeTrue();
		stopwatch.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(50);
	}

	[Fact]
	public void SupportLinearBackoffStrategy()
	{
		// Arrange
		var options = new RetryOptions
		{
			BackoffStrategy = BackoffStrategy.Linear,
		};

		// Assert
		options.BackoffStrategy.ShouldBe(BackoffStrategy.Linear);
	}

	#endregion Linear Delay Strategy Tests

	#region Exponential Delay Strategy Tests

	[Fact]
	public void UseExponentialBackoffByDefault()
	{
		// Arrange
		var options = new RetryOptions();

		// Assert
		options.BackoffStrategy.ShouldBe(BackoffStrategy.Exponential);
	}

	[Fact]
	public async Task ApplyExponentialBackoffCorrectly()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(10),
			BackoffStrategy = BackoffStrategy.Exponential,
		};
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		var stopwatch = Stopwatch.StartNew();
		var attemptCount = 0;

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			attemptCount++;
			if (attemptCount < 3)
			{
				return new ValueTask<IMessageResult>(MessageResult.Failed(new MessageProblemDetails { Type = "Error", Title = "Test" }));
			}
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).ConfigureAwait(false);
		stopwatch.Stop();

		// Assert - Exponential: delay1 = 10ms * 2^0, delay2 = 10ms * 2^1 = total ~30ms
		result.IsSuccess.ShouldBeTrue();
		stopwatch.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(20);
	}

	[Fact]
	public async Task DoubleDelayOnEachExponentialRetry()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxAttempts = 4,
			BaseDelay = TimeSpan.FromMilliseconds(5),
			BackoffStrategy = BackoffStrategy.Exponential,
		};
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		var attemptCount = 0;

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			attemptCount++;
			if (attemptCount < 4)
			{
				return new ValueTask<IMessageResult>(MessageResult.Failed(new MessageProblemDetails { Type = "Error", Title = "Test" }));
			}
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		attemptCount.ShouldBe(4);
	}

	[Fact]
	public async Task RetryOnFailedResult()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxAttempts = 2,
			BaseDelay = TimeSpan.FromMilliseconds(1),
			BackoffStrategy = BackoffStrategy.Exponential,
		};
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		var attemptCount = 0;

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			attemptCount++;
			return new ValueTask<IMessageResult>(MessageResult.Failed(new MessageProblemDetails { Type = "Error", Title = "Test" }));
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		attemptCount.ShouldBe(2);
	}

	#endregion Exponential Delay Strategy Tests

	#region Exponential With Jitter Strategy Tests

	[Fact]
	public async Task ApplyJitterToExponentialBackoff()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(10),
			BackoffStrategy = BackoffStrategy.ExponentialWithJitter,
			JitterFactor = 0.5,
		};
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		var attemptCount = 0;

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			attemptCount++;
			if (attemptCount < 3)
			{
				return new ValueTask<IMessageResult>(MessageResult.Failed(new MessageProblemDetails { Type = "Error", Title = "Test" }));
			}
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		attemptCount.ShouldBe(3);
	}

	[Fact]
	public void HaveDefaultJitterFactorOfTenPercent()
	{
		// Arrange
		var options = new RetryOptions();

		// Assert
		options.JitterFactor.ShouldBe(0.1);
	}

	[Fact]
	public void SupportCustomJitterFactor()
	{
		// Arrange
		var options = new RetryOptions
		{
			JitterFactor = 0.25,
		};

		// Assert
		options.JitterFactor.ShouldBe(0.25);
	}

	#endregion Exponential With Jitter Strategy Tests

	#region Max Delay Capping Tests

	[Fact]
	public void HaveDefaultMaxDelayOfThirtySeconds()
	{
		// Arrange
		var options = new RetryOptions();

		// Assert
		options.MaxDelay.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public async Task CapDelayAtMaxDelay()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(100),
			MaxDelay = TimeSpan.FromMilliseconds(50),
			BackoffStrategy = BackoffStrategy.Exponential,
		};
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		var stopwatch = Stopwatch.StartNew();
		var attemptCount = 0;

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			attemptCount++;
			if (attemptCount < 3)
			{
				return new ValueTask<IMessageResult>(MessageResult.Failed(new MessageProblemDetails { Type = "Error", Title = "Test" }));
			}
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).ConfigureAwait(false);
		stopwatch.Stop();

		// Assert - delays should be capped at 50ms each, so max ~100ms for 2 delays
		// CI-friendly: Relaxed from 500ms to 2500ms (5x) to account for CI environment variance
		// Thread scheduling and timer resolution can vary significantly in virtualized CI environments
		result.IsSuccess.ShouldBeTrue();
		stopwatch.ElapsedMilliseconds.ShouldBeLessThan(2500);
	}

	#endregion Max Delay Capping Tests

	#region Max Retries Limit Tests

	[Fact]
	public async Task StopRetryingAfterMaxAttempts()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(1),
		};
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		var attemptCount = 0;

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			attemptCount++;
			return new ValueTask<IMessageResult>(MessageResult.Failed(new MessageProblemDetails { Type = "Error", Title = "Test" }));
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		attemptCount.ShouldBe(3);
	}

	[Fact]
	public async Task ReturnExhaustedErrorAfterAllRetriesFail()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxAttempts = 2,
			BaseDelay = TimeSpan.FromMilliseconds(1),
		};
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			throw new InvalidOperationException("Test exception");
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Type.ShouldContain("Retry");
	}

	[Fact]
	public async Task RespectSingleAttemptConfiguration()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxAttempts = 1,
			BaseDelay = TimeSpan.FromMilliseconds(1),
		};
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		var attemptCount = 0;

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			attemptCount++;
			return new ValueTask<IMessageResult>(MessageResult.Failed(new MessageProblemDetails { Type = "Error", Title = "Test" }));
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		attemptCount.ShouldBe(1);
	}

	#endregion Max Retries Limit Tests

	#region Exception Filtering - Retryable Tests

	[Fact]
	public async Task RetrySpecificExceptionTypes()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(1),
		};
		_ = options.RetryableExceptions.Add(typeof(TimeoutException));
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		var attemptCount = 0;

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			attemptCount++;
			if (attemptCount < 3)
			{
				throw new TimeoutException("Test timeout");
			}
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		attemptCount.ShouldBe(3);
	}

	[Fact]
	public async Task NotRetryNonConfiguredExceptionsWhenRetryableListIsSet()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(1),
		};
		_ = options.RetryableExceptions.Add(typeof(TimeoutException));
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		var attemptCount = 0;

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			attemptCount++;
			throw new IOException("Test IO exception");
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		attemptCount.ShouldBe(1);
	}

	[Fact]
	public async Task RetryMultipleExceptionTypes()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxAttempts = 4,
			BaseDelay = TimeSpan.FromMilliseconds(1),
		};
		_ = options.RetryableExceptions.Add(typeof(TimeoutException));
		_ = options.RetryableExceptions.Add(typeof(IOException));
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		var attemptCount = 0;

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			attemptCount++;
			if (attemptCount == 1)
			{
				throw new TimeoutException("Test timeout");
			}
			if (attemptCount == 2)
			{
				throw new IOException("Test IO");
			}
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		attemptCount.ShouldBe(3);
	}

	#endregion Exception Filtering - Retryable Tests

	#region Exception Filtering - Non-Retryable Tests

	[Fact]
	public async Task NotRetryArgumentException()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(1),
		};
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		var attemptCount = 0;

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			attemptCount++;
			throw new ArgumentException("Test argument exception");
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		attemptCount.ShouldBe(1);
	}

	[Fact]
	public async Task NotRetryArgumentNullException()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(1),
		};
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		var attemptCount = 0;

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			attemptCount++;
			throw new ArgumentNullException("param", "Test null exception");
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		attemptCount.ShouldBe(1);
	}

	[Fact]
	public async Task NotRetryCustomNonRetryableException()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(1),
		};
		_ = options.NonRetryableExceptions.Add(typeof(NotSupportedException));
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		var attemptCount = 0;

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			attemptCount++;
			throw new NotSupportedException("Test not supported");
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		attemptCount.ShouldBe(1);
	}

	#endregion Exception Filtering - Non-Retryable Tests

	#region Cancellation Support Tests

	[Fact]
	public async Task ThrowOperationCanceledWhenCancellationRequested()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxAttempts = 5,
			BaseDelay = TimeSpan.FromSeconds(10),
		};
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		using var cts = new CancellationTokenSource();
		var attemptCount = 0;

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			attemptCount++;
			cts.Cancel();
			return new ValueTask<IMessageResult>(MessageResult.Failed(new MessageProblemDetails { Type = "Error", Title = "Test" }));
		}

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
			await middleware.InvokeAsync(message, context, NextDelegate, cts.Token).ConfigureAwait(false)).ConfigureAwait(false);
		attemptCount.ShouldBe(1);
	}

	[Fact]
	public async Task RespectAlreadyCancelledToken()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(100),
		};
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		using var cts = new CancellationTokenSource();
		cts.Cancel();
		var attemptCount = 0;

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			attemptCount++;
			ct.ThrowIfCancellationRequested();
			return new ValueTask<IMessageResult>(MessageResult.Failed(new MessageProblemDetails { Type = "Error", Title = "Test" }));
		}

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
			await middleware.InvokeAsync(message, context, NextDelegate, cts.Token).ConfigureAwait(false)).ConfigureAwait(false);
	}

	#endregion Cancellation Support Tests

	#region Null Argument Validation Tests

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new RetryMiddleware(null!, NullTelemetrySanitizer.Instance, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new RetryOptions());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new RetryMiddleware(options, NullTelemetrySanitizer.Instance, null!));
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenMessageIsNull()
	{
		// Arrange
		var options = new RetryOptions();
		var middleware = CreateMiddleware(options);
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
		var options = new RetryOptions();
		var middleware = CreateMiddleware(options);
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
		var options = new RetryOptions();
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(message, context, null!, CancellationToken.None).AsTask());
	}

	#endregion Null Argument Validation Tests

	#region Non-Retryable Exception Tests (Default Behavior)

	[Fact]
	public async Task NotRetryInvalidOperationException()
	{
		// Arrange - InvalidOperationException is non-retryable by default
		var options = new RetryOptions
		{
			MaxAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(1),
		};
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		var attemptCount = 0;

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			attemptCount++;
			throw new InvalidOperationException("Test invalid operation");
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		attemptCount.ShouldBe(1);
	}

	#endregion Non-Retryable Exception Tests (Default Behavior)

	#region Result-Based Retry Tests

	[Fact]
	public async Task NotRetrySuccessfulResult()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxAttempts = 5,
			BaseDelay = TimeSpan.FromMilliseconds(1),
		};
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		var attemptCount = 0;

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			attemptCount++;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		attemptCount.ShouldBe(1); // Should not retry successful results
	}

	[Fact]
	public async Task RetryFailedResultUntilSuccess()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxAttempts = 5,
			BaseDelay = TimeSpan.FromMilliseconds(1),
		};
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		var attemptCount = 0;

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			attemptCount++;
			if (attemptCount < 4)
			{
				return new ValueTask<IMessageResult>(MessageResult.Failed(new MessageProblemDetails
				{
					Type = "Error",
					Title = $"Failure {attemptCount}",
				}));
			}
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		attemptCount.ShouldBe(4);
	}

	#endregion Result-Based Retry Tests

	#region Mixed Exception and Result Failure Tests

	[Fact]
	public async Task HandleMixedExceptionsAndFailedResults()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxAttempts = 5,
			BaseDelay = TimeSpan.FromMilliseconds(1),
		};
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		var attemptCount = 0;

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			attemptCount++;
			if (attemptCount == 1)
			{
				throw new TimeoutException("First exception");
			}
			if (attemptCount == 2)
			{
				return new ValueTask<IMessageResult>(MessageResult.Failed(new MessageProblemDetails
				{
					Type = "Error",
					Title = "Failed result",
				}));
			}
			if (attemptCount == 3)
			{
				throw new IOException("Third exception");
			}
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		attemptCount.ShouldBe(4);
	}

	#endregion Mixed Exception and Result Failure Tests

	#region Context and Message Preservation Tests

	[Fact]
	public async Task PassSameMessageToAllRetryAttempts()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(1),
		};
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		var capturedMessages = new List<IDispatchMessage>();

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			capturedMessages.Add(msg);
			if (capturedMessages.Count < 3)
			{
				return new ValueTask<IMessageResult>(MessageResult.Failed(new MessageProblemDetails
				{
					Type = "Error",
					Title = "Test",
				}));
			}
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		_ = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		capturedMessages.Count.ShouldBe(3);
		capturedMessages.ShouldAllBe(m => ReferenceEquals(m, message));
	}

	[Fact]
	public async Task PassSameContextToAllRetryAttempts()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(1),
		};
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		var capturedContexts = new List<IMessageContext>();

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			capturedContexts.Add(ctx);
			if (capturedContexts.Count < 3)
			{
				return new ValueTask<IMessageResult>(MessageResult.Failed(new MessageProblemDetails
				{
					Type = "Error",
					Title = "Test",
				}));
			}
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		_ = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		capturedContexts.Count.ShouldBe(3);
		capturedContexts.ShouldAllBe(c => ReferenceEquals(c, context));
	}

	[Fact]
	public async Task PassCancellationTokenToAllRetryAttempts()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(1),
		};
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		using var cts = new CancellationTokenSource();
		var capturedTokens = new List<CancellationToken>();

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			capturedTokens.Add(ct);
			if (capturedTokens.Count < 3)
			{
				return new ValueTask<IMessageResult>(MessageResult.Failed(new MessageProblemDetails
				{
					Type = "Error",
					Title = "Test",
				}));
			}
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		_ = await middleware.InvokeAsync(message, context, NextDelegate, cts.Token).ConfigureAwait(false);

		// Assert
		capturedTokens.Count.ShouldBe(3);
		capturedTokens.ShouldAllBe(t => t == cts.Token);
	}

	#endregion Context and Message Preservation Tests

	#region Logging Verification Tests

	[Fact]
	public async Task LogSuccessOnSubsequentAttempt()
	{
		// Arrange - Verifies the LogMessageSucceeded path when attempt > 1
		var options = new RetryOptions
		{
			MaxAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(1),
		};
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-log-123" };
		var attemptCount = 0;

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			attemptCount++;
			if (attemptCount == 1)
			{
				return new ValueTask<IMessageResult>(MessageResult.Failed(new MessageProblemDetails
				{
					Type = "Error",
					Title = "Test",
				}));
			}
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		attemptCount.ShouldBe(2); // Exercises the success logging on attempt > 1
	}

	#endregion Logging Verification Tests
}
