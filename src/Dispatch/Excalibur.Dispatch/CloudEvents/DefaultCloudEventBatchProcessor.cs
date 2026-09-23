// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.CloudEvents;

namespace Excalibur.Dispatch.CloudEvents;

/// <summary>
/// Default implementation of CloudEvent batch processor.
/// </summary>
/// <param name="batchOptions"> Options used for batch size and thresholds. </param>
/// <param name="dispatcher"> Dispatch pipeline used to dispatch converted events. </param>
/// <param name="serviceProvider"> Service provider used to build message contexts. </param>
public sealed class DefaultCloudEventBatchProcessor(
	CloudEventBatchOptions batchOptions,
	IDispatcher dispatcher,
	IServiceProvider serviceProvider) : ICloudEventBatchProcessor
{
	private readonly CloudEventBatchOptions _batchOptions = batchOptions ?? throw new ArgumentNullException(nameof(batchOptions));
	private readonly IDispatcher _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
	private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

	/// <summary>
	/// Processes a batch of CloudEvents by converting them back to dispatch events and dispatching them.
	/// </summary>
	/// <param name="batch"> The CloudEvent batch to process. </param>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A task representing the asynchronous processing operation. </returns>
	public async Task ProcessBatchAsync(CloudEventBatch batch, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(batch);

		// Process each CloudEvent in the batch
		foreach (var cloudEvent in batch)
		{
			// Convert CloudEvent back to Dispatch message
			var dispatchEvent = cloudEvent.ToDispatchEvent();
			if (dispatchEvent != null)
			{
				// Create message context with service provider
				var context = new MessageContext(dispatchEvent, _serviceProvider);
				_ = await _dispatcher.DispatchAsync(dispatchEvent, context, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	/// <summary>
	/// Creates batches of CloudEvents from a collection of dispatch events based on the configured batch options.
	/// </summary>
	/// <param name="events"> The dispatch events to batch. </param>
	/// <param name="context"> The message context containing metadata. </param>
	/// <returns> A collection of CloudEvent batches ready for processing. </returns>
	public IReadOnlyList<CloudEventBatch> CreateBatches(IEnumerable<IDispatchEvent> events, IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(events);

		var batches = new List<CloudEventBatch>();
		var currentBatch = new CloudEventBatch(_batchOptions);

		foreach (var evt in events)
		{
			var cloudEvent = evt.ToCloudEvent(context);

			if (!currentBatch.TryAdd(cloudEvent))
			{
				if (currentBatch.Count > 0)
				{
					batches.Add(currentBatch);
					currentBatch = new CloudEventBatch(_batchOptions);
				}

				// Try to add to new batch
				if (!currentBatch.TryAdd(cloudEvent))
				{
					// Reaching here means the event was refused by a batch that was already EMPTY, so the
					// event's own size exceeds the limit and a fresh batch has the same limit. The previous
					// code added it to one more empty batch and discarded the result, which at this point
					// could only ever be false: the event was dropped and an EMPTY batch was emitted in its
					// place. An event that cannot fit an empty batch cannot be batched at all, so it is
					// refused here rather than silently lost.
					throw new InvalidOperationException(
						$"A CloudEvent of type '{cloudEvent.Type}' is larger than the maximum batch size and "
						+ "cannot be placed in any batch. Reduce the event payload or raise the configured "
						+ "maximum batch size.");
				}
			}
		}

		if (currentBatch.Count > 0)
		{
			batches.Add(currentBatch);
		}

		return batches;
	}
}
