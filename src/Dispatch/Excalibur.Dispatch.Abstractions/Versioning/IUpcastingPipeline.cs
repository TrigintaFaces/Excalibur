// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Manages automatic message version migration for ALL message types using registered upcasters.
/// </summary>
/// <remarks>
/// <para>
/// The pipeline builds a directed graph of version transformations and uses
/// breadth-first search to find shortest paths between any two versions.
/// Paths are computed once and cached for O(1) lookup performance.
/// </para>
/// <para>
/// <b>Supports all message types:</b>
/// <list type="bullet">
/// <item><description><see cref="IDomainEvent"/> - event sourcing</description></item>
/// <item><description><see cref="ICommand"/> - API versioning</description></item>
/// <item><description><see cref="IQuery{TResult}"/> - read model evolution</description></item>
/// <item><description><see cref="IIntegrationEvent"/> - external system compatibility</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Thread Safety:</b> Implementations must be thread-safe for concurrent reads
/// and safe for concurrent registration during application startup.
/// </para>
/// </remarks>
public interface IUpcastingPipeline
{
	/// <summary>
	/// Upcasts a message to the latest registered version for its type.
	/// </summary>
	/// <param name="message">The message to upcast (any version, any type).</param>
	/// <returns>The message upcasted to the latest version, or the original if already latest.</returns>
	/// <remarks>
	/// If no upcasters are registered for the message type, returns the original message unchanged.
	/// </remarks>
	IDispatchMessage Upcast(IDispatchMessage message);

	/// <summary>
	/// Upcasts a message to a specific target version.
	/// </summary>
	/// <param name="message">The message to upcast.</param>
	/// <param name="targetVersion">The desired version.</param>
	/// <returns>The message at the target version.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when no path exists to the target version, or when attempting to downcast
	/// (target version is lower than source version).
	/// </exception>
	IDispatchMessage UpcastTo(IDispatchMessage message, int targetVersion);

	/// <summary>
	/// Registers a generic message upcaster for a specific version transition.
	/// </summary>
	/// <typeparam name="TOld">The old message type.</typeparam>
	/// <typeparam name="TNew">The new message type.</typeparam>
	/// <param name="upcaster">The upcaster instance.</param>
	/// <remarks>
	/// <para>
	/// Registration should typically happen during application startup via dependency injection.
	/// Multiple upcasters can be registered to form a chain (v1→v2→v3→v4).
	/// </para>
	/// <para>
	/// After registration, paths are automatically computed using BFS.
	/// </para>
	/// </remarks>
	void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TOld, TNew>(IMessageUpcaster<TOld, TNew> upcaster)
		where TOld : IDispatchMessage, IVersionedMessage
		where TNew : IDispatchMessage, IVersionedMessage;

	/// <summary>
	/// Checks if a path exists between two versions for a message type.
	/// </summary>
	/// <param name="messageType">The logical message type name.</param>
	/// <param name="fromVersion">The source version.</param>
	/// <param name="toVersion">The target version.</param>
	/// <returns>True if a path exists (including multi-hop); otherwise false.</returns>
	/// <remarks>
	/// This method can be used to validate upcasting capability before attempting
	/// the operation, or to provide diagnostic information about registered paths.
	/// </remarks>
	bool CanUpcast(string messageType, int fromVersion, int toVersion);

	/// <summary>
	/// Gets the latest registered version for a message type.
	/// </summary>
	/// <param name="messageType">The logical message type name.</param>
	/// <returns>The highest version number, or 0 if no versions registered.</returns>
	int GetLatestVersion(string messageType);
}
