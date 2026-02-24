// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Metrics;

namespace Excalibur.Dispatch.Tests.Messaging.Metrics;

/// <summary>
/// Unit tests for <see cref="HistogramExtensions"/>.
/// </summary>
/// <remarks>
/// Tests the histogram extension methods for timing operations.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Metrics")]
[Trait("Priority", "0")]
public sealed class HistogramExtensionsShould
{
	#region StartTimer Tests

	[Fact]
	public void StartTimer_ReturnsHistogramTimer()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act
		using var timer = histogram.StartTimer();

		// Assert - Timer is a struct, just verify it's usable
		_ = timer.ShouldBeAssignableTo<IDisposable>();
	}

	[Fact]
	public void StartTimer_WhenDisposed_RecordsToHistogram()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act
		using (histogram.StartTimer())
		{
			// Small delay to ensure measurable time
			global::Tests.Shared.Infrastructure.TestTiming.Sleep(1);
		}

		// Assert
		histogram.Count.ShouldBe(1);
	}

	[Fact]
	public void StartTimer_MultipleTimers_RecordsMultipleTimes()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act
		for (var i = 0; i < 5; i++)
		{
			using (histogram.StartTimer())
			{
				// Quick iteration
			}
		}

		// Assert
		histogram.Count.ShouldBe(5);
	}

	#endregion

	#region Time (Action) Tests

	[Fact]
	public void Time_ExecutesAction()
	{
		// Arrange
		var histogram = new ValueHistogram();
		var executed = false;

		// Act
		_ = histogram.Time(() => executed = true);

		// Assert
		executed.ShouldBeTrue();
	}

	[Fact]
	public void Time_RecordsExecutionTime()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act
		histogram.Time(() => global::Tests.Shared.Infrastructure.TestTiming.Sleep(10));

		// Assert
		histogram.Count.ShouldBe(1);
		histogram.Mean.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Time_PropagatesExceptionFromAction()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			histogram.Time(() => throw new InvalidOperationException("Test error")));
	}

	[Fact]
	public void Time_RecordsTimeEvenWhenActionThrows()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act
		try
		{
			histogram.Time(() =>
			{
				global::Tests.Shared.Infrastructure.TestTiming.Sleep(1);
				throw new InvalidOperationException("Test error");
			});
		}
		catch (InvalidOperationException)
		{
			// Expected
		}

		// Assert - Time was still recorded before exception propagated
		histogram.Count.ShouldBe(1);
	}

	[Fact]
	public void Time_WithMultipleActions_RecordsMultipleTimes()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act
		histogram.Time(() => { });
		histogram.Time(() => { });
		histogram.Time(() => { });

		// Assert
		histogram.Count.ShouldBe(3);
	}

	#endregion

	#region Time<T> (Func<T>) Tests

	[Fact]
	public void Time_Func_ReturnsResult()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act
		var result = histogram.Time(() => 42);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public void Time_Func_RecordsExecutionTime()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act
		_ = histogram.Time(() =>
		{
			global::Tests.Shared.Infrastructure.TestTiming.Sleep(10);
			return "result";
		});

		// Assert
		histogram.Count.ShouldBe(1);
		histogram.Mean.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Time_Func_PropagatesException()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			histogram.Time<int>(() => throw new InvalidOperationException("Test error")));
	}

	[Fact]
	public void Time_Func_ReturnsReferenceType()
	{
		// Arrange
		var histogram = new ValueHistogram();
		var expected = new List<int> { 1, 2, 3 };

		// Act
		var result = histogram.Time(() => expected);

		// Assert
		result.ShouldBeSameAs(expected);
	}

	[Fact]
	public void Time_Func_ReturnsNull()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act
		var result = histogram.Time<string?>(() => null);

		// Assert
		result.ShouldBeNull();
	}

	#endregion

	#region TimeAsync (Func<Task>) Tests

	[Fact]
	public async Task TimeAsync_ExecutesAsyncAction()
	{
		// Arrange
		var histogram = new ValueHistogram();
		var executed = false;

		// Act
		await histogram.TimeAsync(async () =>
		{
			await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1);
			executed = true;
		});

		// Assert
		executed.ShouldBeTrue();
	}

	[Fact]
	public async Task TimeAsync_RecordsExecutionTime()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act
		await histogram.TimeAsync(() => global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(10));

		// Assert
		histogram.Count.ShouldBe(1);
		histogram.Mean.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task TimeAsync_PropagatesException()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await histogram.TimeAsync(() => throw new InvalidOperationException("Test error")));
	}

	[Fact]
	public async Task TimeAsync_WithMultipleOperations_RecordsMultipleTimes()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act
		await histogram.TimeAsync(() => Task.CompletedTask);
		await histogram.TimeAsync(() => Task.CompletedTask);
		await histogram.TimeAsync(() => Task.CompletedTask);

		// Assert
		histogram.Count.ShouldBe(3);
	}

	#endregion

	#region TimeAsync<T> (Func<Task<T>>) Tests

	[Fact]
	public async Task TimeAsync_Func_ReturnsResult()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act
		var result = await histogram.TimeAsync(async () =>
		{
			await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1);
			return 42;
		});

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public async Task TimeAsync_Func_RecordsExecutionTime()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act
		_ = await histogram.TimeAsync(async () =>
		{
			await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(10);
			return "result";
		});

		// Assert
		histogram.Count.ShouldBe(1);
		histogram.Mean.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task TimeAsync_Func_PropagatesException()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await histogram.TimeAsync<int>(async () =>
			{
				await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1);
				throw new InvalidOperationException("Test error");
			}));
	}

	[Fact]
	public async Task TimeAsync_Func_ReturnsReferenceType()
	{
		// Arrange
		var histogram = new ValueHistogram();
		var expected = new Dictionary<string, int> { ["key"] = 1 };

		// Act
		var result = await histogram.TimeAsync(() => Task.FromResult(expected));

		// Assert
		result.ShouldBeSameAs(expected);
	}

	[Fact]
	public async Task TimeAsync_Func_ReturnsNull()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act
		var result = await histogram.TimeAsync(() => Task.FromResult<string?>(null));

		// Assert
		result.ShouldBeNull();
	}

	#endregion

	#region Integration Tests

	[Fact]
	public async Task MixedTimings_AllRecordedCorrectly()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act
		histogram.Time(() => { });
		_ = histogram.Time(() => 1);
		await histogram.TimeAsync(() => Task.CompletedTask);
		_ = await histogram.TimeAsync(() => Task.FromResult("result"));
		using (histogram.StartTimer())
		{
			// Timer
		}

		// Assert
		histogram.Count.ShouldBe(5);
	}

	[Fact]
	public void ConcurrentTimings_AreThreadSafe()
	{
		// Arrange
		var histogram = new ValueHistogram();
		var exceptions = new List<Exception>();

		// Act
		_ = Parallel.For(0, 100, i =>
		{
			try
			{
				histogram.Time(() => Thread.SpinWait(100));
			}
			catch (Exception ex)
			{
				lock (exceptions)
				{
					exceptions.Add(ex);
				}
			}
		});

		// Assert
		exceptions.ShouldBeEmpty();
		histogram.Count.ShouldBe(100);
	}

	#endregion
}
