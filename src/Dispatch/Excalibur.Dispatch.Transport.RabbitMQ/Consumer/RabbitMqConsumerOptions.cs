// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Configuration options for RabbitMQ message consumers.
/// </summary>
public sealed class RabbitMqConsumerOptions
{
	/// <summary>
	/// Gets or sets the acknowledgment mode for messages.
	/// </summary>
	/// <value>The acknowledgment mode. Default is <see cref="AckMode.Manual"/>.</value>
	public AckMode AckMode { get; set; } = AckMode.Manual;

	/// <summary>
	/// Gets or sets the retry policy for failed message processing.
	/// </summary>
	/// <value>The retry policy. Default is exponential backoff with 3 retries.</value>
	public RetryPolicy RetryPolicy { get; set; } = RetryPolicy.Exponential(maxRetries: 3);

	/// <summary>
	/// Gets or sets the dead letter exchange name for failed messages.
	/// </summary>
	/// <remarks>
	/// When set, messages that exceed <see cref="RetryPolicy"/> attempts will be routed
	/// to this exchange instead of being discarded.
	/// </remarks>
	/// <value>The dead letter exchange name. Default is <c>null</c> (no dead lettering).</value>
	public string? DeadLetterExchange { get; set; }

	/// <summary>
	/// Gets or sets the dead letter routing key.
	/// </summary>
	/// <remarks>
	/// If not set, the original message routing key will be used.
	/// </remarks>
	/// <value>The dead letter routing key. Default is <c>null</c>.</value>
	public string? DeadLetterRoutingKey { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to requeue messages on rejection
	/// when not using dead letter exchange.
	/// </summary>
	/// <value><see langword="true"/> to requeue rejected messages; otherwise, <see langword="false"/>. Default is <see langword="false"/>.</value>
	public bool RequeueOnReject { get; set; }

	/// <summary>
	/// Gets or sets the prefetch count for message consumption.
	/// </summary>
	/// <remarks>
	/// Controls how many unacknowledged messages the consumer can have.
	/// Higher values improve throughput but use more memory.
	/// </remarks>
	/// <value>The prefetch count. Default is 100.</value>
	public ushort PrefetchCount { get; set; } = 100;

	/// <summary>
	/// Gets or sets a value indicating whether prefetch is applied globally
	/// across all consumers on the channel.
	/// </summary>
	/// <value><see langword="true"/> for global prefetch; <see langword="false"/> for per-consumer prefetch. Default is <see langword="false"/>.</value>
	public bool PrefetchGlobal { get; set; }

	/// <summary>
	/// Gets or sets the batch acknowledgment size when using <see cref="AckMode.Batch"/>.
	/// </summary>
	/// <value>The batch size. Default is 100.</value>
	public int BatchAckSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the batch acknowledgment timeout when using <see cref="AckMode.Batch"/>.
	/// </summary>
	/// <remarks>
	/// Messages will be acknowledged after this timeout even if the batch is not full.
	/// </remarks>
	/// <value>The batch timeout. Default is 100 milliseconds.</value>
	public TimeSpan BatchAckTimeout { get; set; } = TimeSpan.FromMilliseconds(100);

	/// <summary>
	/// Gets or sets the consumer tag prefix.
	/// </summary>
	/// <value>The consumer tag prefix. Default is "dispatch-consumer".</value>
	public string ConsumerTag { get; set; } = "dispatch-consumer";

	/// <summary>
	/// Gets or sets a value indicating whether to include message headers
	/// in dead letter messages for diagnostic purposes.
	/// </summary>
	/// <value><see langword="true"/> to include death headers; otherwise, <see langword="false"/>. Default is <see langword="true"/>.</value>
	public bool IncludeDeathHeaders { get; set; } = true;
}
