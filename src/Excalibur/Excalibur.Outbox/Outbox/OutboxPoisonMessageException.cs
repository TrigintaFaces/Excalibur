// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Raised when a staged outbox row cannot be decoded into a dispatchable message.
/// </summary>
/// <remarks>
/// Distinct from a delivery failure on purpose. A row this process cannot decode will not decode on the
/// next attempt either, and it never reached a transport -- so it is evidence about the row, not about
/// transport health. Keeping it in its own type lets the drain dead-letter the row immediately without
/// charging the failure to the transport's circuit breaker, which would otherwise let a single corrupt
/// row open the circuit and stall delivery of every healthy message behind it.
/// </remarks>
[SuppressMessage(
	"Design",
	"CA1064:Exceptions should be public",
	Justification = "Internal control-flow signal that separates a corrupt row from a delivery failure; " +
		"it is caught by the drain that raises it and never escapes to a consumer.")]
internal sealed class OutboxPoisonMessageException : Exception
{
	public OutboxPoisonMessageException()
	{
	}

	public OutboxPoisonMessageException(string message)
		: base(message)
	{
	}

	public OutboxPoisonMessageException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	public OutboxPoisonMessageException(string messageId, string reason, Exception innerException)
		: base($"Outbox message '{messageId}' could not be decoded for dispatch: {reason}", innerException) =>
		MessageId = messageId;

	/// <summary>
	/// Gets the identifier of the outbox row that could not be decoded.
	/// </summary>
	public string? MessageId { get; }
}
