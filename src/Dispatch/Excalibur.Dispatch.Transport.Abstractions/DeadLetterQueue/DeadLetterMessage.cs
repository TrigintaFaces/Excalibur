// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json.Serialization;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Represents a message in the dead letter queue.
/// </summary>
public sealed class DeadLetterMessage
{
	/// <summary>
	/// Gets or sets the original message.
	/// </summary>
	/// <value>The current <see cref="OriginalMessage"/> value.</value>
	public TransportMessage OriginalMessage { get; set; } = null!;

	/// <summary>
	/// Gets or sets the original message envelope (needed for acknowledgment).
	/// </summary>
	/// <value>The current <see cref="OriginalEnvelope"/> value.</value>
	/// <remarks>
	/// This property is excluded from JSON serialization because MessageEnvelope contains runtime-only
	/// data (IValidationResult, IAuthorizationResult, IRouteResult) that should not be persisted.
	/// </remarks>
	[JsonIgnore]
	public MessageEnvelope? OriginalEnvelope { get; set; }

	/// <summary>
	/// Gets or sets the original message context.
	/// </summary>
	/// <value>The current <see cref="OriginalContext"/> value.</value>
	public IMessageContext? OriginalContext { get; set; }

	/// <summary>
	/// Gets or sets the reason for dead lettering.
	/// </summary>
	/// <value>The current <see cref="Reason"/> value.</value>
	public string Reason { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the exception that caused the failure.
	/// </summary>
	/// <value>The current <see cref="Exception"/> value.</value>
	public Exception? Exception { get; set; }

	/// <summary>
	/// Gets or sets the number of delivery attempts.
	/// </summary>
	/// <value>The current <see cref="DeliveryAttempts"/> value.</value>
	public int DeliveryAttempts { get; set; }

	/// <summary>
	/// Gets or sets the original source queue/topic.
	/// </summary>
	/// <value>The current <see cref="OriginalSource"/> value.</value>
	public string? OriginalSource { get; set; }

	/// <summary>
	/// Gets or sets when the message was dead lettered.
	/// </summary>
	/// <value>The current <see cref="DeadLetteredAt"/> value.</value>
	public DateTimeOffset DeadLetteredAt { get; set; }

	/// <summary>
	/// Gets additional metadata.
	/// </summary>
	/// <value>The current <see cref="Metadata"/> value.</value>
	public Dictionary<string, string> Metadata { get; init; } = [];
}
