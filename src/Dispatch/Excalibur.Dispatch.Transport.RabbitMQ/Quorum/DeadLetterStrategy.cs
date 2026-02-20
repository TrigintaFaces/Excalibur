// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Specifies the dead letter strategy for RabbitMQ quorum queues.
/// </summary>
/// <remarks>
/// Quorum queues support two dead letter strategies that control how messages
/// are forwarded to the dead letter exchange.
/// </remarks>
public enum DeadLetterStrategy
{
	/// <summary>
	/// Messages are dead-lettered at most once. If the dead letter exchange
	/// is unavailable, the message is dropped. This is the default strategy.
	/// </summary>
	AtMostOnce = 0,

	/// <summary>
	/// Messages are dead-lettered at least once using a safer mechanism.
	/// Requires the <c>x-dead-letter-strategy</c> queue argument to be set to <c>at-least-once</c>.
	/// This provides stronger guarantees but may result in duplicate dead-lettered messages.
	/// </summary>
	AtLeastOnce = 1,
}
