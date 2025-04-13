using Excalibur.DataAccess;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess;

public class OrderedEventProcessorShould
{
	[Fact]
	public async Task ProcessEventsSequentially()
	{
		// Arrange
		var processor = new OrderedEventProcessor();
		var sequence = string.Empty;

		// Act
		await processor.ProcessAsync(async () =>
		{
			await Task.Delay(50).ConfigureAwait(true);
			sequence += "A";
		}).ConfigureAwait(true);

		await processor.ProcessAsync(async () =>
		{
			sequence += "B";
		}).ConfigureAwait(true);

		// Assert
		sequence.ShouldBe("AB");

		await processor.DisposeAsync().ConfigureAwait(true);
	}

	[Fact]
	public async Task ThrowObjectDisposedExceptionWhenDisposed()
	{
		// Arrange
		var processor = new OrderedEventProcessor();
		await processor.DisposeAsync().ConfigureAwait(true);

		// Act & Assert
		_ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
		{
			await processor.ProcessAsync(() => Task.CompletedTask).ConfigureAwait(true);
		}).ConfigureAwait(true);
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionWhenNullDelegate()
	{
		// Arrange
		var processor = new OrderedEventProcessor();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
		{
			await processor.ProcessAsync(null!).ConfigureAwait(true);
		}).ConfigureAwait(true);

		await processor.DisposeAsync().ConfigureAwait(true);
	}

	[Fact]
	public void DisposeShouldReleaseSemaphore()
	{
		// Arrange
		var processor = new OrderedEventProcessor();

		// Act
		processor.Dispose();

		// Assert Calling Dispose again should not throw
		processor.Dispose();
	}
}
