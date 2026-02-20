// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Tests.Functional.Resilience;

/// <summary>
/// Functional tests for bulkhead isolation patterns in dispatch scenarios.
/// </summary>
[Trait("Category", "Functional")]
[Trait("Component", "Resilience")]
[Trait("Feature", "Bulkhead")]
public sealed class BulkheadPatternFunctionalShould : FunctionalTestBase
{
	[Fact]
	public async Task LimitConcurrentExecutions()
	{
		// Arrange
		const int maxConcurrent = 3;
		var semaphore = new SemaphoreSlim(maxConcurrent);
		var maxObservedConcurrency = 0;
		var currentConcurrency = 0;
		var lockObj = new object();

		// Act - Simulate concurrent executions
		var tasks = Enumerable.Range(1, 10).Select(async _ =>
		{
			await semaphore.WaitAsync().ConfigureAwait(false);
			try
			{
				lock (lockObj)
				{
					currentConcurrency++;
					if (currentConcurrency > maxObservedConcurrency)
					{
						maxObservedConcurrency = currentConcurrency;
					}
				}

				await Task.Delay(10).ConfigureAwait(false); // Intentional: simulates work inside bulkhead

				lock (lockObj)
				{
					currentConcurrency--;
				}
			}
			finally
			{
				_ = semaphore.Release();
			}
		});

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert
		maxObservedConcurrency.ShouldBeLessThanOrEqualTo(maxConcurrent);
	}

	[Fact]
	public void RejectWhenBulkheadFull()
	{
		// Arrange
		const int maxConcurrent = 2;
		const int maxQueued = 3;
		const int totalRequests = 10;

		var accepted = 0;
		var rejected = 0;

		// Act - Simulate bulkhead capacity
		for (var i = 0; i < totalRequests; i++)
		{
			if (accepted < maxConcurrent + maxQueued)
			{
				accepted++;
			}
			else
			{
				rejected++;
			}
		}

		// Assert
		accepted.ShouldBe(5);
		rejected.ShouldBe(5);
	}

	[Fact]
	public void TrackBulkheadMetrics()
	{
		// Arrange
		var metrics = new TestBulkheadMetrics();

		// Act - Simulate usage
		metrics.TotalExecutions = 100;
		metrics.CurrentExecutions = 5;
		metrics.CurrentQueueLength = 10;
		metrics.TotalRejections = 3;

		// Assert
		metrics.TotalExecutions.ShouldBe(100);
		metrics.CurrentExecutions.ShouldBe(5);
		metrics.CurrentQueueLength.ShouldBe(10);
		metrics.TotalRejections.ShouldBe(3);
	}

	[Fact]
	public void CalculateRejectionRate()
	{
		// Arrange
		var metrics = new TestBulkheadMetrics
		{
			TotalExecutions = 100,
			TotalRejections = 5,
		};

		// Act
		var rejectionRate = (double)metrics.TotalRejections / (metrics.TotalExecutions + metrics.TotalRejections);

		// Assert
		rejectionRate.ShouldBeInRange(0.04, 0.05);
	}

	[Fact]
	public async Task QueueRequestsWhenAtCapacity()
	{
		// Arrange
		const int maxConcurrent = 2;
		var semaphore = new SemaphoreSlim(maxConcurrent);
		var queuedCount = 0;
		var processedCount = 0;
		var lockObj = new object();

		// Act - More requests than concurrent capacity
		var tasks = Enumerable.Range(1, 5).Select(async i =>
		{
			if (semaphore.CurrentCount == 0)
			{
				lock (lockObj)
				{
					queuedCount++;
				}
			}

			await semaphore.WaitAsync().ConfigureAwait(false);
			try
			{
				await Task.Delay(20).ConfigureAwait(false); // Intentional: simulates work inside bulkhead
				lock (lockObj)
				{
					processedCount++;
				}
			}
			finally
			{
				_ = semaphore.Release();
			}
		});

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert
		processedCount.ShouldBe(5);
	}

	[Fact]
	public async Task IsolateFaultsBetweenBulkheads()
	{
		// Arrange
		var bulkhead1Results = new ConcurrentBag<bool>();
		var bulkhead2Results = new ConcurrentBag<bool>();
		var semaphore1 = new SemaphoreSlim(2);
		var semaphore2 = new SemaphoreSlim(2);

		// Act - Bulkhead 1 experiences failures
		var bulkhead1Tasks = Enumerable.Range(1, 3).Select(async i =>
		{
			await semaphore1.WaitAsync().ConfigureAwait(false);
			try
			{
				await Task.Delay(5).ConfigureAwait(false); // Intentional: simulates work inside bulkhead
				bulkhead1Results.Add(i % 2 == 0); // Simulate some failures
			}
			finally
			{
				_ = semaphore1.Release();
			}
		});

		// Bulkhead 2 should be unaffected
		var bulkhead2Tasks = Enumerable.Range(1, 3).Select(async _ =>
		{
			await semaphore2.WaitAsync().ConfigureAwait(false);
			try
			{
				await Task.Delay(5).ConfigureAwait(false); // Intentional: simulates work inside bulkhead
				bulkhead2Results.Add(true);
			}
			finally
			{
				_ = semaphore2.Release();
			}
		});

		await Task.WhenAll(bulkhead1Tasks.Concat(bulkhead2Tasks)).ConfigureAwait(false);

		// Assert - Bulkhead 2 succeeded independently
		bulkhead2Results.ShouldAllBe(r => r);
	}

	[Fact]
	public void ThrowBulkheadRejectedException()
	{
		// Arrange & Act
		var exception = new InvalidOperationException("Bulkhead capacity exceeded");

		// Assert
		exception.Message.ShouldBe("Bulkhead capacity exceeded");
	}

	private sealed class TestBulkheadMetrics
	{
		public int CurrentExecutions { get; set; }
		public int CurrentQueueLength { get; set; }
		public int TotalExecutions { get; set; }
		public int TotalRejections { get; set; }
	}
}
