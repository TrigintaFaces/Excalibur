// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using System.Threading.Channels;

using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Batches acknowledgments for efficient processing with Google Pub/Sub.
/// </summary>
public sealed class AcknowledgmentBatcher : IAcknowledgmentBatcher, IDisposable
{
	private readonly ILogger<AcknowledgmentBatcher> _logger;
	private readonly SubscriberServiceApiClient _client;
	private readonly string _subscriptionName;
	private readonly AcknowledgmentBatcherOptions _options;
	private readonly Channel<AckRequest> _ackChannel;
	private readonly CancellationTokenSource _shutdownCts;
	private readonly Task _processingTask;
	private readonly AcknowledgmentMetrics _metrics;
	private readonly Timer _flushTimer;
	private readonly ConcurrentDictionary<string, DateTimeOffset> _ackDeadlines;

	/// <summary>
	/// Initializes a new instance of the <see cref="AcknowledgmentBatcher" /> class.
	/// </summary>
	public AcknowledgmentBatcher(
		ILogger<AcknowledgmentBatcher> logger,
		SubscriberServiceApiClient client,
		string subscriptionName,
		AcknowledgmentBatcherOptions options)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_client = client ?? throw new ArgumentNullException(nameof(client));
		_subscriptionName = subscriptionName ?? throw new ArgumentNullException(nameof(subscriptionName));
		_options = options ?? throw new ArgumentNullException(nameof(options));

		_ackChannel = Channel.CreateUnbounded<AckRequest>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

		_shutdownCts = new CancellationTokenSource();
		_metrics = new AcknowledgmentMetrics();
		_ackDeadlines = new ConcurrentDictionary<string, DateTimeOffset>(StringComparer.Ordinal);

		// Start the processing loop
		_processingTask = ProcessAcknowledgmentsAsync(_shutdownCts.Token);

		// Start flush timer
		_flushTimer = new Timer(
			_ => _ = FlushPendingAcknowledgmentsAsync(),
			state: null,
			_options.FlushInterval,
			_options.FlushInterval);

