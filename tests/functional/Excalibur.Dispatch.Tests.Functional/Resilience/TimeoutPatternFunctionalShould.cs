// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.Functional.Resilience;

/// <summary>
/// Functional tests for timeout patterns in dispatch scenarios.
/// </summary>
[Trait("Category", "Functional")]
[Trait("Component", "Resilience")]
[Trait("Feature", "Timeout")]
public sealed class TimeoutPatternFunctionalShould : FunctionalTestBase
{
	[Fact]
	public async Task TimeoutLongRunningOperation()
	{
		// Arrange — Use a large gap between timeout and operation so that thread pool
		// timer delays under heavy CI load cannot cause the operation to complete first.
		// The test only waits ~200ms (for the timeout to fire), not for the full operation.
		var timeout = TimeSpan.FromMilliseconds(200);
		var operationDuration = TimeSpan.FromSeconds(30);
		var timedOut = false;

		// Act
		// Intentional: Task.Delay simulates a long-running operation that should be cancelled by timeout
		using var cts = new CancellationTokenSource(timeout);
		try
		{
			await Task.Delay(operationDuration, cts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			timedOut = true;
		}

		// Assert
		timedOut.ShouldBeTrue("Operation should have timed out");
	}

	[Fact]
	public async Task CompleteWithinTimeout()
	{
		// Arrange
		var timeout = TimeSpan.FromMilliseconds(500);
		var operationDuration = TimeSpan.FromMilliseconds(50);
		var completed = false;

		// Act - Intentional: Task.Delay simulates an operation that completes before timeout
		using var cts = new CancellationTokenSource(timeout);
		try
		{
			await Task.Delay(operationDuration, cts.Token).ConfigureAwait(false);
			completed = true;
		}
		catch (OperationCanceledException)
		{
			completed = false;
		}

		// Assert
		completed.ShouldBeTrue("Operation should have completed within timeout");
	}

	[Fact]
	public async Task CancelOperationOnTimeout()
	{
		// Arrange
		var cancellationRequested = false;
		var timeout = TimeSpan.FromMilliseconds(200);

		// Act
		using var cts = new CancellationTokenSource(timeout);
		try
		{
			while (!cts.Token.IsCancellationRequested)
			{
				await Task.Delay(10, cts.Token).ConfigureAwait(false);
			}

			// Loop exited because token was cancelled between iterations
			cancellationRequested = true;
		}
		catch (OperationCanceledException)
		{
			cancellationRequested = true;
		}

		// Assert
		cancellationRequested.ShouldBeTrue();
	}

	[Fact]
	public void UseDefaultTimeoutWhenNotSpecified()
	{
		// Arrange
		var defaultTimeout = TimeSpan.FromSeconds(30);
		TimeSpan? specifiedTimeout = null;

		// Act
		var effectiveTimeout = specifiedTimeout ?? defaultTimeout;

		// Assert
		effectiveTimeout.ShouldBe(defaultTimeout);
	}

	[Fact]
	public void OverrideDefaultTimeoutWhenSpecified()
	{
		// Arrange
		var defaultTimeout = TimeSpan.FromSeconds(30);
		TimeSpan? specifiedTimeout = TimeSpan.FromSeconds(10);

		// Act
		var effectiveTimeout = specifiedTimeout ?? defaultTimeout;

		// Assert
		effectiveTimeout.ShouldBe(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void TrackTimeoutStatistics()
	{
		// Arrange
		var statistics = new TestTimeoutStats
		{
			TotalOperations = 100,
			CompletedWithinTimeout = 95,
			TimedOut = 5,
		};

		// Act
		var timeoutRate = (double)statistics.TimedOut / statistics.TotalOperations;

		// Assert
		timeoutRate.ShouldBe(0.05);
		statistics.CompletedWithinTimeout.ShouldBe(95);
		statistics.TimedOut.ShouldBe(5);
	}

	[Fact]
	public async Task HandleNestedTimeouts()
	{
		// Arrange - Outer timeout longer than inner. Use generous gaps to avoid
		// timer precision issues under heavy thread pool load in CI.
		var outerTimeout = TimeSpan.FromSeconds(5);
		var innerTimeout = TimeSpan.FromMilliseconds(200);
		var innerTimedOut = false;
		var outerCompleted = false;

		// Act
		using var outerCts = new CancellationTokenSource(outerTimeout);
		try
		{
			using var innerCts = new CancellationTokenSource(innerTimeout);
			try
			{
				await Task.Delay(TimeSpan.FromSeconds(2), innerCts.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				innerTimedOut = true;
			}

			outerCompleted = true;
		}
		catch (OperationCanceledException)
		{
			outerCompleted = false;
		}

		// Assert
		innerTimedOut.ShouldBeTrue("Inner operation should have timed out");
		outerCompleted.ShouldBeTrue("Outer operation should have completed");
	}

	[Fact]
	public void ConfigurePerOperationTimeouts()
	{
		// Arrange
		var operationTimeouts = new Dictionary<string, TimeSpan>
		{
			["DatabaseQuery"] = TimeSpan.FromSeconds(30),
			["ExternalApiCall"] = TimeSpan.FromSeconds(10),
			["FileUpload"] = TimeSpan.FromMinutes(5),
			["QuickLookup"] = TimeSpan.FromSeconds(2),
		};

		// Assert
		operationTimeouts["DatabaseQuery"].ShouldBe(TimeSpan.FromSeconds(30));
		operationTimeouts["ExternalApiCall"].ShouldBe(TimeSpan.FromSeconds(10));
		operationTimeouts["FileUpload"].ShouldBe(TimeSpan.FromMinutes(5));
		operationTimeouts["QuickLookup"].ShouldBe(TimeSpan.FromSeconds(2));
	}

	[Fact]
	public async Task PropagateTimeoutThroughCallStack()
	{
		// Arrange — generous gap to handle thread pool timer delays under CI load
		var timeoutOccurred = false;
		var timeout = TimeSpan.FromMilliseconds(200);

		async Task InnerOperation(CancellationToken ct)
		{
			await Task.Delay(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);
		}

		async Task MiddleOperation(CancellationToken ct)
		{
			await InnerOperation(ct).ConfigureAwait(false);
		}

		// Act
		using var cts = new CancellationTokenSource(timeout);
		try
		{
			await MiddleOperation(cts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			timeoutOccurred = true;
		}

		// Assert
		timeoutOccurred.ShouldBeTrue("Timeout should propagate through call stack");
	}

	private sealed class TestTimeoutStats
	{
		public int TotalOperations { get; init; }
		public int CompletedWithinTimeout { get; init; }
		public int TimedOut { get; init; }
	}
}
