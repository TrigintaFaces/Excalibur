// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// RabbitMQ routing strategies for CloudEvents.
/// </summary>
public enum RabbitMqRoutingStrategy
{
	/// <summary>
	/// Use the CloudEvent type as routing key.
	/// </summary>
	EventType = 0,

	/// <summary>
	/// Use the CloudEvent source as routing key.
	/// </summary>
	Source = 1,

	/// <summary>
	/// Use the CloudEvent subject as routing key.
	/// </summary>
	Subject = 2,

	/// <summary>
	/// Use the correlation ID as routing key.
	/// </summary>
	CorrelationId = 3,

	/// <summary>
	/// Use the tenant ID as routing key.
	/// </summary>
	TenantId = 4,

	/// <summary>
	/// Use a combination of type and source as routing key.
	/// </summary>
	TypeAndSource = 5,

	/// <summary>
	/// Use a custom routing key from CloudEvent extensions.
	/// </summary>
	Custom = 6,

	/// <summary>
	/// Use a static routing key for all messages.
	/// </summary>
	Static = 7,
}
