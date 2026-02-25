// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Routing;

/// <summary>
/// Interface for message routes.
/// </summary>
public interface IMessageRoute<in TMessage>
{
	/// <summary>
	/// Gets the message type this route handles.
	/// </summary>
	/// <value>
	/// The message type this route handles.
	/// </value>
	string MessageType { get; }

	/// <summary>
	/// Determines if this route can handle the message.
	/// </summary>
	bool CanRoute(TMessage message);

	/// <summary>
	/// Routes the message.
	/// </summary>
	ValueTask RouteAsync(TMessage message, CancellationToken cancellationToken);
}
