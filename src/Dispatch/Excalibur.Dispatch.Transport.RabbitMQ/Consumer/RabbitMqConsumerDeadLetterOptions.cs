// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Dead-letter configuration for RabbitMQ message consumers.
/// </summary>
public sealed class RabbitMqConsumerDeadLetterOptions
{
	/// <summary>
	/// Gets or sets the dead letter exchange name for failed messages.
	/// </summary>
	/// <remarks>
	/// When set, messages that exceed <see cref="RabbitMqConsumerOptions.RetryPolicy"/> attempts will be routed
	/// to this exchange instead of being discarded.
	/// </remarks>
	/// <value>The dead letter exchange name. Default is <c>null</c> (no dead lettering).</value>
	public string? Exchange { get; set; }

	/// <summary>
	/// Gets or sets the dead letter routing key.
	/// </summary>
	/// <remarks>
	/// If not set, the original message routing key will be used.
	/// </remarks>
	/// <value>The dead letter routing key. Default is <c>null</c>.</value>
	public string? RoutingKey { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to requeue messages on rejection
	/// when not using dead letter exchange.
	/// </summary>
	/// <value><see langword="true"/> to requeue rejected messages; otherwise, <see langword="false"/>. Default is <see langword="false"/>.</value>
	public bool RequeueOnReject { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to include message headers
	/// in dead letter messages for diagnostic purposes.
	/// </summary>
	/// <value><see langword="true"/> to include death headers; otherwise, <see langword="false"/>. Default is <see langword="true"/>.</value>
	public bool IncludeDeathHeaders { get; set; } = true;
}
