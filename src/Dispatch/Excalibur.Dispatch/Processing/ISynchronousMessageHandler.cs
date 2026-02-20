// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Processing;

/// <summary>
/// Synchronous message handler interface for zero-allocation processing.
/// </summary>
public interface ISynchronousMessageHandler<TMessage>
	where TMessage : unmanaged
{
	/// <summary>
	/// Check if this handler can process the given message. This method should be extremely fast (ideally just checking a message type field).
	/// </summary>
	bool CanHandle(in TMessage message);

	/// <summary>
	/// Handle the message synchronously.
	/// </summary>
	HandlerResult Handle(in TMessage message, ulong messageId, Span<byte> workBuffer);
}
