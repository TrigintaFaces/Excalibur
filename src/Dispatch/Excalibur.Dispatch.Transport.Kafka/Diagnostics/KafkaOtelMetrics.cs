// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Provides OpenTelemetry metric instruments for Kafka transport operations.
/// </summary>
/// <remarks>
/// <para>
/// Emits the following Kafka-specific metrics aligned with OpenTelemetry semantic conventions:
/// <list type="bullet">
/// <item><c>dispatch.kafka.messages.produced</c> - Counter of messages produced to Kafka</item>
/// <item><c>dispatch.kafka.messages.consumed</c> - Counter of messages consumed from Kafka</item>
/// <item><c>dispatch.kafka.consumer.lag</c> - Gauge of consumer lag (messages behind)</item>
/// <item><c>dispatch.kafka.partition.count</c> - UpDownCounter of assigned partitions</item>
/// </list>
/// </para>
/// </remarks>
public sealed class KafkaOtelMetrics : IDisposable
{
	/// <summary>
	/// The meter name for Kafka transport metrics.
	/// </summary>
	public const string MeterName = "Excalibur.Dispatch.Transport.Kafka";

	private readonly bool _ownsMeter;
	private long _currentConsumerLag;

	/// <summary>
	/// Initializes a new instance of the <see cref="KafkaOtelMetrics"/> class.
	/// </summary>
	public KafkaOtelMetrics()
	{
		Meter = new Meter(MeterName);
		_ownsMeter = true;
		InitializeInstruments();
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="KafkaOtelMetrics"/> class using an <see cref="IMeterFactory"/>.
	/// </summary>
	/// <param name="meterFactory">The meter factory for DI-managed meter lifecycle.</param>
	public KafkaOtelMetrics(IMeterFactory meterFactory)
	{
		ArgumentNullException.ThrowIfNull(meterFactory);
		Meter = meterFactory.Create(MeterName);
		InitializeInstruments();
	}

	/// <summary>
	/// Gets the meter instance.
	/// </summary>
	public Meter Meter { get; }

	/// <summary>
	/// Gets the counter for messages produced to Kafka.
	/// </summary>
	public Counter<long> MessagesProduced { get; private set; } = null!;

	/// <summary>
	/// Gets the counter for messages consumed from Kafka.
	/// </summary>
	public Counter<long> MessagesConsumed { get; private set; } = null!;

	/// <summary>
	/// Gets the gauge for consumer lag.
	/// </summary>
	public ObservableGauge<long> ConsumerLag { get; private set; } = null!;

	/// <summary>
	/// Gets the up/down counter for assigned partition count.
	/// </summary>
	public UpDownCounter<int> PartitionCount { get; private set; } = null!;

	/// <summary>
	/// Records a message produced to Kafka.
	/// </summary>
	/// <param name="topic">The Kafka topic.</param>
	/// <param name="partition">The partition number.</param>
	public void RecordMessageProduced(string topic, int partition)
	{
		MessagesProduced.Add(1,
			new KeyValuePair<string, object?>(KafkaOtelMetricConstants.Tags.Topic, topic),
			new KeyValuePair<string, object?>(KafkaOtelMetricConstants.Tags.Partition, partition));
	}

	/// <summary>
	/// Records a message consumed from Kafka.
	/// </summary>
	/// <param name="topic">The Kafka topic.</param>
	/// <param name="consumerGroup">The consumer group.</param>
	/// <param name="partition">The partition number.</param>
	public void RecordMessageConsumed(string topic, string consumerGroup, int partition)
	{
		MessagesConsumed.Add(1,
			new KeyValuePair<string, object?>(KafkaOtelMetricConstants.Tags.Topic, topic),
			new KeyValuePair<string, object?>(KafkaOtelMetricConstants.Tags.ConsumerGroup, consumerGroup),
			new KeyValuePair<string, object?>(KafkaOtelMetricConstants.Tags.Partition, partition));
	}

	/// <summary>
	/// Updates the current consumer lag value.
	/// </summary>
	/// <param name="lag">The current consumer lag (number of messages behind).</param>
	public void UpdateConsumerLag(long lag)
	{
		Interlocked.Exchange(ref _currentConsumerLag, lag);
	}

	/// <summary>
	/// Records a partition assignment change.
	/// </summary>
	/// <param name="delta">The change in partition count (positive for assignment, negative for revocation).</param>
	/// <param name="consumerGroup">The consumer group.</param>
	public void RecordPartitionChange(int delta, string consumerGroup)
	{
		PartitionCount.Add(delta,
			new KeyValuePair<string, object?>(KafkaOtelMetricConstants.Tags.ConsumerGroup, consumerGroup));
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_ownsMeter)
		{
			Meter.Dispose();
		}
	}

	private void InitializeInstruments()
	{
		MessagesProduced = Meter.CreateCounter<long>(
			KafkaOtelMetricConstants.Instruments.MessagesProduced,
			"messages",
			"Total number of messages produced to Kafka");

		MessagesConsumed = Meter.CreateCounter<long>(
			KafkaOtelMetricConstants.Instruments.MessagesConsumed,
			"messages",
			"Total number of messages consumed from Kafka");

		ConsumerLag = Meter.CreateObservableGauge(
			KafkaOtelMetricConstants.Instruments.ConsumerLag,
			() => Interlocked.Read(ref _currentConsumerLag),
			"messages",
			"Current consumer lag in number of messages");

		PartitionCount = Meter.CreateUpDownCounter<int>(
			KafkaOtelMetricConstants.Instruments.PartitionCount,
			"partitions",
			"Number of partitions currently assigned to this consumer");
	}
}
