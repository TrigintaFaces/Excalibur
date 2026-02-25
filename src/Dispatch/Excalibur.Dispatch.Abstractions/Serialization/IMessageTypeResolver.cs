// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Serialization;

/// <summary>
/// Service interface for resolving message types during deserialization.
/// </summary>
/// <remarks>
/// <para>
/// The IMessageTypeResolver is responsible for mapping type identifiers (typically from
/// message metadata or headers) to actual .NET types during message deserialization.
/// This enables polymorphic message handling and type-safe deserialization in distributed
/// messaging scenarios.
/// </para>
/// <para>
/// Type resolution is fundamentally an in-memory lookup operation, so this interface
/// uses synchronous methods. Async convenience wrappers are available as extension
/// methods in <see cref="MessageTypeResolverExtensions"/>.
/// </para>
/// </remarks>
public interface IMessageTypeResolver
{
	/// <summary>
	/// Resolves a .NET type from a type identifier string.
	/// </summary>
	/// <param name="typeIdentifier">The type identifier from the message metadata.</param>
	/// <returns>The resolved .NET type, or null if the type cannot be resolved.</returns>
	Type? ResolveType(string typeIdentifier);

	/// <summary>
	/// Gets the type identifier for a given .NET type.
	/// </summary>
	/// <param name="messageType">The .NET type to get an identifier for.</param>
	/// <returns>The type identifier string for the given type.</returns>
	string GetTypeIdentifier(Type messageType);

	/// <summary>
	/// Registers a type with the resolver for future resolution.
	/// </summary>
	/// <param name="messageType">The .NET type to register.</param>
	/// <param name="typeIdentifier">The type identifier to associate with the type.</param>
	void RegisterType(Type messageType, string typeIdentifier);
}
