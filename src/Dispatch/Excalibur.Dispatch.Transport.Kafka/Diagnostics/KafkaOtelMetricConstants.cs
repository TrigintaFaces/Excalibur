// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Constants for Kafka OpenTelemetry metrics.
/// </summary>
[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Logical grouping of telemetry constants")]
public static class KafkaOtelMetricConstants
{
	/// <summary>
	/// Metric instrument names for Kafka transport operations.
	/// </summary>
	public static class Instruments
	{
		/// <summary>
		/// Counter for total messages produced to Kafka.
		/// </summary>
		public const string MessagesProduced = "dispatch.kafka.messages.produced";

		/// <summary>
		/// Counter for total messages consumed from Kafka.
		/// </summary>
		public const string MessagesConsumed = "dispatch.kafka.messages.consumed";

		/// <summary>
		/// Observable gauge for current consumer lag.
		/// </summary>
		public const string ConsumerLag = "dispatch.kafka.consumer.lag";

		/// <summary>
		/// UpDownCounter for the number of assigned partitions.
		/// </summary>
		public const string PartitionCount = "dispatch.kafka.partition.count";
	}

	/// <summary>
	/// Tag names for Kafka metrics.
	/// </summary>
	public static class Tags
	{
		/// <summary>
		/// The Kafka topic name.
		/// </summary>
		public const string Topic = "kafka.topic";

		/// <summary>
		/// The Kafka consumer group.
		/// </summary>
		public const string ConsumerGroup = "kafka.consumer_group";

		/// <summary>
		/// The Kafka partition number.
		/// </summary>
		public const string Partition = "kafka.partition";
	}
}
