// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Transport;

/// <summary>
/// Options for RabbitMQ transport.
/// </summary>
public class RabbitMQOptions
{
	/// <summary>
	/// Gets or sets the virtual host.
	/// </summary>
	/// <value> The RabbitMQ virtual host used for connections. </value>
	public string VirtualHost { get; set; } = "/";

	/// <summary>
	/// Gets or sets the prefetch count for consumers.
	/// </summary>
	/// <value> The number of messages pre-fetched per consumer. </value>
	public ushort PrefetchCount { get; set; } = 100;

	/// <summary>
	/// Gets or sets a value indicating whether to automatically acknowledge messages.
	/// </summary>
	/// <value> <see langword="true" /> to automatically acknowledge messages; otherwise, <see langword="false" />. </value>
	public bool AutoAck { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether queues should be durable.
	/// </summary>
	/// <value> <see langword="true" /> to declare durable queues; otherwise, <see langword="false" />. </value>
	public bool Durable { get; set; } = true;

	/// <summary>
	/// Gets or sets the exchange name.
	/// </summary>
	/// <value> The exchange to publish messages to. </value>
	public string Exchange { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the routing key.
	/// </summary>
	/// <value> The routing key used for message publishing. </value>
	public string RoutingKey { get; set; } = string.Empty;
}
