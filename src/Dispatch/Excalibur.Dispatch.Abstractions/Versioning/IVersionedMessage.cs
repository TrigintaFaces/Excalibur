// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Marker interface for messages that support explicit versioning.
/// </summary>
/// <remarks>
/// Any message type implementing this interface can be automatically upcasted
/// through the IUpcastingPipeline when older versions are encountered.
/// This includes domain events, commands, queries, and integration events.
/// </remarks>
public interface IVersionedMessage
{
	/// <summary>
	/// Gets the schema version of this message.
	/// </summary>
	/// <remarks>
	/// Version numbers should start at 1 and increment sequentially.
	/// Breaking changes require a version increment.
	/// </remarks>
	int Version { get; }

	/// <summary>
	/// Gets the logical message type name (version-independent).
	/// </summary>
	/// <remarks>
	/// This should remain constant across versions.
	/// Examples:
	/// <list type="bullet">
	/// <item><description>"UserCreatedEvent" for UserCreatedEventV1, V2, V3</description></item>
	/// <item><description>"CreateUserCommand" for CreateUserCommandV1, V2, V3</description></item>
	/// <item><description>"GetUserQuery" for GetUserQueryV1, V2, V3</description></item>
	/// </list>
	/// </remarks>
	string MessageType { get; }
}
