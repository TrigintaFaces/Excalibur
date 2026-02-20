// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Configuration options for Confluent Schema Registry message consumption.
/// </summary>
/// <remarks>
/// <para>
/// These options control the behavior of <see cref="ConfluentMessageProcessor"/>
/// when consuming messages with Confluent wire format.
/// </para>
/// </remarks>
public sealed class ConfluentConsumerOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to automatically acknowledge messages
	/// after successful processing.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to automatically acknowledge messages; otherwise,
	/// <see langword="false"/>. Default is <see langword="true"/>.
	/// </value>
	/// <remarks>
	/// When set to <see langword="false"/>, the consumer is responsible for
	/// explicit acknowledgment using the message context.
	/// </remarks>
	public bool AutoAcknowledge { get; set; } = true;

	/// <summary>
	/// Gets or sets the error handling strategy for deserialization failures.
	/// </summary>
	/// <value>
	/// The error handling strategy. Default is <see cref="DeserializationErrorHandling.DeadLetter"/>.
	/// </value>
	/// <remarks>
	/// <para>
	/// The default <see cref="DeserializationErrorHandling.DeadLetter"/> strategy
	/// preserves failed messages for inspection while allowing continued processing.
	/// </para>
	/// </remarks>
	public DeserializationErrorHandling ErrorHandling { get; set; } = DeserializationErrorHandling.DeadLetter;

	/// <summary>
	/// Gets or sets the topic name for dead letter messages.
	/// </summary>
	/// <value>
	/// The dead letter topic name, or <see langword="null"/> to use the default
	/// naming convention (<c>{original-topic}.DLQ</c>).
	/// </value>
	/// <remarks>
	/// Only used when <see cref="ErrorHandling"/> is set to
	/// <see cref="DeserializationErrorHandling.DeadLetter"/>.
	/// </remarks>
	public string? DeadLetterTopic { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable automatic upcasting
	/// for versioned messages.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to enable automatic upcasting; otherwise,
	/// <see langword="false"/>. Default is <see langword="true"/>.
	/// </value>
	/// <remarks>
	/// When enabled, the processor will use <see cref="UpcastingConfluentDeserializer"/>
	/// if <c>IUpcastingPipeline</c> is registered in the service provider.
	/// </remarks>
	public bool EnableUpcasting { get; set; } = true;
}
