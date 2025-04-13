using Excalibur.DataAccess;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess;

public class OrderedEventProcessorExtendedShould
{
	[Fact]
	public async Task ProcessMultipleEventsSequentiallyUnderConcurrentLoad()
	{
		// Arrange
		using var processor = new OrderedEventProcessor();
		var result = new List<int>();
		var tasks = new List<Task>();
		var random = new Random(42); // Fixed seed for reproducibility

		// Create 10 events with random delays to simulate concurrent processing
		for (var i = 0; i < 10; i++)
		{
			var eventId = i;
			var task = processor.ProcessAsync(async () =>
			{
				// Random delay to simulate work
				await Task.Delay(random.Next(10, 50)).ConfigureAwait(true);
				result.Add(eventId);
			});
			tasks.Add(task);
		}

		// Act
		await Task.WhenAll(tasks).ConfigureAwait(true);

		// Assert Events should be processed in order despite random delays
		result.Count.ShouldBe(10);
		for (var i = 0; i < 10; i++)
		{
			result[i].ShouldBe(i);
		}
	}

	[Fact]
	public async Task ContinueProcessingAfterExceptionInEvent()
	{
		// Arrange
		using var processor = new OrderedEventProcessor();
		var executionOrder = new List<string>();

		// Act & Assert First event runs normally
		await processor.ProcessAsync(async () =>
		{
			await Task.Delay(10).ConfigureAwait(true);
			executionOrder.Add("First");
		}).ConfigureAwait(true);

		// Second event throws an exception
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
		{
			await processor.ProcessAsync(() =>
			{
				executionOrder.Add("Second");
				throw new InvalidOperationException("Test exception");
			}).ConfigureAwait(true);
		}).ConfigureAwait(true);

		// Third event should still process
		await processor.ProcessAsync(async () =>
		{
			await Task.Delay(10).ConfigureAwait(true);
			executionOrder.Add("Third");
		}).ConfigureAwait(true);

		// Assert
		executionOrder.Count.ShouldBe(3);
		executionOrder[0].ShouldBe("First");
		executionOrder[1].ShouldBe("Second");
		executionOrder[2].ShouldBe("Third");
	}

	[Fact]
	public async Task ReleaseResourcesWhenDisposeAsyncCalled()
	{
		// Arrange
		var processor = new OrderedEventProcessor();
		var disposed = false;

		// Act
		await processor.ProcessAsync(async () =>
		{
			await Task.Delay(10).ConfigureAwait(true);
		}).ConfigureAwait(true);

		await processor.DisposeAsync().ConfigureAwait(true);

		// Try to process another event after disposal
		try
		{
			await processor.ProcessAsync(() => Task.CompletedTask).ConfigureAwait(true);
		}
		catch (ObjectDisposedException)
		{
			disposed = true;
		}

		// Assert
		disposed.ShouldBeTrue();
	}

	[Fact]
	public async Task MaintainOrderWhenEventsHaveDifferentDurations()
	{
		// Arrange
		using var processor = new OrderedEventProcessor();
		var sequence = string.Empty;

		// Act Start several events with different durations
		var tasks = new List<Task>
		{
			// Long running task started first
			processor.ProcessAsync(async () =>
			{
				await Task.Delay(100).ConfigureAwait(true);
				sequence += "A";
			}),

			// Medium duration task started second
			processor.ProcessAsync(async () =>
			{
				await Task.Delay(50).ConfigureAwait(true);
				sequence += "B";
			}),

			// Short duration task started third
			processor.ProcessAsync(async () =>
			{
				await Task.Delay(10).ConfigureAwait(true);
				sequence += "C";
			})
		};

		await Task.WhenAll(tasks).ConfigureAwait(true);

		// Assert - should process in order despite different durations
		sequence.ShouldBe("ABC");
	}
}
