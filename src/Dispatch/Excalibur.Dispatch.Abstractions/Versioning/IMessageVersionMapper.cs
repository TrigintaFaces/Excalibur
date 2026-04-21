// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Versioning;

/// <summary>
/// Defines the contract for mapping messages between schema versions.
/// </summary>
/// <remarks>
/// <para>
/// Implementations handle message schema evolution by transforming messages from one version to another.
/// This enables consumers to upgrade message schemas without requiring all producers to update simultaneously.
/// </para>
/// <para>
/// Register mappers in DI and the <c>MessageVersionMiddleware</c> will automatically
/// invoke them when a version mismatch is detected via the <c>x-message-version</c> transport header.
/// </para>
/// </remarks>
public interface IMessageVersionMapper
{
	/// <summary>
	/// Determines whether this mapper can transform a message between the specified versions.
	/// </summary>
	/// <param name="messageType">The message type identifier.</param>
	/// <param name="fromVersion">The source schema version.</param>
	/// <param name="toVersion">The target schema version.</param>
	/// <returns><see langword="true"/> if this mapper supports the specified version transformation.</returns>
	bool CanMap(string messageType, int fromVersion, int toVersion);

	/// <summary>
	/// Transforms a message from one schema version to another.
	/// </summary>
	/// <param name="message">The message payload to transform.</param>
	/// <param name="messageType">The message type identifier.</param>
	/// <param name="fromVersion">The source schema version.</param>
	/// <param name="toVersion">The target schema version.</param>
	/// <returns>The transformed message in the target schema version.</returns>
	object Map(object message, string messageType, int fromVersion, int toVersion);
}
