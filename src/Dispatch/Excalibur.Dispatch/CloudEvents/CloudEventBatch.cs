// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections;
using System.Globalization;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Options.CloudEvents;

namespace Excalibur.Dispatch.CloudEvents;

/// <summary>
/// Represents a batch of CloudEvents for efficient bulk processing. Implements the CloudEvents Batch Format specification.
/// </summary>
public sealed class CloudEventBatch : IReadOnlyList<CloudEvent>
{
	private readonly List<CloudEvent> _events;
	private readonly CloudEventBatchOptions _options;

	// Note: CompositeFormat caching would be ideal but may not be available in this context

	/// <summary>
	/// Initializes a new instance of the <see cref="CloudEventBatch" /> class with default options.
	/// </summary>
	public CloudEventBatch()
		: this(options: null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CloudEventBatch" /> class.
	/// </summary>
	public CloudEventBatch(CloudEventBatchOptions? options)
	{
		_options = options ?? new CloudEventBatchOptions();
		_events = new List<CloudEvent>(_options.InitialCapacity);
		CurrentBatchSize = 0;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CloudEventBatch"/> class.
	/// Initializes a new instance with existing events.
	/// </summary>
	public CloudEventBatch(IEnumerable<CloudEvent> events, CloudEventBatchOptions? options = null)
		: this(options)
	{
		ArgumentNullException.ThrowIfNull(events);

		if (events.Any(evt => !TryAdd(evt)))
		{
			throw new InvalidOperationException(
					string.Concat(
							ErrorConstants.BatchSizeLimitExceededAtEvent,
							" ",
							(_events.Count + 1).ToString(CultureInfo.InvariantCulture)));
		}
	}

	/// <summary>
	/// Gets the maximum number of events allowed in this batch.
	/// </summary>
	/// <value> The limit on the number of events permitted in the batch. </value>
	public int MaxEvents => _options.MaxEvents;

	/// <summary>
	/// Gets the maximum total size in bytes for this batch.
	/// </summary>
	/// <value> The configured maximum batch size expressed in bytes. </value>
	public long MaxBatchSize => _options.MaxBatchSizeBytes;

	/// <summary>
	/// Gets the current estimated size of the batch in bytes.
	/// </summary>
	/// <value> The cumulative size estimate for events currently in the batch. </value>
	public long CurrentBatchSize { get; private set; }

	/// <summary>
	/// Gets the number of events in the batch.
	/// </summary>
	/// <value> The count of events currently queued in the batch. </value>
	public int Count => _events.Count;

	/// <summary>
	/// Gets the CloudEvent at the specified index.
	/// </summary>
	/// <param name="index"> Zero-based index of the event to retrieve. </param>
	public CloudEvent this[int index] => _events[index];

	/// <summary>
	/// Attempts to add a CloudEvent to the batch.
	/// </summary>
	/// <returns> True if the event was added; false if it would exceed batch limits. </returns>
	public bool TryAdd(CloudEvent cloudEvent)
	{
		ArgumentNullException.ThrowIfNull(cloudEvent);

		// Check event count limit
		if (_events.Count >= _options.MaxEvents)
		{
			return false;
		}

		// Estimate event size
		var eventSize = EstimateEventSize(cloudEvent);

		// Check batch size limit
		if (CurrentBatchSize + eventSize > _options.MaxBatchSizeBytes)
		{
			return false;
		}

		_events.Add(cloudEvent);
		CurrentBatchSize += eventSize;
		return true;
	}

	/// <summary>
	/// Adds multiple CloudEvents to the batch.
	/// </summary>
	/// <returns> The number of events successfully added. </returns>
	public int AddRange(IEnumerable<CloudEvent> events)
	{
		ArgumentNullException.ThrowIfNull(events);

		var added = 0;
		foreach (var evt in events)
		{
			if (TryAdd(evt))
			{
				added++;
			}
			else
			{
				break;
			}
		}

		return added;
	}

	/// <summary>
	/// Splits the current batch into multiple batches if needed.
	/// </summary>
	public IReadOnlyList<CloudEventBatch> Split()
	{
		if (_events.Count <= _options.MaxEvents && CurrentBatchSize <= _options.MaxBatchSizeBytes)
		{
			return [this];
		}

		var batches = new List<CloudEventBatch>();
		var currentBatch = new CloudEventBatch(_options);

		foreach (var evt in _events.Where(evt => !currentBatch.TryAdd(evt)))
		{
			if (currentBatch.Count > 0)
			{
				batches.Add(currentBatch);
				currentBatch = new CloudEventBatch(_options);
			}

			// Single event exceeds batch size - add it alone
			if (!currentBatch.TryAdd(evt))
			{
				var singleEventBatch = new CloudEventBatch(_options);
				singleEventBatch._events.Add(evt);
				singleEventBatch.CurrentBatchSize = EstimateEventSize(evt);
				batches.Add(singleEventBatch);
			}
		}

		if (currentBatch.Count > 0)
		{
			batches.Add(currentBatch);
		}

		return batches;
	}

	/// <summary>
	/// Clears all events from the batch.
	/// </summary>
	public void Clear()
	{
		_events.Clear();
		CurrentBatchSize = 0;
	}

	/// <summary>
	/// Returns an enumerator that iterates through the cloud events in this batch.
	/// </summary>
	/// <returns> An enumerator for the cloud events. </returns>
	public IEnumerator<CloudEvent> GetEnumerator() => _events.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	private static long EstimateEventSize(CloudEvent cloudEvent)
	{
		// Estimate based on key fields
		long size = 0;

		// Fixed overhead for JSON structure
		size += 100;

		// Event attributes
		size += cloudEvent.Id?.Length ?? 0;
		size += cloudEvent.Source?.ToString().Length ?? 0;
		size += cloudEvent.Type?.Length ?? 0;
		size += cloudEvent.Subject?.Length ?? 0;
		size += cloudEvent.DataContentType?.Length ?? 0;
		size += cloudEvent.DataSchema?.ToString().Length ?? 0;

		// Extension attributes
		foreach (var ext in cloudEvent.ExtensionAttributes)
		{
			size += ext.Name.Length;
			var value = cloudEvent[ext.Name];
			size += value?.ToString()?.Length ?? 0;
		}

		// Data payload estimate
		if (cloudEvent.Data != null)
		{
			size += cloudEvent.Data switch
			{
				string s => s.Length,
				byte[] bytes => bytes.Length,
				_ => 1024, // Default estimate for objects
			};
		}

		return size;
	}
}
