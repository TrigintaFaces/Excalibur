// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Transport.GooglePubSub;

using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// High-performance batch serializer for Google Pub/Sub messages. Optimized for throughput with memory pooling and parallel processing.
/// </summary>
[RequiresDynamicCode("Uses reflection-based serialization via MakeGenericMethod")]
[RequiresUnreferencedCode("Uses reflection-based serialization that may require unreferenced types")]
public sealed class PubSubBatchSerializer : IDisposable
{
	private readonly IMessageSerializer _serializer;
	private readonly IOptions<PubSubSerializationOptions> _options;
	private readonly ILogger<PubSubBatchSerializer> _logger;
	private readonly ArrayPool<byte> _arrayPool;
	private readonly ParallelOptions _parallelOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="PubSubBatchSerializer" /> class.
	/// </summary>
	public PubSubBatchSerializer(
		IMessageSerializer serializer,
		IOptions<PubSubSerializationOptions> options,
		ILogger<PubSubBatchSerializer> logger)
	{
		_serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		_arrayPool = ArrayPool<byte>.Create(
			options.Value.MaxBufferSize,
			options.Value.MaxBuffersPerBucket);

		_parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
	}

	/// <summary>
	/// Provides memory usage statistics for monitoring.
	/// </summary>
	public static SerializationStatistics GetStatistics() =>
		new()
		{
			ArrayPoolInUse = GC.GetTotalMemory(forceFullCollection: false),

			// Additional statistics could be tracked
		};

	/// <summary>
	/// Serializes a batch of messages efficiently.
	/// </summary>
	/// <returns> A <see cref="Task" /> representing the asynchronous operation. </returns>
	public async Task<IReadOnlyList<PubsubMessage>> SerializeBatchAsync<T>(
		IEnumerable<T> messages,
		CancellationToken cancellationToken,
		Func<T, Dictionary<string, string>>? attributeSelector = null)
		where T : notnull
	{
		using var activity = GooglePubSubTelemetryConstants.SharedActivitySource.StartActivity("SerializeBatch");

		var messageList = messages as IList<T> ?? [.. messages];
		_ = activity?.SetTag("batch.size", messageList.Count);

		if (messageList.Count == 0)
		{
			return [];
		}

		var stopwatch = ValueStopwatch.StartNew();
		var results = new PubsubMessage[messageList.Count];

		if (messageList.Count > 10 && !cancellationToken.IsCancellationRequested)
		{
			// Parallel processing for larger batches
			await Task.Factory.StartNew(
					() =>
					{
						_ = Parallel.For(0, messageList.Count, _parallelOptions, i =>
						{
							if (cancellationToken.IsCancellationRequested)
							{
								return;
							}

							var attributes = attributeSelector?.Invoke(messageList[i]);
							results[i] = _serializer.SerializeToPubSubMessage(messageList[i], attributes);
						});
					},
					cancellationToken,
					TaskCreationOptions.DenyChildAttach,
					TaskScheduler.Default)
				.ConfigureAwait(false);
		}
		else
		{
			// Sequential processing for small batches
			for (var i = 0; i < messageList.Count; i++)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var attributes = attributeSelector?.Invoke(messageList[i]);
				results[i] = _serializer.SerializeToPubSubMessage(messageList[i], attributes);
			}
		}

		var totalBytes = results.Sum(m => m.Data.Length);
		_ = activity?.SetTag("batch.bytes", totalBytes);
		_ = activity?.SetTag("batch.duration_ms", stopwatch.ElapsedMilliseconds);

		_logger.LogDebug(
			"Serialized batch of {Count} messages ({TotalBytes} bytes) in {Duration}ms",
			messageList.Count,
			totalBytes,
			stopwatch.ElapsedMilliseconds);

