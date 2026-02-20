// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;

namespace Excalibur.Dispatch.Tests.Functional.Pipeline;

/// <summary>
/// Functional tests for middleware pipeline patterns in dispatch scenarios.
/// </summary>
[Trait("Category", "Functional")]
[Trait("Component", "Pipeline")]
[Trait("Feature", "Middleware")]
public sealed class MiddlewarePipelineFunctionalShould : FunctionalTestBase
{
	[Fact]
	public async Task ExecuteBehaviorsInRegisteredOrder()
	{
		// Arrange
		var executionOrder = new ConcurrentQueue<string>();
		var behaviors = new List<IDispatchMiddleware>
		{
			new OrderedBehavior("First", executionOrder),
			new OrderedBehavior("Second", executionOrder),
			new OrderedBehavior("Third", executionOrder),
		};

		var request = new TestRequest { Data = "Test" };

		// Act - Execute pipeline
		var result = await ExecutePipelineAsync(request, behaviors, () =>
		{
			executionOrder.Enqueue("Handler");
			return Task.FromResult<object>("Result");
		}).ConfigureAwait(false);

		// Assert
		var order = executionOrder.ToArray();
		order.Length.ShouldBe(7); // 3 before + handler + 3 after
		order[0].ShouldBe("First-Before");
		order[1].ShouldBe("Second-Before");
		order[2].ShouldBe("Third-Before");
		order[3].ShouldBe("Handler");
		order[4].ShouldBe("Third-After");
		order[5].ShouldBe("Second-After");
		order[6].ShouldBe("First-After");
	}

	[Fact]
	public async Task ApplyValidationBehavior()
	{
		// Arrange
		var validationErrors = new List<string>();
		var validationBehavior = new ValidationBehavior(req =>
		{
			if (req is TestRequest tr && string.IsNullOrEmpty(tr.Data))
			{
				validationErrors.Add("Data is required");
				return false;
			}

			return true;
		});

		var invalidRequest = new TestRequest { Data = string.Empty };
		var handlerCalled = false;

		// Act
		try
		{
			_ = await ExecutePipelineAsync(invalidRequest, [validationBehavior], () =>
			{
				handlerCalled = true;
				return Task.FromResult<object>("Result");
			}).ConfigureAwait(false);
		}
		catch (ValidationException)
		{
			// Expected
		}

		// Assert
		validationErrors.Count.ShouldBe(1);
		validationErrors[0].ShouldBe("Data is required");
		handlerCalled.ShouldBeFalse();
	}

	[Fact]
	public async Task ApplyLoggingBehavior()
	{
		// Arrange
		var logs = new ConcurrentQueue<string>();
		var loggingBehavior = new LoggingBehavior(logs);
		var request = new TestRequest { Data = "Test" };

		// Act
		_ = await ExecutePipelineAsync(request, [loggingBehavior], () =>
			Task.FromResult<object>("Result")).ConfigureAwait(false);

		// Assert
		var logEntries = logs.ToArray();
		logEntries.Length.ShouldBe(2);
		logEntries[0].ShouldContain("Handling TestRequest");
		logEntries[1].ShouldContain("Handled TestRequest");
	}

	[Fact]
	public async Task ApplyPerformanceBehavior()
	{
		// Arrange
		var metrics = new ConcurrentDictionary<string, TimeSpan>();
		var performanceBehavior = new PerformanceBehavior(metrics);
		var request = new TestRequest { Data = "Test" };

		// Act
		_ = await ExecutePipelineAsync(request, [performanceBehavior], async () =>
		{
			await Task.Delay(50).ConfigureAwait(false);
			return "Result";
		}).ConfigureAwait(false);

		// Assert
		metrics.ContainsKey("TestRequest").ShouldBeTrue();
		metrics["TestRequest"].TotalMilliseconds.ShouldBeGreaterThanOrEqualTo(40);
	}

	[Fact]
	public async Task ApplyAuthorizationBehavior()
	{
		// Arrange
		var authBehavior = new AuthorizationBehavior(isAuthorized: false);
		var request = new TestRequest { Data = "Test" };
		var handlerCalled = false;

		// Act & Assert
		_ = await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
		{
			_ = await ExecutePipelineAsync(request, [authBehavior], () =>
			{
				handlerCalled = true;
				return Task.FromResult<object>("Result");
			}).ConfigureAwait(false);
		}).ConfigureAwait(false);

