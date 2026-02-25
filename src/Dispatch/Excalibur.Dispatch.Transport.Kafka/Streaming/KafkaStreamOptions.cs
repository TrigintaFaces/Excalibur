// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Configuration options for Kafka stream processing.
/// </summary>
/// <remarks>
/// <para>
/// Configures a stream processing topology that reads from an input topic,
/// applies transformations, and writes results to an output topic. This follows
/// the Kafka Streams model of stateless stream processing.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddOptions&lt;KafkaStreamOptions&gt;()
///     .Configure(options =>
///     {
///         options.InputTopic = "raw-events";
///         options.OutputTopic = "processed-events";
///         options.ApplicationId = "event-processor";
///         options.ProcessingGuarantee = ProcessingGuarantee.ExactlyOnce;
///     });
/// </code>
/// </example>
public sealed class KafkaStreamOptions
{
	/// <summary>
	/// Gets or sets the input topic to consume messages from.
	/// </summary>
	/// <value>The input topic name.</value>
	[Required]
	public string InputTopic { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the output topic to produce processed messages to.
	/// </summary>
	/// <remarks>
	/// Set to <c>null</c> or empty if the stream processor is a terminal sink
	/// (e.g., writes to a database instead of another topic).
	/// </remarks>
	/// <value>The output topic name. Default is empty (no output topic).</value>
	public string OutputTopic { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the application identifier for the stream processor.
	/// </summary>
	/// <remarks>
	/// Used as the consumer group ID and as the prefix for internal state store
	/// topics. Must be unique across stream processing applications.
	/// </remarks>
	/// <value>The application ID.</value>
	[Required]
	public string ApplicationId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the processing guarantee level.
	/// </summary>
	/// <value>The processing guarantee. Default is <see cref="Streaming.ProcessingGuarantee.AtLeastOnce"/>.</value>
	public ProcessingGuarantee ProcessingGuarantee { get; set; } = ProcessingGuarantee.AtLeastOnce;
}
