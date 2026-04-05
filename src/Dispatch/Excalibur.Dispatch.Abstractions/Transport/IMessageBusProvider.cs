// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Provides access to registered message bus instances, both local and remote.
/// </summary>
public interface IMessageBusProvider
{
	/// <summary>
	/// Gets a registered message bus by name.
	/// </summary>
	/// <param name="name"> The name of the message bus to retrieve. </param>
	/// <returns> The message bus instance if found; otherwise, null. </returns>
	IMessageBus? GetMessageBus(string name);

	/// <summary>
	/// Gets all registered message buses.
	/// </summary>
	/// <returns> A collection of all registered message bus instances. </returns>
	IEnumerable<IMessageBus> GetAllMessageBuses();

	/// <summary>
	/// Gets the names of all registered message buses.
	/// </summary>
	/// <returns> A collection of message bus names. </returns>
	IEnumerable<string> GetAllMessageBusNames();

	/// <summary>
	/// Gets a registered remote message bus by name.
	/// </summary>
	/// <param name="name"> The name of the remote message bus to retrieve. </param>
	/// <returns> The remote message bus instance if found; otherwise, null. </returns>
	IMessageBus? GetRemoteMessageBus(string name);

	/// <summary>
	/// Attempts to get a message bus by name.
	/// </summary>
	/// <param name="name"> The name of the message bus to retrieve. </param>
	/// <param name="bus"> When this method returns, contains the message bus instance if found; otherwise, null. </param>
	/// <returns> true if the message bus was found; otherwise, false. </returns>
	bool TryGet(string name, out IMessageBus? bus);
}
