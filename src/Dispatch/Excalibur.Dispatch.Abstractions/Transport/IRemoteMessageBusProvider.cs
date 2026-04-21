// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Provides access to remote message bus instances.
/// Implementations that support remote buses should implement this interface
/// alongside <see cref="IMessageBusProvider"/>.
/// </summary>
public interface IRemoteMessageBusProvider
{
	/// <summary>Gets all registered remote message buses.</summary>
	IEnumerable<IMessageBus> GetAllRemoteMessageBuses();

	/// <summary>Gets the names of all registered remote message buses.</summary>
	IEnumerable<string> GetAllRemoteMessageBusNames();

	/// <summary>Attempts to get a remote message bus by name.</summary>
	bool TryGetRemote(string name, out IMessageBus? bus);
}
