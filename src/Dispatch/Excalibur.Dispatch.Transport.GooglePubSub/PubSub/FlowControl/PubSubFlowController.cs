// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Controls the flow of messages in PubSub operations.
/// </summary>
public sealed class PubSubFlowController : IDisposable
{
	private readonly int _maxConcurrency;
	private readonly int _maxOutstandingMessages;
	private readonly SemaphoreSlim _semaphore;
	private int _outstandingMessages;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="PubSubFlowController"/> class.
	/// </summary>
	/// <param name="maxConcurrency">The maximum number of concurrent operations.</param>
	/// <param name="maxOutstandingMessages">The maximum number of outstanding messages.</param>
	public PubSubFlowController(int maxConcurrency = 100, int maxOutstandingMessages = 1000)
	{
		_maxConcurrency = maxConcurrency;
		_maxOutstandingMessages = maxOutstandingMessages;
		_semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
	}

	/// <summary>
	/// Gets the number of outstanding messages.
	/// </summary>
	public int OutstandingMessages => _outstandingMessages;

	/// <summary>
	/// Gets the available capacity.
	/// </summary>
	public int AvailableCapacity => _maxOutstandingMessages - _outstandingMessages;

	/// <summary>
	///
	/// </summary>
	/// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
	public async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken)
	{
		await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		_ = Interlocked.Increment(ref _outstandingMessages);
		return new FlowControlLease(this);
	}

	public bool TryAcquire(out IDisposable? lease)
	{
		lease = null;
		if (_outstandingMessages >= _maxOutstandingMessages)
		{
			return false;
		}

		if (_semaphore.Wait(0))
		{
			_ = Interlocked.Increment(ref _outstandingMessages);
			lease = new FlowControlLease(this);
			return true;
		}

		return false;
	}

	/// <summary>
	/// Gets the allowed batch size based on current flow control state.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public Task<int> GetAllowedBatchSizeAsync(int requestedSize, CancellationToken _)
	{
		var allowedSize = Math.Min(requestedSize, AvailableCapacity);
		return Task.FromResult(Math.Max(1, allowedSize));
	}

	/// <summary>
	/// Records that a batch has been received for flow control tracking.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public Task RecordBatchReceivedAsync(int _, CancellationToken __) =>

		// This is tracked automatically through AcquireAsync/Release
		Task.CompletedTask;

	/// <summary>
	/// Creates subscriber flow control settings from this controller's configuration.
	/// </summary>
	public global::Google.Api.Gax.FlowControlSettings CreateSubscriberFlowControlSettings() =>
		new(
			maxOutstandingElementCount: _maxOutstandingMessages,
			maxOutstandingByteCount: null);

	/// <summary>
	/// Gets flow control metrics.
	/// </summary>
	public FlowControlMetrics GetMetrics()
	{
		var metrics = new FlowControlMetrics { MaxConcurrency = _maxConcurrency, MaxOutstandingMessages = _maxOutstandingMessages };
		metrics.RecordMessageReceived(0); // Initialize with current state
		return metrics;
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_semaphore.Dispose();
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Releases a flow control lease.
	/// </summary>
	internal void Release()
	{
		_ = Interlocked.Decrement(ref _outstandingMessages);
		_ = _semaphore.Release();
	}

	private sealed class FlowControlLease(PubSubFlowController controller) : IDisposable
	{
		private volatile bool _disposed;

		public void Dispose()
		{
			if (!_disposed)
			{
				controller.Release();
				_disposed = true;
			}
		}
	}
}
