// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Threading.Channels;

namespace Excalibur.Dispatch.Delivery.BatchProcessing;

/// <summary>
/// Utility class for batch processing with parallel execution support.
/// </summary>
public static class Batching
{
	/// <summary>
	/// Processes a batch of items in parallel with the specified degree of parallelism.
	/// </summary>
	/// <typeparam name="T"> The type of items to process. </typeparam>
	/// <param name="items"> The collection of items to process. </param>
	/// <param name="processor"> The async function to process each item. </param>
	/// <param name="parallelDegree"> The degree of parallelism. </param>
	/// <param name="timeout"> The timeout for processing the batch. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A tuple containing successful results and failed items with their exceptions. </returns>
	public static async Task<(List<T> Successful, List<(T Item, Exception Exception)> Failed)> ProcessBatchAsync<T>(
		IEnumerable<T> items,
		Func<T, CancellationToken, Task> processor,
		int parallelDegree,
		TimeSpan timeout,
		CancellationToken cancellationToken)
	{
		var successful = new List<T>();
		var failed = new List<(T Item, Exception Exception)>();

		using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		cts.CancelAfter(timeout);

		var channel = Channel.CreateBounded<T>(new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.Wait });

		// Producer
		var producerTask = Task.Factory.StartNew(
			() => ProduceItemsAsync(items, channel.Writer, cts.Token),
			cts.Token,
			TaskCreationOptions.None,
			TaskScheduler.Default).Unwrap();

		// Consumers
		var consumerTasks = new Task[parallelDegree];
		for (var i = 0; i < parallelDegree; i++)
		{
			consumerTasks[i] = Task.Factory.StartNew(
				() => ConsumeItemsAsync(channel.Reader, processor, successful, failed, cts.Token),
				cts.Token,
				TaskCreationOptions.None,
				TaskScheduler.Default).Unwrap();
		}

		await producerTask.ConfigureAwait(false);
		await Task.WhenAll(consumerTasks).ConfigureAwait(false);

		return (successful, failed);
	}

	private static async Task ProduceItemsAsync<T>(
		IEnumerable<T> items,
		ChannelWriter<T> writer,
		CancellationToken cancellationToken)
	{
		Exception? completionException = null;
		try
		{
			foreach (var item in items)
			{
				await writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
			}
		}
		catch (Exception ex)
		{
			completionException = ex;
			throw;
		}
		finally
		{
			writer.TryComplete(completionException);
		}
	}

	private static async Task ConsumeItemsAsync<T>(
		ChannelReader<T> reader,
		Func<T, CancellationToken, Task> processor,
		List<T> successful,
		List<(T Item, Exception Exception)> failed,
		CancellationToken cancellationToken)
	{
		await foreach (var item in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
		{
			try
			{
				await processor(item, cancellationToken).ConfigureAwait(false);
				lock (successful)
				{
					successful.Add(item);
				}
			}
			catch (Exception ex)
			{
				lock (failed)
				{
					failed.Add((item, ex));
				}
			}
		}
	}

	/// <summary>
	/// Processes items in batches with a callback for each completed batch.
	/// </summary>
	/// <typeparam name="T"> The type of items to process. </typeparam>
	/// <param name="items"> The collection of items to process. </param>
	/// <param name="batchSize"> The size of each batch. </param>
	/// <param name="batchProcessor"> The async function to process each batch. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The total number of items processed. </returns>
	public static async Task<int> ProcessInBatchesAsync<T>(
		IEnumerable<T> items,
		int batchSize,
		Func<List<T>, CancellationToken, Task> batchProcessor,
		CancellationToken cancellationToken)
	{
		var totalProcessed = 0;
		var batch = new List<T>(batchSize);

		foreach (var item in items)
		{
			batch.Add(item);

			if (batch.Count >= batchSize)
			{
				await batchProcessor(batch, cancellationToken).ConfigureAwait(false);
				totalProcessed += batch.Count;
				batch.Clear();
			}
		}

		// Process remaining items
		if (batch.Count > 0)
		{
			await batchProcessor(batch, cancellationToken).ConfigureAwait(false);
			totalProcessed += batch.Count;
		}

		return totalProcessed;
	}
}
