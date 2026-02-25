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

		var channel = Channel.CreateUnbounded<T>();

		// Producer
		var producerTask = Task.Factory.StartNew(
			async () =>
			{
				foreach (var item in items)
				{
					await channel.Writer.WriteAsync(item, cts.Token).ConfigureAwait(false);
				}

				channel.Writer.Complete();
			}, cts.Token, TaskCreationOptions.None, TaskScheduler.Default).Unwrap();

		// Consumers
		var consumerTasks = Enumerable.Range(0, parallelDegree)
			.Select(_ => Task.Factory.StartNew(
				async () =>
				{
					await foreach (var item in channel.Reader.ReadAllAsync(cts.Token).ConfigureAwait(false))
					{
						try
						{
							await processor(item, cts.Token).ConfigureAwait(false);
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
				}, cts.Token, TaskCreationOptions.None, TaskScheduler.Default).Unwrap())
			.ToArray();

		await producerTask.ConfigureAwait(false);
		await Task.WhenAll(consumerTasks).ConfigureAwait(false);

		return (successful, failed);
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