		_logger.LogInformation("Acknowledgment batcher started for subscription {Subscription}", _subscriptionName);
	}

	/// <summary>
	/// Adds an acknowledgment to the batch queue.
	/// </summary>
	public async ValueTask AcknowledgeAsync(string ackId, DateTimeOffset deadline, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(ackId);

		// Track deadline for monitoring
		_ackDeadlines[ackId] = deadline;

		var request = new AckRequest { AckId = ackId, Timestamp = DateTimeOffset.UtcNow, Deadline = deadline };

		await _ackChannel.Writer.WriteAsync(request, cancellationToken).ConfigureAwait(false);
		_metrics.IncrementQueued();

		// Check if we're approaching deadline
		var timeToDeadline = deadline - DateTimeOffset.UtcNow;
		if (timeToDeadline < _options.DeadlineWarningThreshold)
		{
			_logger.LogWarning(
				"Acknowledgment {AckId} is approaching deadline in {TimeToDeadline}ms",
				ackId,
				timeToDeadline.TotalMilliseconds);
			_metrics.IncrementDeadlineWarnings();

			// Force immediate flush if very close to deadline
			if (timeToDeadline < TimeSpan.FromSeconds(5))
			{
				await FlushPendingAcknowledgmentsAsync().ConfigureAwait(false);
			}
		}
	}

	/// <summary>
	/// Adds an acknowledgment ID to the batch.
	/// </summary>
	public ValueTask AddAcknowledgmentAsync(string ackId, CancellationToken cancellationToken) =>

		// Use a default deadline of 10 minutes from now
		AcknowledgeAsync(ackId, DateTimeOffset.UtcNow.AddMinutes(10), cancellationToken);

	/// <summary>
	/// Adds multiple acknowledgment IDs to the batch.
	/// </summary>
	public async ValueTask AddAcknowledgmentsAsync(IEnumerable<string> ackIds, CancellationToken cancellationToken)
	{
		var deadline = DateTimeOffset.UtcNow.AddMinutes(10);
		foreach (var ackId in ackIds)
		{
			await AcknowledgeAsync(ackId, deadline, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Flushes any pending acknowledgments immediately.
	/// </summary>
	public async ValueTask FlushAsync(CancellationToken cancellationToken) =>
		await FlushPendingAcknowledgmentsAsync().ConfigureAwait(false);

	/// <summary>
	/// Gets the current metrics for the acknowledgment batcher.
	/// </summary>
	public AcknowledgmentMetrics GetMetrics() => _metrics.Clone();

	/// <summary>
	/// Asynchronously disposes the acknowledgment batcher.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		await _shutdownCts.CancelAsync().ConfigureAwait(false);
		_ = _ackChannel.Writer.TryComplete();

		try
		{
			await _processingTask.ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Expected
		}

		if (_flushTimer != null)
		{
			await _flushTimer.DisposeAsync().ConfigureAwait(false);
		}

		_shutdownCts.Dispose();

		_logger.LogInformation(
			"Acknowledgment batcher disposed. Metrics: {Metrics}",
			_metrics);
	}

	/// <summary>
	/// Disposes the acknowledgment batcher. Prefer <see cref="DisposeAsync"/> for proper async cleanup.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This method signals cancellation and cleans up resources without blocking.
	/// Use <see cref="DisposeAsync"/> for graceful shutdown that waits for pending acknowledgments.
	/// </para>
	/// <para>
	/// Per .NET best practices, synchronous Dispose should not block on async operations.
	/// </para>
	/// </remarks>
	public void Dispose()
	{
		try
		{
			_shutdownCts.Cancel();
		}
		catch (ObjectDisposedException)
		{
			// Already disposed
		}

		_ = _ackChannel.Writer.TryComplete();

		try
		{
			_flushTimer?.Dispose();
		}
		catch (ObjectDisposedException)
		{
			// Already disposed
		}

		try
		{
			_shutdownCts.Dispose();
		}
		catch (ObjectDisposedException)
		{
			// Already disposed
		}
	}

	private async Task ProcessAcknowledgmentsAsync(CancellationToken cancellationToken)
	{
		var batch = new List<AckRequest>(_options.BatchSize);

		try
		{
			await foreach (var ackRequest in _ackChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
			{
				batch.Add(ackRequest);

				// Send batch if it reaches the configured size
				if (batch.Count >= _options.BatchSize)
				{
					await SendBatchAsync(batch, cancellationToken).ConfigureAwait(false);
					batch.Clear();
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// Expected during shutdown
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error in acknowledgment processing loop");
			_metrics.IncrementErrors();
		}
		finally
		{
			// Send any remaining acknowledgments
			if (batch.Count > 0)
			{
				await SendBatchAsync(batch, CancellationToken.None).ConfigureAwait(false);
			}
		}
	}

	private async Task SendBatchAsync(List<AckRequest> batch, CancellationToken cancellationToken)
	{
		if (batch.Count == 0)
		{
			return;
		}

		var sw = ValueStopwatch.StartNew();
		var ackIds = batch.Select(static b => b.AckId).ToArray();

		try
		{
			var request = new AcknowledgeRequest { Subscription = _subscriptionName, AckIds = { ackIds } };

			await _client.AcknowledgeAsync(request, cancellationToken).ConfigureAwait(false);
			_metrics.RecordBatchSent(batch.Count, sw.Elapsed);

			// Clean up tracked deadlines
			foreach (var ackId in ackIds)
			{
				_ = _ackDeadlines.TryRemove(ackId, out _);
			}

			_logger.LogDebug(
				"Sent acknowledgment batch of {Count} messages in {ElapsedMs}ms",
				batch.Count,
				sw.ElapsedMilliseconds);
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Failed to acknowledge batch of {Count} messages after {ElapsedMs}ms",
				batch.Count,
				sw.ElapsedMilliseconds);

			_metrics.IncrementErrors();

			// On failure, check which messages are close to deadline
			foreach (var ackRequest in batch)
			{
				var timeToDeadline = ackRequest.Deadline - DateTimeOffset.UtcNow;
				if (timeToDeadline < TimeSpan.FromSeconds(10))
				{
					_logger.LogCritical(
						"Message {AckId} may be redelivered - acknowledgment failed with {TimeToDeadline}s to deadline",
						ackRequest.AckId,
						timeToDeadline.TotalSeconds);
				}
			}

			throw;
		}
	}

	private async Task FlushPendingAcknowledgmentsAsync()
	{
		var batch = new List<AckRequest>(_options.BatchSize);

		// Read all pending acknowledgments without blocking
		while (_ackChannel.Reader.TryRead(out var ackRequest))
		{
			batch.Add(ackRequest);

			if (batch.Count >= _options.BatchSize)
			{
				await SendBatchAsync(batch, CancellationToken.None).ConfigureAwait(false);
				batch.Clear();
			}
		}

		// Send any remaining
		if (batch.Count > 0)
		{
			await SendBatchAsync(batch, CancellationToken.None).ConfigureAwait(false);
		}

		// Check for messages approaching deadline
		var now = DateTimeOffset.UtcNow;
		var warningCount = 0;

		foreach (var (ackId, deadline) in _ackDeadlines)
		{
			var timeToDeadline = deadline - now;
			if (timeToDeadline < _options.DeadlineWarningThreshold)
			{
				warningCount++;
				_logger.LogWarning(
					"Message {AckId} has {TimeToDeadline}s until deadline",
					ackId,
					timeToDeadline.TotalSeconds);
			}
		}

		if (warningCount > 0)
		{
			_metrics.IncrementDeadlineWarnings(warningCount);
		}
	}

	private readonly record struct AckRequest(string AckId, DateTimeOffset Timestamp, DateTimeOffset Deadline);
}
