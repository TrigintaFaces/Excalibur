// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Kafka partitioning strategy for CloudEvents.
/// </summary>
public enum KafkaPartitioningStrategy
{
	/// <summary>
	/// Use the correlation ID as partition key.
	/// </summary>
	CorrelationId = 0,

	/// <summary>
	/// Use the tenant ID as partition key.
	/// </summary>
	TenantId = 1,

	/// <summary>
	/// Use the user ID as partition key.
	/// </summary>
	UserId = 2,

	/// <summary>
	/// Use the CloudEvent source as partition key.
	/// </summary>
	Source = 3,

	/// <summary>
	/// Use the CloudEvent type as partition key.
	/// </summary>
	Type = 4,

	/// <summary>
	/// Use the CloudEvent ID as partition key.
	/// </summary>
	EventId = 5,

	/// <summary>
	/// Use round-robin partitioning.
	/// </summary>
	RoundRobin = 6,

	/// <summary>
	/// Use a custom partition key from CloudEvent extensions.
	/// </summary>
	Custom = 7,
}
