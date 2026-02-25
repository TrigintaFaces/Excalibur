// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Provides mapping of outbox messages to transport-specific contexts.
/// </summary>
/// <remarks>
/// <para>
/// This interface bridges the outbox pattern with the message mapping system,
/// allowing outbound messages to be transformed according to transport-specific
/// configurations before being published.
/// </para>
/// </remarks>
public interface IOutboxMessageMapper
{
	/// <summary>
	/// Creates a transport message context from an outbound message.
	/// </summary>
	/// <param name="message">The outbound message from the outbox.</param>
	/// <param name="targetTransport">The target transport name.</param>
	/// <returns>A transport message context configured for the target transport.</returns>
	ITransportMessageContext CreateContext(OutboundMessage message, string targetTransport);

	/// <summary>
	/// Maps an outbound message to a transport-specific context using configured mappings.
	/// </summary>
	/// <param name="message">The outbound message from the outbox.</param>
	/// <param name="sourceContext">The source transport context.</param>
	/// <param name="targetTransport">The target transport name.</param>
	/// <returns>A transport message context mapped for the target transport.</returns>
	ITransportMessageContext MapToTransport(
		OutboundMessage message,
		ITransportMessageContext sourceContext,
		string targetTransport);

	/// <summary>
	/// Gets the target transports for a given message type.
	/// </summary>
	/// <param name="messageType">The fully qualified message type name.</param>
	/// <returns>A collection of target transport names, or empty if using default routing.</returns>
	IReadOnlyCollection<string> GetTargetTransports(string messageType);
}
