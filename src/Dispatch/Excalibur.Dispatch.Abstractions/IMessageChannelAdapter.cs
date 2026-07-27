// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// Defines a contract for adapting message channels to a common interface.
/// </summary>
/// <remarks>
/// This composite aggregates the focused send, receive, acknowledge, and connection contracts.
/// Consumers that only need one capability can depend on the narrow interface
/// (<see cref="IMessageChannelSender{TMessage}" />, <see cref="IMessageChannelReceiver{TMessage}" />,
/// <see cref="IMessageChannelAcknowledger{TMessage}" />, or <see cref="IMessageChannelConnection" />);
/// implementers of a full adapter continue to implement this aggregate.
/// </remarks>
/// <typeparam name="TMessage"> The type of message handled by the adapter. </typeparam>
public interface IMessageChannelAdapter<TMessage> :
	IMessageChannelSender<TMessage>,
	IMessageChannelReceiver<TMessage>,
	IMessageChannelAcknowledger<TMessage>,
	IMessageChannelConnection
	where TMessage : class
{
}
