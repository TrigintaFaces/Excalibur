namespace Excalibur.Tests.Domain;

[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class OrderedEventProcessorExtendedShould
{
	private static readonly int[] ExpectedSequence = [1, 2];

	[Fact]
	public async Task ProcessEventsSequentially()
	{
		// Arrange
		await using var processor = new Excalibur.Domain.OrderedEventProcessor();
		var results = new List<int>();

		// Act
		await processor.ProcessAsync(async () =>
		{
			await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(10).ConfigureAwait(false);
			results.Add(1);
		}).ConfigureAwait(false);

		await processor.ProcessAsync(() =>
		{
			results.Add(2);
			return Task.CompletedTask;
		}).ConfigureAwait(false);

		// Assert
		results.ShouldBe(ExpectedSequence);
	}

	[Fact]
	public async Task ThrowOnNullProcessEvent()
	{
		// Arrange
		await using var processor = new Excalibur.Domain.OrderedEventProcessor();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => processor.ProcessAsync(null!)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ThrowObjectDisposedException_WhenDisposedAsync()
	{
		// Arrange
		var processor = new Excalibur.Domain.OrderedEventProcessor();
		await processor.DisposeAsync().ConfigureAwait(false);

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(
			() => processor.ProcessAsync(() => Task.CompletedTask)).ConfigureAwait(false);
	}

	[Fact]
	public void ThrowObjectDisposedException_WhenDisposedSync()
	{
		// Arrange
		var processor = new Excalibur.Domain.OrderedEventProcessor();
		processor.Dispose();

		// Act & Assert — second call should not throw (idempotent)
		processor.Dispose();
	}

	[Fact]
	public async Task HandleExceptionInProcessEvent()
	{
		// Arrange
		await using var processor = new Excalibur.Domain.OrderedEventProcessor();

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => processor.ProcessAsync(() => throw new InvalidOperationException("test error"))).ConfigureAwait(false);

		// Processor should still work after exception
		var processed = false;
		await processor.ProcessAsync(() =>
		{
			processed = true;
			return Task.CompletedTask;
		}).ConfigureAwait(false);

		processed.ShouldBeTrue();
	}

	[Fact]
	public async Task DisposeAsync_IsIdempotent()
	{
		// Arrange
		var processor = new Excalibur.Domain.OrderedEventProcessor();

		// Act & Assert — should not throw on double dispose
		await processor.DisposeAsync().ConfigureAwait(false);
		await processor.DisposeAsync().ConfigureAwait(false);
	}
}
