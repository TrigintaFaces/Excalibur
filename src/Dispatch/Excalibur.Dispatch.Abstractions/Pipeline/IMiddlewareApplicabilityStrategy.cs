// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Strategy for determining whether middleware should be applied to a specific message.
/// </summary>
public interface IMiddlewareApplicabilityStrategy
{
	/// <summary>
	/// Determines the message kinds for a given message.
	/// </summary>
	/// <typeparam name="T"> The message type. </typeparam>
	/// <param name="message"> The message to inspect. </param>
	/// <returns> The message kinds that apply to this message. </returns>
	MessageKinds DetermineMessageKinds<T>(T message)
		where T : IDispatchMessage;

	/// <summary>
	/// Determines whether middleware with the specified applicable kinds should process a message.
	/// </summary>
	/// <param name="applicableKinds"> The kinds of messages the middleware can process. </param>
	/// <param name="messageKinds"> The kinds of the current message. </param>
	/// <returns> True if the middleware should process the message; otherwise, false. </returns>
	bool ShouldApplyMiddleware(MessageKinds applicableKinds, MessageKinds messageKinds);
}
