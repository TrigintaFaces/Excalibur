// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Context for tracking outbound messages during processing.
/// </summary>
/// <remarks> Creates a new outbox context. </remarks>
public sealed class OutboxContext(
	string? correlationId,
	string? causationId,
	string? tenantId,
	string sourceMessageType)
{
	private readonly List<OutboundMessageRequest> _outboundMessages = [];

	/// <summary>
	/// Gets the correlation identifier.
	/// </summary>
	/// <value>The current <see cref="CorrelationId"/> value.</value>
	public string? CorrelationId { get; } = correlationId;

	/// <summary>
	/// Gets the causation identifier.
	/// </summary>
	/// <value>The current <see cref="CausationId"/> value.</value>
	public string? CausationId { get; } = causationId;

	/// <summary>
	/// Gets the tenant identifier.
	/// </summary>
	/// <value>The current <see cref="TenantId"/> value.</value>
	public string? TenantId { get; } = tenantId;

	/// <summary>
	/// Gets the source message type name.
	/// </summary>
	/// <value>
	/// The source message type name.
	/// </value>
	public string SourceMessageType { get; } = sourceMessageType ?? throw new ArgumentNullException(nameof(sourceMessageType));

	/// <summary>
	/// Gets the list of outbound messages to be staged.
	/// </summary>
	/// <value>
	/// The list of outbound messages to be staged.
	/// </value>
	public IReadOnlyList<OutboundMessageRequest> OutboundMessages => _outboundMessages.AsReadOnly();

	/// <summary>
	/// Adds an outbound message to be staged in the outbox.
	/// </summary>
	/// <param name="message"> The message to stage. </param>
	/// <param name="destination"> Optional destination for the message. </param>
	/// <param name="scheduledAt"> Optional scheduled delivery time. </param>
	public void AddOutboundMessage(
		IDispatchMessage message,
		string? destination = null,
		DateTimeOffset? scheduledAt = null)
	{
		ArgumentNullException.ThrowIfNull(message);

		_outboundMessages.Add(new OutboundMessageRequest(message, destination, scheduledAt));
	}
}
