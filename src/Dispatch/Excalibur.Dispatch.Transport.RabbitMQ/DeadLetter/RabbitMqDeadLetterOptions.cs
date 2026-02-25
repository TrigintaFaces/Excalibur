// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Configuration options for RabbitMQ dead letter queue management.
/// </summary>
/// <remarks>
/// <para>
/// RabbitMQ uses dead letter exchanges (DLX) with <c>x-dead-letter-exchange</c> and
/// <c>x-dead-letter-routing-key</c> queue arguments to route failed messages.
/// This class composes with the existing <see cref="RabbitMQDeadLetterOptions"/> from transport configuration.
/// </para>
/// </remarks>
public sealed class RabbitMqDeadLetterOptions
{
	/// <summary>
	/// Gets or sets the dead letter exchange name.
	/// </summary>
	/// <value>The DLX exchange name. Defaults to <c>dead-letters</c>.</value>
	public string Exchange { get; set; } = "dead-letters";

	/// <summary>
	/// Gets or sets the dead letter queue name.
	/// </summary>
	/// <value>The DLQ queue name. Defaults to <c>dead-letter-queue</c>.</value>
	public string QueueName { get; set; } = "dead-letter-queue";

	/// <summary>
	/// Gets or sets the routing key for dead letter messages.
	/// </summary>
	/// <value>The routing key. Defaults to <c>#</c>.</value>
	public string RoutingKey { get; set; } = "#";

	/// <summary>
	/// Gets or sets a value indicating whether to include exception stack traces in DLQ message headers.
	/// </summary>
	/// <value><see langword="true"/> to include stack traces; otherwise, <see langword="false"/>. Defaults to <see langword="true"/>.</value>
	public bool IncludeStackTrace { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum number of messages to retrieve in a single batch.
	/// </summary>
	/// <value>The batch size. Defaults to 100.</value>
	public int MaxBatchSize { get; set; } = 100;
}
