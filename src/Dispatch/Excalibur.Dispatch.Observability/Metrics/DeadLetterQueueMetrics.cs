// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Observability.Diagnostics;

namespace Excalibur.Dispatch.Observability.Metrics;

/// <summary>
/// Provides centralized metrics collection for Dead Letter Queue operations.
/// </summary>
public sealed class DeadLetterQueueMetrics : IDeadLetterQueueMetrics, IDisposable
{
	/// <summary>
	/// The meter name for Dead Letter Queue metrics.
	/// </summary>
	public const string MeterName = "Excalibur.Dispatch.DeadLetterQueue";

	private const string DefaultQueueName = "default";

	private readonly ConcurrentDictionary<string, long> _queueDepths = new();
	private readonly TagCardinalityGuard _messageTypeGuard = new();
	private readonly TagCardinalityGuard _reasonGuard = new();
	private Counter<long> _enqueued = null!;
	private Counter<long> _replayed = null!;
	private Counter<long> _purged = null!;

	/// <summary>
	/// Initializes a new instance of the <see cref="DeadLetterQueueMetrics"/> class.
	/// </summary>
	public DeadLetterQueueMetrics()
	{
		Meter = new Meter(MeterName);
		InitializeInstruments();
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DeadLetterQueueMetrics"/> class using an <see cref="IMeterFactory"/>.
	/// </summary>
	/// <param name="meterFactory"> The meter factory for DI-managed meter lifecycle. </param>
	public DeadLetterQueueMetrics(IMeterFactory meterFactory)
	{
		ArgumentNullException.ThrowIfNull(meterFactory);
		Meter = meterFactory.Create(MeterName);
		InitializeInstruments();
	}

	/// <inheritdoc />
	public Meter Meter { get; }

	/// <inheritdoc />
	public void RecordEnqueued(string messageType, string reason, string? sourceQueue = null)
	{
		var tags = new List<KeyValuePair<string, object?>>
		{
			new("message_type", _messageTypeGuard.Guard(messageType)), new("reason", _reasonGuard.Guard(reason)),
		};

		if (!string.IsNullOrEmpty(sourceQueue))
		{
			tags.Add(new("source_queue", sourceQueue));
		}

		_enqueued.Add(1, [.. tags]);
	}

	/// <inheritdoc />
	public void RecordReplayed(string messageType, bool success)
	{
		_replayed.Add(1,
			new KeyValuePair<string, object?>("message_type", _messageTypeGuard.Guard(messageType)),
			new KeyValuePair<string, object?>("success", success));
	}

	/// <inheritdoc />
	public void RecordPurged(long count, string reason)
	{
		_purged.Add(count,
			new KeyValuePair<string, object?>("reason", _reasonGuard.Guard(reason)));
	}

	/// <inheritdoc />
	public void UpdateDepth(long depth, string? queueName = null)
	{
		var name = queueName ?? DefaultQueueName;
		_queueDepths[name] = depth;
	}

	/// <inheritdoc />
	public void Dispose() => Meter.Dispose();

	private void InitializeInstruments()
	{
		_enqueued = Meter.CreateCounter<long>(
			"dispatch.dlq.enqueued",
			"count",
			"Total number of messages enqueued to the dead letter queue");

		_replayed = Meter.CreateCounter<long>(
			"dispatch.dlq.replayed",
			"count",
			"Total number of messages replayed from the dead letter queue");

		_purged = Meter.CreateCounter<long>(
			"dispatch.dlq.purged",
			"count",
			"Total number of messages purged from the dead letter queue");

		// Create observable gauge that reports current depth for each queue
		_ = Meter.CreateObservableGauge(
			"dispatch.dlq.depth",
			observeValues: GetQueueDepths,
			unit: "messages",
			description: "Current number of messages in the dead letter queue");
	}

	private IEnumerable<Measurement<long>> GetQueueDepths()
	{
		foreach (var kvp in _queueDepths)
		{
			yield return new Measurement<long>(
				kvp.Value,
				new KeyValuePair<string, object?>("queue_name", kvp.Key));
		}
	}
}
