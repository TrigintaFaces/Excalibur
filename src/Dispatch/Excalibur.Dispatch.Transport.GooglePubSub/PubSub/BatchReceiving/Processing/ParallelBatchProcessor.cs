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
public class ParallelBatchProcessor(
	IOptions<BatchConfiguration> options,
	Func<ReceivedMessage, CancellationToken, Task<object>> messageProcessor,
	ILogger<ParallelBatchProcessor> logger,
	BatchMetricsCollector metricsCollector) : BatchProcessorBase(logger, metricsCollector)
{
	private readonly Func<ReceivedMessage, CancellationToken, Task<object>> _messageProcessor =
		messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));

	/// <inheritdoc />
	protected internal override async Task ProcessBatchCoreAsync(
		MessageBatch batch,
		List<ProcessedMessage> successfulMessages,
		List<FailedMessage> failedMessages,
		CancellationToken cancellationToken)
	{
		Logger.LogDebug(
			"Processing batch of {MessageCount} messages in parallel",
			batch.Count);

		// Create tasks for parallel processing
		var tasks = batch.Messages.Select(message =>
			ProcessMessageAsync(
				message,
				successfulMessages,
				failedMessages,
				cancellationToken));

		// Wait for all messages to be processed
		await Task.WhenAll(tasks).ConfigureAwait(false);
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
