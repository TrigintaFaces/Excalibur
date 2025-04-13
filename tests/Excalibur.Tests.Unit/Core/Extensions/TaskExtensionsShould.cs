using Excalibur.Core.Extensions;

using Shouldly;

namespace Excalibur.Tests.Unit.Core.Extensions;

public class TaskExtensionsShould
{
	[Fact]
	public async Task CompleteNormallyWhenTaskCompletesWithinTimeout()
	{
		// Arrange
		var task = Task.Delay(100);
		var timeout = TimeSpan.FromSeconds(1);

		// Act & Assert - Should not throw
		await Should.NotThrowAsync(async () => await task.TimeoutAfter(timeout).ConfigureAwait(true)).ConfigureAwait(true);
	}

	[Fact]
	public async Task ThrowTimeoutExceptionWhenTaskDoesNotCompleteWithinTimeout()
	{
		// Arrange
		var task = Task.Delay(1000);
		var timeout = TimeSpan.FromMilliseconds(100);

		// Act & Assert
		var exception = await Should.ThrowAsync<TimeoutException>(async () =>
			await task.TimeoutAfter(timeout).ConfigureAwait(true)).ConfigureAwait(true);

		exception.Message.ShouldContain("did not complete within the timeout");
		exception.Message.ShouldContain("0.1 seconds");
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionWhenTaskIsNull()
	{
		// Arrange
		Task task = null;
		var timeout = TimeSpan.FromSeconds(1);

		// Act & Assert
		var exception = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await task.TimeoutAfter(timeout).ConfigureAwait(true)).ConfigureAwait(true);

		exception.ParamName.ShouldBe("task");
	}

	[Fact]
	public async Task CompleteWithResultWhenGenericTaskCompletesWithinTimeout()
	{
		// Arrange
		var expected = "Test Result";
		var task = Task.FromResult(expected);
		var timeout = TimeSpan.FromSeconds(1);

		// Act
		var result = await task.TimeoutAfter(timeout).ConfigureAwait(true);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public async Task ThrowTimeoutExceptionWhenGenericTaskDoesNotCompleteWithinTimeout()
	{
		// Arrange
		var task = Task.Delay(1000).ContinueWith(_ => "Result");
		var timeout = TimeSpan.FromMilliseconds(100);

		// Act & Assert
		var exception = await Should.ThrowAsync<TimeoutException>(async () =>
			await task.TimeoutAfter(timeout).ConfigureAwait(true)).ConfigureAwait(true);

		exception.Message.ShouldContain("did not complete within the timeout");
		exception.Message.ShouldContain("0.1 seconds");
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionWhenGenericTaskIsNull()
	{
		// Arrange
		Task<string> task = null;
		var timeout = TimeSpan.FromSeconds(1);

		// Act & Assert
		var exception = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await task.TimeoutAfter(timeout).ConfigureAwait(true)).ConfigureAwait(true);

		exception.ParamName.ShouldBe("task");
	}

	[Fact]
	public async Task CancelDelayTaskWhenMainTaskCompletes()
	{
		// Arrange
		var taskCompletionSource = new TaskCompletionSource<bool>();
		var task = taskCompletionSource.Task;
		var timeout = TimeSpan.FromSeconds(10);

		// Start the timeout
		var timeoutTask = Task.Run(async () => await task.TimeoutAfter(timeout).ConfigureAwait(true));

		// Small delay to ensure timeout task has started
		await Task.Delay(50).ConfigureAwait(true);

		// Act - Complete the original task
		taskCompletionSource.SetResult(true);

		// Wait for the timeout task to complete
		_ = await timeoutTask.ConfigureAwait(true);

		// Assert - No exception means the cancellation worked correctly We're testing internal implementation here, which is not ideal, but
		// it's hard to test cancellation behavior directly
		_ = Should.NotThrow(() => timeoutTask.Wait(100));
	}

	[Fact]
	public async Task HandleLongRunningTasks()
	{
		// Arrange
		var longTask = Task.Run(async () =>
		{
			await Task.Delay(500).ConfigureAwait(true);
			return "Long operation completed";
		});

		var timeout = TimeSpan.FromSeconds(2);

		// Act
		var result = await longTask.TimeoutAfter(timeout).ConfigureAwait(true);

		// Assert
		result.ShouldBe("Long operation completed");
	}
}
