// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Interface for transport provider implementations.
/// </summary>
public interface ITransportProvider
{
	/// <summary>
	/// Gets the name of the transport provider.
	/// </summary>
	string Name { get; }

	/// <summary>
	/// Gets the transport type identifier.
	/// </summary>
	string TransportType { get; }

	/// <summary>
	/// Gets the version of the transport provider.
	/// </summary>
	string Version { get; }

	/// <summary>
	/// Gets the capabilities of this transport provider.
	/// </summary>
	TransportCapabilities Capabilities { get; }

	/// <summary>
	/// Gets a value indicating whether this provider is available in the current environment.
	/// </summary>
	bool IsAvailable { get; }

}
