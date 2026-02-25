// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Pool for managing message envelopes to avoid allocations.
/// </summary>
public interface IMessageEnvelopePool
{
	/// <summary>
	/// Rents an envelope for the specified message.
	/// </summary>
	MessageEnvelopeHandle<TMessage> Rent<TMessage>(TMessage message, in MessageMetadata metadata)
		where TMessage : class, IDispatchMessage;

	/// <summary>
	/// Rents an envelope with a full context.
	/// </summary>
	MessageEnvelopeHandle<TMessage> RentWithContext<TMessage>(TMessage message, IMessageContext context)
		where TMessage : class, IDispatchMessage;

	/// <summary>
	/// Gets pool statistics.
	/// </summary>
	MessageEnvelopePoolStats GetStats();
}
