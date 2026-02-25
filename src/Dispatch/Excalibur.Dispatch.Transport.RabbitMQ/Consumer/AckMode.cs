// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Specifies the acknowledgment mode for RabbitMQ message consumers.
/// </summary>
public enum AckMode
{
	/// <summary>
	/// Messages are acknowledged automatically by RabbitMQ upon delivery.
	/// Use with caution as messages may be lost if processing fails.
	/// </summary>
	Auto = 0,

	/// <summary>
	/// Messages must be explicitly acknowledged or rejected by the consumer.
	/// Provides the highest level of delivery guarantee.
	/// </summary>
	Manual = 1,

	/// <summary>
	/// Messages are acknowledged in batches for improved throughput.
	/// Batch size and timeout are configurable via <see cref="RabbitMqConsumerOptions.BatchAckSize"/>
	/// and <see cref="RabbitMqConsumerOptions.BatchAckTimeout"/>.
	/// </summary>
	Batch = 2,
}
