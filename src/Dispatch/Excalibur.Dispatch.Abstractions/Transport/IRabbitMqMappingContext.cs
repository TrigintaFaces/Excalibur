// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// RabbitMQ-specific mapping context for configuring message properties.
/// </summary>
public interface IRabbitMqMappingContext
{
	/// <summary>
	/// Gets or sets the exchange name.
	/// </summary>
	string? Exchange { get; set; }

	/// <summary>
	/// Gets or sets the routing key.
	/// </summary>
	string? RoutingKey { get; set; }

	/// <summary>
	/// Gets or sets the message priority (0-255).
	/// </summary>
	byte? Priority { get; set; }

	/// <summary>
	/// Gets or sets the reply-to queue name.
	/// </summary>
	string? ReplyTo { get; set; }

	/// <summary>
	/// Gets or sets the message expiration in milliseconds.
	/// </summary>
	string? Expiration { get; set; }

	/// <summary>
	/// Gets or sets the delivery mode (1 = non-persistent, 2 = persistent).
	/// </summary>
	byte? DeliveryMode { get; set; }

	/// <summary>
	/// Sets a custom header on the message.
	/// </summary>
	/// <param name="key">The header key.</param>
	/// <param name="value">The header value.</param>
	void SetHeader(string key, string value);
}
