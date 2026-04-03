// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// Read-only view of a registered handler entry, exposing the metadata consumers
/// need to inspect the handler registry without access to the mutable concrete type.
/// </summary>
public interface IHandlerRegistryEntry
{
	/// <summary>
	/// Gets the type of message that this handler can process.
	/// </summary>
	Type MessageType { get; }

	/// <summary>
	/// Gets the type of the handler implementation.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]
	Type HandlerType { get; }

	/// <summary>
	/// Gets a value indicating whether this handler returns a response after processing.
	/// </summary>
	bool ExpectsResponse { get; }
}
