// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.ErrorHandling;

/// <summary>
/// Specifies the reason why a message was moved to the dead letter queue.
/// </summary>
public enum DeadLetterReason
{
	/// <summary>
	/// Message exceeded the maximum number of retry attempts.
	/// </summary>
	MaxRetriesExceeded = 0,

	/// <summary>
	/// Circuit breaker was open, preventing message delivery.
	/// </summary>
	CircuitBreakerOpen = 1,

	/// <summary>
	/// Message could not be deserialized.
	/// </summary>
	DeserializationFailed = 2,

	/// <summary>
	/// No handler was found for the message type.
	/// </summary>
	HandlerNotFound = 3,

	/// <summary>
	/// Message failed validation.
	/// </summary>
	ValidationFailed = 4,

	/// <summary>
	/// Message was manually rejected by a handler.
	/// </summary>
	ManualRejection = 5,

	/// <summary>
	/// Message expired before it could be processed.
	/// </summary>
	MessageExpired = 6,

	/// <summary>
	/// Authorization failed for the message.
	/// </summary>
	AuthorizationFailed = 7,

	/// <summary>
	/// Unhandled exception occurred during processing.
	/// </summary>
	UnhandledException = 8,

	/// <summary>
	/// Message was poisoned (repeatedly causing failures).
	/// </summary>
	PoisonMessage = 9,

	/// <summary>
	/// Unknown or unspecified reason.
	/// </summary>
	Unknown = 99,
}
