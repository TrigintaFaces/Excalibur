// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines the contract for a registry that resolves projection event handlers.
/// </summary>
public interface IProjectionEventHandlerRegistry
{
	/// <summary>
	/// Resolves a projection handler for the specified event type and key type.
	/// </summary>
	/// <typeparam name="TKey"> The type of the aggregate key. </typeparam>
	/// <param name="eventType"> The type of event to find a handler for. </param>
	/// <returns> The projection handler if found; otherwise, null. </returns>
	IProjectionHandler? ResolveHandler<TKey>(string eventType);
}
