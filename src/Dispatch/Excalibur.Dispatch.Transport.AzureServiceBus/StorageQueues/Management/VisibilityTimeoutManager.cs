// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;


using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Manages visibility timeout renewal for Azure Storage Queue messages.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="VisibilityTimeoutManager" /> class.
/// </remarks>
/// <param name="queueClient"> The queue client. </param>
/// <param name="logger"> The logger instance. </param>
public sealed class VisibilityTimeoutManager(QueueClient queueClient, ILogger<VisibilityTimeoutManager> logger)
	: IVisibilityTimeoutManager, IAsyncDisposable
{
	private readonly QueueClient _queueClient = queueClient ?? throw new ArgumentNullException(nameof(queueClient));
	private readonly ILogger<VisibilityTimeoutManager> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly ConcurrentDictionary<string, RenewalTask> _activeRenewals = new(StringComparer.Ordinal);
	private readonly SemaphoreSlim _renewalSemaphore = new(10);
	private volatile bool _disposed;

	/// <inheritdoc />
	public Task StartRenewalAsync(QueueMessage message, int renewalIntervalSeconds, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ThrowIfDisposed();

		if (_activeRenewals.ContainsKey(message.MessageId))
		{
			_logger.LogDebug("Renewal already active for message {MessageId}", message.MessageId);
			return Task.CompletedTask;
		}

		var renewalTask = new RenewalTask(message, TimeSpan.FromSeconds(renewalIntervalSeconds));
		if (_activeRenewals.TryAdd(message.MessageId, renewalTask))
		{
			_ = Task.Run(() => RenewVisibilityLoopAsync(renewalTask, cancellationToken), cancellationToken);
			_logger.LogDebug("Started visibility renewal for message {MessageId}", message.MessageId);
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public async Task UpdateVisibilityAsync(string messageId, string popReceipt, TimeSpan visibilityTimeout,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(popReceipt);
		ThrowIfDisposed();

		await _renewalSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var response = await _queueClient
				.UpdateMessageAsync(messageId, popReceipt, visibilityTimeout: visibilityTimeout, cancellationToken: cancellationToken)
				.ConfigureAwait(false);

			// Update the pop receipt if we have an active renewal
			if (_activeRenewals.TryGetValue(messageId, out var renewalTask))
			{
				renewalTask.UpdatePopReceipt(response.Value.PopReceipt);
			}

			_logger.LogDebug("Updated visibility timeout for message {MessageId}", messageId);
		}
		catch (RequestFailedException ex) when (ex.Status == 404)
		{
			_logger.LogDebug("Message {MessageId} no longer exists for visibility update", messageId);
			StopRenewal(messageId);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to update visibility timeout for message {MessageId}", messageId);
			throw;
		}
		finally
		{
			_ = _renewalSemaphore.Release();
		}
	}

	/// <inheritdoc />
	public void StopRenewal(string messageId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

		if (_activeRenewals.TryRemove(messageId, out var renewalTask))
		{
			renewalTask.Cancel();
			renewalTask.Dispose();
			_logger.LogDebug("Stopped visibility renewal for message {MessageId}", messageId);
		}
	}

	/// <inheritdoc />
	public async Task StopAllRenewalsAsync(CancellationToken cancellationToken)
	{
		var renewalTasks = _activeRenewals.Values.ToArray();
		_activeRenewals.Clear();

		foreach (var renewalTask in renewalTasks)
		{
			renewalTask.Cancel();
		}

		_logger.LogDebug("Stopped all {Count} active visibility renewals", renewalTasks.Length);
		await Task.CompletedTask.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		await StopAllRenewalsAsync(CancellationToken.None).ConfigureAwait(false);
		_renewalSemaphore.Dispose();
		_disposed = true;
	}

	private async Task RenewVisibilityLoopAsync(RenewalTask renewalTask, CancellationToken cancellationToken)
	{
		using var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, renewalTask.CancellationToken);
		var combinedToken = combinedTokenSource.Token;

		try
		{
			while (!combinedToken.IsCancellationRequested)
			{
				await Task.Delay(renewalTask.RenewalInterval, combinedToken).ConfigureAwait(false);

				if (combinedToken.IsCancellationRequested)
				{
					break;
				}

				try
				{
					await UpdateVisibilityAsync(renewalTask.MessageId, renewalTask.PopReceipt, TimeSpan.FromMinutes(10), combinedToken)
						.ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Failed to renew visibility for message {MessageId}", renewalTask.MessageId);

					// Stop renewal on repeated failures
					if (renewalTask.IncrementFailureCount() > 3)
					{
						_logger.LogError("Stopping renewal for message {MessageId} after multiple failures", renewalTask.MessageId);
						break;
					}
				}
			}
		}
		finally
		{
			_ = _activeRenewals.TryRemove(renewalTask.MessageId, out _);
		}
	}

	private void ThrowIfDisposed()
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(VisibilityTimeoutManager));
		}
	}

	/// <summary>
	/// Represents a renewal task for a specific message.
	/// </summary>
	private sealed class RenewalTask(QueueMessage message, TimeSpan renewalInterval) : IDisposable
	{
		private readonly CancellationTokenSource _cancellationTokenSource = new();
#if NET9_0_OR_GREATER

		private readonly Lock _lock = new();

#else

		private readonly object _lock = new();

#endif
		private string _popReceipt = message.PopReceipt;
		private int _failureCount;

		public string MessageId { get; } = message.MessageId;

		public TimeSpan RenewalInterval { get; } = renewalInterval;

		public CancellationToken CancellationToken => _cancellationTokenSource.Token;

		public string PopReceipt
		{
			get
			{
				lock (_lock)
				{
					return _popReceipt;
				}
			}
		}

		public void UpdatePopReceipt(string newPopReceipt)
		{
			lock (_lock)
			{
				_popReceipt = newPopReceipt;
			}
		}

		public int IncrementFailureCount()
		{
			lock (_lock)
			{
				return ++_failureCount;
			}
		}

		public void Cancel()
		{
			_cancellationTokenSource.Cancel();
			_cancellationTokenSource.Dispose();
		}

		public void Dispose()
		{
			_cancellationTokenSource.Cancel();
			_cancellationTokenSource.Dispose();
		}
	}
}
