// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Specifies how the message ID is extracted for idempotency checking.
/// </summary>
public enum MessageIdStrategy
{
	/// <summary>
	/// Use the MessageId from the message header.
	/// </summary>
	FromHeader,

	/// <summary>
	/// Use the CorrelationId from the message context.
	/// </summary>
	FromCorrelationId,

	/// <summary>
	/// Use a composite key combining handler type and correlation ID: {HandlerType}:{CorrelationId}.
	/// </summary>
	CompositeKey,

	/// <summary>
	/// User provides a custom strategy via <see cref="IMessageIdProvider"/>.
	/// </summary>
	Custom,
}