		handlerCalled.ShouldBeFalse();
	}

	[Fact]
	public async Task ApplyTransactionBehavior()
	{
		// Arrange
		var transactionState = new TransactionState();
		var transactionBehavior = new TransactionBehavior(transactionState);
		var request = new TestRequest { Data = "Test" };

		// Act
		_ = await ExecutePipelineAsync(request, [transactionBehavior], () =>
			Task.FromResult<object>("Result")).ConfigureAwait(false);

		// Assert
		transactionState.WasStarted.ShouldBeTrue();
		transactionState.WasCommitted.ShouldBeTrue();
		transactionState.WasRolledBack.ShouldBeFalse();
	}

	[Fact]
	public async Task RollbackTransactionOnError()
	{
		// Arrange
		var transactionState = new TransactionState();
		var transactionBehavior = new TransactionBehavior(transactionState);
		var request = new TestRequest { Data = "Test" };

		// Act
		try
		{
			_ = await ExecutePipelineAsync(request, [transactionBehavior], () =>
			{
				throw new InvalidOperationException("Handler error");
#pragma warning disable CS0162 // Unreachable code detected
				return Task.FromResult<object>("Result");
#pragma warning restore CS0162
			}).ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
			// Expected
		}

		// Assert
		transactionState.WasStarted.ShouldBeTrue();
		transactionState.WasCommitted.ShouldBeFalse();
		transactionState.WasRolledBack.ShouldBeTrue();
	}

	[Fact]
	public async Task ApplyCachingBehavior()
	{
		// Arrange
		var cache = new ConcurrentDictionary<string, object>();
		var cachingBehavior = new CachingBehavior(cache);
		var request = new TestRequest { Data = "CacheKey" };
		var handlerCallCount = 0;

		async Task<object> Handler()
		{
			handlerCallCount++;
			await Task.Delay(10).ConfigureAwait(false);
			return "Computed Result";
		}

		// Act - First call should compute
		var result1 = await ExecutePipelineAsync(request, [cachingBehavior], Handler).ConfigureAwait(false);

		// Second call should use cache
		var result2 = await ExecutePipelineAsync(request, [cachingBehavior], Handler).ConfigureAwait(false);

		// Assert
		handlerCallCount.ShouldBe(1);
		result1.ShouldBe("Computed Result");
		result2.ShouldBe("Computed Result");
	}

	[Fact]
	public async Task ComposeBehaviorsCorrectly()
	{
		// Arrange
		var logs = new ConcurrentQueue<string>();
		var metrics = new ConcurrentDictionary<string, TimeSpan>();
		var executionOrder = new ConcurrentQueue<string>();

		var behaviors = new List<IDispatchMiddleware>
		{
			new LoggingBehavior(logs),
			new PerformanceBehavior(metrics),
			new OrderedBehavior("Inner", executionOrder),
		};

		var request = new TestRequest { Data = "Test" };

		// Act
		_ = await ExecutePipelineAsync(request, behaviors, () =>
		{
			executionOrder.Enqueue("Handler");
			return Task.FromResult<object>("Result");
		}).ConfigureAwait(false);

		// Assert
		logs.Count.ShouldBe(2);
		metrics.ContainsKey("TestRequest").ShouldBeTrue();
		var order = executionOrder.ToArray();
		order.ShouldContain("Inner-Before");
		order.ShouldContain("Handler");
		order.ShouldContain("Inner-After");
	}

	[Fact]
	public async Task HandleBehaviorExceptionsGracefully()
	{
		// Arrange
		var errorBehavior = new ErrorBehavior();
		var cleanupBehavior = new CleanupBehavior();
		var request = new TestRequest { Data = "Test" };

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
		{
			_ = await ExecutePipelineAsync(request, [cleanupBehavior, errorBehavior], () =>
				Task.FromResult<object>("Result")).ConfigureAwait(false);
		}).ConfigureAwait(false);

		cleanupBehavior.WasCleanedUp.ShouldBeTrue();
	}

	private static async Task<object> ExecutePipelineAsync(
		object request,
		List<IDispatchMiddleware> behaviors,
		Func<Task<object>> handler)
	{
		// Build pipeline from behaviors (innermost to outermost)
		Func<Task<object>> pipeline = handler;

		for (var i = behaviors.Count - 1; i >= 0; i--)
		{
			var behavior = behaviors[i];
			var next = pipeline;
			pipeline = () => behavior.HandleAsync(request, next);
		}

		return await pipeline().ConfigureAwait(false);
	}

	private interface IDispatchMiddleware
	{
		Task<object> HandleAsync(object request, Func<Task<object>> next);
	}

	private sealed class TestRequest
	{
		public string Data { get; init; } = string.Empty;
	}

	private sealed class OrderedBehavior(string name, ConcurrentQueue<string> executionOrder) : IDispatchMiddleware
	{
		public async Task<object> HandleAsync(object request, Func<Task<object>> next)
		{
			executionOrder.Enqueue($"{name}-Before");
			var result = await next().ConfigureAwait(false);
			executionOrder.Enqueue($"{name}-After");
			return result;
		}
	}

	private sealed class ValidationBehavior(Func<object, bool> validator) : IDispatchMiddleware
	{
		public Task<object> HandleAsync(object request, Func<Task<object>> next)
		{
			if (!validator(request))
			{
				throw new ValidationException("Validation failed");
			}

			return next();
		}
	}

	private sealed class LoggingBehavior(ConcurrentQueue<string> logs) : IDispatchMiddleware
	{
		public async Task<object> HandleAsync(object request, Func<Task<object>> next)
		{
			logs.Enqueue($"Handling {request.GetType().Name}");
			var result = await next().ConfigureAwait(false);
			logs.Enqueue($"Handled {request.GetType().Name}");
			return result;
		}
	}

	private sealed class PerformanceBehavior(ConcurrentDictionary<string, TimeSpan> metrics) : IDispatchMiddleware
	{
		public async Task<object> HandleAsync(object request, Func<Task<object>> next)
		{
			var sw = Stopwatch.StartNew();
			var result = await next().ConfigureAwait(false);
			sw.Stop();
			metrics[request.GetType().Name] = sw.Elapsed;
			return result;
		}
	}

	private sealed class AuthorizationBehavior(bool isAuthorized) : IDispatchMiddleware
	{
		public Task<object> HandleAsync(object request, Func<Task<object>> next)
		{
			if (!isAuthorized)
			{
				throw new UnauthorizedAccessException("Not authorized");
			}

			return next();
		}
	}

	private sealed class TransactionBehavior(TransactionState state) : IDispatchMiddleware
	{
		public async Task<object> HandleAsync(object request, Func<Task<object>> next)
		{
			state.WasStarted = true;
			try
			{
				var result = await next().ConfigureAwait(false);
				state.WasCommitted = true;
				return result;
			}
			catch
			{
				state.WasRolledBack = true;
				throw;
			}
		}
	}

	private sealed class TransactionState
	{
		public bool WasStarted { get; set; }
		public bool WasCommitted { get; set; }
		public bool WasRolledBack { get; set; }
	}

	private sealed class CachingBehavior(ConcurrentDictionary<string, object> cache) : IDispatchMiddleware
	{
		public async Task<object> HandleAsync(object request, Func<Task<object>> next)
		{
			var key = request.GetType().Name + "_" + request.GetHashCode();

			if (cache.TryGetValue(key, out var cached))
			{
				return cached;
			}

			var result = await next().ConfigureAwait(false);
			cache[key] = result;
			return result;
		}
	}

	private sealed class ErrorBehavior : IDispatchMiddleware
	{
		public Task<object> HandleAsync(object request, Func<Task<object>> next)
		{
			throw new InvalidOperationException("Behavior error");
		}
	}

	private sealed class CleanupBehavior : IDispatchMiddleware
	{
		public bool WasCleanedUp { get; private set; }

		public async Task<object> HandleAsync(object request, Func<Task<object>> next)
		{
			try
			{
				return await next().ConfigureAwait(false);
			}
			finally
			{
				WasCleanedUp = true;
			}
		}
	}

	private sealed class ValidationException(string message) : Exception(message);
}
