// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// RabbitMQ exchange types for CloudEvents.
/// </summary>
public enum RabbitMqExchangeType
{
	/// <summary>
	/// Direct exchange - routes based on exact routing key match.
	/// </summary>
	Direct = 0,

	/// <summary>
	/// Topic exchange - routes based on routing key patterns with wildcards.
	/// </summary>
	Topic = 1,

	/// <summary>
	/// Fanout exchange - routes to all bound queues regardless of routing key.
	/// </summary>
	Fanout = 2,

	/// <summary>
	/// Headers exchange - routes based on message header values.
	/// </summary>
	Headers = 3,
}