		return results;
	}

	/// <summary>
	/// Deserializes a batch of received messages efficiently.
	/// </summary>
	/// <returns> A <see cref="Task" /> representing the asynchronous operation. </returns>
	public async Task<IReadOnlyList<T>> DeserializeBatchAsync<T>(
		IEnumerable<ReceivedMessage> messages,
		CancellationToken cancellationToken)
		where T : notnull
	{
		using var activity = GooglePubSubTelemetryConstants.SharedActivitySource.StartActivity("DeserializeBatch");

		var messageList = messages as IList<ReceivedMessage> ?? [.. messages];
		_ = activity?.SetTag("batch.size", messageList.Count);

		if (messageList.Count == 0)
		{
			return [];
		}

		var stopwatch = ValueStopwatch.StartNew();
		var results = new T[messageList.Count];
		var errors = new List<(int Index, Exception Error)>();

		if (messageList.Count > 10 && !cancellationToken.IsCancellationRequested)
		{
			// Parallel processing for larger batches
			await Task.Factory.StartNew(
					() =>
					{
						var lockObj = new object();

						_ = Parallel.For(0, messageList.Count, _parallelOptions, i =>
						{
							if (cancellationToken.IsCancellationRequested)
							{
								return;
							}

							try
							{
								results[i] = _serializer.DeserializeFromPubSubMessage<T>(messageList[i]);
							}
							catch (Exception ex)
							{
								lock (lockObj)
								{
									errors.Add((i, ex));
								}
							}
						});
					},
					cancellationToken,
					TaskCreationOptions.DenyChildAttach,
					TaskScheduler.Default)
				.ConfigureAwait(false);
		}
		else
		{
			// Sequential processing for small batches
			for (var i = 0; i < messageList.Count; i++)
			{
				cancellationToken.ThrowIfCancellationRequested();

				try
				{
					results[i] = _serializer.DeserializeFromPubSubMessage<T>(messageList[i]);
				}
				catch (Exception ex)
				{
					errors.Add((i, ex));
				}
			}
		}

		_ = activity?.SetTag("batch.duration_ms", stopwatch.ElapsedMilliseconds);
		_ = activity?.SetTag("batch.errors", errors.Count);

		if (errors.Count > 0)
		{
			_logger.LogWarning(
				"Failed to deserialize {ErrorCount} of {TotalCount} messages",
				errors.Count,
				messageList.Count);

			// Log first few errors for debugging
			foreach (var error in errors.Take(5))
			{
				_logger.LogError(
					error.Error,
					"Failed to deserialize message at index {Index}",
					error.Index);
			}
		}

		_logger.LogDebug(
			"Deserialized batch of {Count} messages in {Duration}ms",
			messageList.Count,
			stopwatch.ElapsedMilliseconds);

		// Filter out null results from errors
		return results.Where(r => !EqualityComparer<T>.Default.Equals(r, default(T))).ToList();
	}

	/// <summary>
	/// Optimized serialization for streaming scenarios.
	/// </summary>
	public async IAsyncEnumerable<PubsubMessage> SerializeStreamAsync<T>(
		IAsyncEnumerable<T> messages,
		Func<T, Dictionary<string, string>>? attributeSelector = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
		where T : notnull
	{
		using var activity = GooglePubSubTelemetryConstants.SharedActivitySource.StartActivity("SerializeStream");

		var count = 0;
		var totalBytes = 0L;

		await foreach (var message in messages.WithCancellation(cancellationToken).ConfigureAwait(false))
		{
			var attributes = attributeSelector?.Invoke(message);
			var pubsubMessage = _serializer.SerializeToPubSubMessage(message, attributes);

			count++;
			totalBytes += pubsubMessage.Data.Length;

			yield return pubsubMessage;

			// Log progress periodically
			if (count % 1000 == 0)
			{
				_logger.LogDebug(
					"Serialized {Count} messages ({TotalBytes} bytes) in stream",
					count,
					totalBytes);
			}
		}

		_ = activity?.SetTag("stream.total_count", count);
		_ = activity?.SetTag("stream.total_bytes", totalBytes);
	}

	/// <summary>
	/// Creates an efficient batch from mixed message types.
	/// </summary>
	/// <returns> A <see cref="Task" /> representing the asynchronous operation. </returns>
	public async Task<IReadOnlyList<PubsubMessage>> SerializeMixedBatchAsync(
		IEnumerable<object> messages,
		CancellationToken cancellationToken)
	{
		using var activity = GooglePubSubTelemetryConstants.SharedActivitySource.StartActivity("SerializeMixedBatch");

		var messageList = messages as IList<object> ?? [.. messages];
		_ = activity?.SetTag("batch.size", messageList.Count);

		if (messageList.Count == 0)
		{
			return [];
		}

		// Group by type for more efficient serialization
		var messageGroups = messageList
			.Select((msg, index) => (Message: msg, Index: index))
			.GroupBy(x => x.Message.GetType())
			.ToList();

		var results = new PubsubMessage[messageList.Count];

		await Task.Factory.StartNew(
				() =>
				{
					_ = Parallel.ForEach(messageGroups, _parallelOptions, group =>
					{
						if (cancellationToken.IsCancellationRequested)
						{
							return;
						}

						foreach (var (message, index) in group)
						{
							results[index] = _serializer.SerializeToPubSubMessage(message);
						}
					});
				},
				cancellationToken,
				TaskCreationOptions.DenyChildAttach,
				TaskScheduler.Default)
			.ConfigureAwait(false);

		_ = activity?.SetTag("batch.type_count", messageGroups.Count);

		_logger.LogDebug(
			"Serialized mixed batch of {Count} messages with {TypeCount} different types",
			messageList.Count,
			messageGroups.Count);

		return results;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		// Shared static ActivitySource is process-lifetime and not disposed here.
		GC.SuppressFinalize(this);
	}
}
