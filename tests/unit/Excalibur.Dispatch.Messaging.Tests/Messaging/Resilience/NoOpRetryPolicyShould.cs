// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Tests.Messaging.Resilience;

/// <summary>
/// Tests for the <see cref="NoOpRetryPolicy"/> class.
/// Sprint 44: Unit tests for IRetryPolicy implementations.
/// Task: Excalibur.Dispatch-qu9v
/// </summary>
[Trait("Category", "Unit")]
public sealed class NoOpRetryPolicyShould
{
	#region Singleton Pattern Tests

	[Fact]
	public void ProvideStaticSingletonInstance()
	{
		// Act
		var instance = NoOpRetryPolicy.Instance;

		// Assert
		_ = instance.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnSameInstanceOnMultipleAccesses()
	{
		// Act
		var instance1 = NoOpRetryPolicy.Instance;
		var instance2 = NoOpRetryPolicy.Instance;

		// Assert
		instance1.ShouldBeSameAs(instance2);
	}

	[Fact]
	public async Task BeThreadSafe()
	{
		// Arrange
		var instances = new NoOpRetryPolicy[100];
		var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		// Act - Trigger concurrent reads without blocking worker threads on a barrier.
		var tasks = Enumerable.Range(0, instances.Length).Select(async i =>
		{
			await startGate.Task.ConfigureAwait(false);
			instances[i] = NoOpRetryPolicy.Instance;
		});
		startGate.SetResult();
		await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

		// Assert - All instances should be the same
		for (var i = 1; i < instances.Length; i++)
		{
			instances[i].ShouldBeSameAs(instances[0]);
		}
	}

	#endregion Singleton Pattern Tests

	#region Pass-Through Execution Tests

	[Fact]
	public async Task ExecuteAndReturnResultDirectly()
	{
		// Arrange
		var policy = NoOpRetryPolicy.Instance;

		// Act
		var result = await policy.ExecuteAsync(async ct =>
		{
			ct.ThrowIfCancellationRequested();
			await Task.Yield();
			return 42;
		}, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public async Task ExecuteVoidActionDirectly()
	{
		// Arrange
		var policy = NoOpRetryPolicy.Instance;
		var executed = false;

		// Act
		await policy.ExecuteAsync(async ct =>
		{
			ct.ThrowIfCancellationRequested();
			await Task.Yield();
			executed = true;
		}, CancellationToken.None).ConfigureAwait(false);

		// Assert
		executed.ShouldBeTrue();
	}

	[Fact]
	public async Task ExecuteExactlyOnce()
	{
		// Arrange
		var policy = NoOpRetryPolicy.Instance;
		var executionCount = 0;

		// Act
		_ = await policy.ExecuteAsync(async ct =>
		{
			executionCount++;
			ct.ThrowIfCancellationRequested();
			await Task.Yield();
			return "result";
		}, CancellationToken.None).ConfigureAwait(false);

		// Assert
		executionCount.ShouldBe(1);
	}

	[Fact]
	public async Task PassCancellationTokenToAction()
	{
		// Arrange
		var policy = NoOpRetryPolicy.Instance;
		using var cts = new CancellationTokenSource();
		CancellationToken receivedToken = default;

		// Act
		_ = await policy.ExecuteAsync(ct =>
		{
			receivedToken = ct;
			return Task.FromResult("result");
		}, cts.Token).ConfigureAwait(false);

		// Assert
		receivedToken.ShouldBe(cts.Token);
	}

	#endregion Pass-Through Execution Tests

	#region Exception Propagation Tests

	[Fact]
	public async Task PropagateExceptionImmediately()
	{
		// Arrange
		var policy = NoOpRetryPolicy.Instance;
		var executionCount = 0;

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await policy.ExecuteAsync<string>(ct =>
			{
				executionCount++;
				throw new InvalidOperationException("Test error");
			}, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		// Should only execute once - no retry
		executionCount.ShouldBe(1);
	}

	[Fact]
	public async Task PropagateExceptionWithOriginalStackTrace()
	{
		// Arrange
		var policy = NoOpRetryPolicy.Instance;
		var originalException = new InvalidOperationException("Original error");

		// Act & Assert
		var caughtException = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await policy.ExecuteAsync<string>(ct => throw originalException, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		caughtException.ShouldBeSameAs(originalException);
	}

	[Fact]
	public async Task PropagateVoidActionException()
	{
		// Arrange
		var policy = NoOpRetryPolicy.Instance;
		var executionCount = 0;

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(async () =>
			await policy.ExecuteAsync(ct =>
			{
				executionCount++;
				throw new ArgumentException("Void action error");
			}, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		executionCount.ShouldBe(1);
	}

	[Fact]
	public async Task PropagateOperationCanceledException()
	{
		// Arrange
		var policy = NoOpRetryPolicy.Instance;

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
			await policy.ExecuteAsync<string>(ct =>
				throw new OperationCanceledException("Cancelled"), CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
	}

	[Fact]
	public async Task PropagateTaskCanceledException()
	{
		// Arrange
		var policy = NoOpRetryPolicy.Instance;

		// Act & Assert
		_ = await Should.ThrowAsync<TaskCanceledException>(async () =>
			await policy.ExecuteAsync<string>(ct =>
				throw new TaskCanceledException("Cancelled"), CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
	}

	#endregion Exception Propagation Tests

	#region Null Action Validation Tests

	[Fact]
	public async Task ThrowArgumentNullExceptionForNullGenericAction()
	{
		// Arrange
		var policy = NoOpRetryPolicy.Instance;

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await policy.ExecuteAsync<string>(null!, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionForNullVoidAction()
	{
		// Arrange
		var policy = NoOpRetryPolicy.Instance;

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await policy.ExecuteAsync(null!, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
	}

	#endregion Null Action Validation Tests

	#region Cancellation Token Tests

	[Fact]
	public async Task RespectCancellationToken()
	{
		// Arrange
		var policy = NoOpRetryPolicy.Instance;
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
			await policy.ExecuteAsync(async ct =>
			{
				ct.ThrowIfCancellationRequested();
				await Task.Yield();
				return "result";
			}, cts.Token).ConfigureAwait(false)).ConfigureAwait(false);
	}

	[Fact]
	public async Task UseCancellationTokenNoneByDefault()
	{
		// Arrange
		var policy = NoOpRetryPolicy.Instance;
		CancellationToken receivedToken = default;

		// Act - Call with CancellationToken.None
		_ = await policy.ExecuteAsync(ct =>
		{
			receivedToken = ct;
			return Task.FromResult("result");
		}, CancellationToken.None).ConfigureAwait(false);

		// Assert
		receivedToken.ShouldBe(CancellationToken.None);
	}

	#endregion Cancellation Token Tests

	#region IRetryPolicy Interface Compliance Tests

	[Fact]
	public void ImplementIRetryPolicyInterface()
	{
		// Act
		var policy = NoOpRetryPolicy.Instance;

		// Assert
		_ = policy.ShouldBeAssignableTo<IRetryPolicy>();
	}

	[Fact]
	public async Task WorkWithDifferentResultTypes()
	{
		// Arrange
		var policy = NoOpRetryPolicy.Instance;

		// Act & Assert - int
		var intResult = await policy.ExecuteAsync(ct => Task.FromResult(42), CancellationToken.None).ConfigureAwait(false);
		intResult.ShouldBe(42);

		// Act & Assert - string
		var stringResult = await policy.ExecuteAsync(ct => Task.FromResult("hello"), CancellationToken.None).ConfigureAwait(false);
		stringResult.ShouldBe("hello");

		// Act & Assert - complex type
		var complexResult = await policy.ExecuteAsync(ct => Task.FromResult(new { Name = "test", Value = 123 }), CancellationToken.None).ConfigureAwait(false);
		complexResult.Name.ShouldBe("test");
		complexResult.Value.ShouldBe(123);

		// Act & Assert - null result
		var nullResult = await policy.ExecuteAsync(ct => Task.FromResult<string?>(null), CancellationToken.None).ConfigureAwait(false);
		nullResult.ShouldBeNull();
	}

	#endregion IRetryPolicy Interface Compliance Tests
}
