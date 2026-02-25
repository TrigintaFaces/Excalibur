// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Domain;

/// <summary>
/// Provides an event processor that ensures events are processed sequentially in the order they are received.
/// </summary>
public sealed class OrderedEventProcessor : IAsyncDisposable, IDisposable
{
	private readonly SemaphoreSlim _semaphore = new(1, 1);

	private volatile bool _disposedValue;

	/// <summary>
	/// Processes a single event asynchronously while maintaining strict ordering.
	/// </summary>
	/// <param name="processEvent"> A delegate to process the event. </param>
	/// <returns> A task that represents the asynchronous processing operation. </returns>
	public async Task ProcessAsync(Func<Task> processEvent)
	{
		ArgumentNullException.ThrowIfNull(processEvent);
		ObjectDisposedException.ThrowIf(_disposedValue, this);

		await _semaphore.WaitAsync().ConfigureAwait(false);
		try
		{
			await processEvent().ConfigureAwait(false);
		}
		finally
		{
			_ = _semaphore.Release();
		}
	}

	/// <summary>
	/// Asynchronously disposes the ordered event processor and its resources.
	/// </summary>
	/// <returns> A ValueTask representing the asynchronous dispose operation. </returns>
	public ValueTask DisposeAsync()
	{
		_disposedValue = true;

		_semaphore.Dispose();
		GC.SuppressFinalize(this);

		return ValueTask.CompletedTask;
	}

	/// <summary>
	/// Disposes the ordered event processor and its resources.
	/// </summary>
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	private void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				_semaphore.Dispose();
			}

			_disposedValue = true;
		}
	}
}
