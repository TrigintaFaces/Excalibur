// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Batch processor that processes messages in parallel within each batch.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ParallelBatchProcessor" /> class.
/// </remarks>
/// <param name="options"> Batch configuration options. </param>
/// <param name="messageProcessor"> The message processing function. </param>
/// <param name="logger"> Logger instance. </param>
/// <param name="metricsCollector"> Metrics collector. </param>
internal sealed class ParallelBatchProcessor(
	IOptions<BatchOptions> options,
	Func<ReceivedMessage, CancellationToken, Task<object>> messageProcessor,
	ILogger<ParallelBatchProcessor> logger,
	BatchMetricsCollector metricsCollector) : BatchProcessorBase(logger, metricsCollector)
{
	private readonly IOptions<BatchOptions> _options = options ?? throw new ArgumentNullException(nameof(options));

	private readonly Func<ReceivedMessage, CancellationToken, Task<object>> _messageProcessor =
		messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));

	/// <inheritdoc />
	protected internal override async Task ProcessBatchCoreAsync(
		MessageBatch batch,
		List<ProcessedMessage> successfulMessages,
		List<FailedMessage> failedMessages,
		CancellationToken cancellationToken)
	{
		var maxConcurrency = _options.Value.ConcurrentBatchProcessors;

		Logger.LogDebug(
			"Processing batch of {MessageCount} messages in parallel, at most {MaxConcurrency} at a time",
			batch.Count,
			maxConcurrency);

		// Fan out over the batch, but never start more than the configured number of concurrent
		// message processors: a batch may hold up to MaxMessagesPerBatch messages, and an
		// unbounded fan-out would aim all of them at the consumer's downstream at once.
		await Parallel.ForEachAsync(
			batch.Messages,
			new ParallelOptions
			{
				MaxDegreeOfParallelism = maxConcurrency,
				CancellationToken = cancellationToken,
			},
			async (message, ct) => await ProcessMessageAsync(
				message,
				successfulMessages,
				failedMessages,
				ct).ConfigureAwait(false)).ConfigureAwait(false);
	}

	/// <inheritdoc />
	protected override Task<object> ProcessMessageCoreAsync(ReceivedMessage message, CancellationToken cancellationToken) =>
		_messageProcessor(message, cancellationToken);

	/// <inheritdoc />
	protected override bool DetermineRetryPolicy(Exception exception) =>

		// For parallel processing, be more aggressive with retries since order doesn't matter
		exception is TimeoutException ||
		exception is InvalidOperationException ||
		exception is HttpRequestException ||
		(exception is ArgumentException &&
		 exception.Message.Contains("transient", StringComparison.OrdinalIgnoreCase));

	/// <inheritdoc />
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5394:Do not use insecure randomness",
		Justification = "Random is used for retry jitter timing, not for security purposes. Cryptographic randomness is unnecessary for backoff delays.")]
	protected override TimeSpan GetRetryDelay(Exception exception) =>

		// Use exponential backoff with jitter for parallel processing
		TimeSpan.FromSeconds(Math.Min(60, Math.Pow(2, Random.Shared.Next(1, 4))));
}
