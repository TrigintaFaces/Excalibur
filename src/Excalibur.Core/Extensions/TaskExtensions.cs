namespace Excalibur.Core.Extensions;

public static class TaskExtensions
{
	public static async Task TimeoutAfter(this Task task, TimeSpan timeout)
	{
		ArgumentNullException.ThrowIfNull(task);

		using var cts = new CancellationTokenSource();

		var delayTask = Task.Delay(timeout, cts.Token);
		var completedTask = await Task.WhenAny(task, delayTask).ConfigureAwait(false);

		if (completedTask == delayTask)
		{
			throw new TimeoutException($"The operation did not complete within the timeout of {timeout.TotalSeconds} seconds.");
		}

		await cts.CancelAsync().ConfigureAwait(false);
		await task.ConfigureAwait(false);
	}

	public static async Task<T> TimeoutAfter<T>(this Task<T> task, TimeSpan timeout)
	{
		ArgumentNullException.ThrowIfNull(task);

		using var cts = new CancellationTokenSource();

		var delayTask = Task.Delay(timeout, cts.Token);
		var completedTask = await Task.WhenAny(task, delayTask).ConfigureAwait(false);

		if (completedTask == delayTask)
		{
			throw new TimeoutException($"The operation did not complete within the timeout of {timeout.TotalSeconds} seconds.");
		}

		await cts.CancelAsync().ConfigureAwait(false);
		return await task.ConfigureAwait(false);
	}
}
