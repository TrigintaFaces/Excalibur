// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Abstractions.Messaging;

/// <summary>
/// Defines the contract for message type registration and resolution,
/// enabling type lookups for serialization and deserialization scenarios.
/// </summary>
public interface IMessageTypeRegistry
{
	/// <summary>
	/// Attempts to retrieve a Type from its name.
	/// </summary>
	/// <param name="typeName">The name of the type to retrieve.</param>
	/// <param name="type">The resolved type if found; otherwise, null.</param>
	/// <returns>true if the type was found; otherwise, false.</returns>
	bool TryGetType(string typeName, [NotNullWhen(true)] out Type? type);

	/// <summary>
	/// Retrieves a Type from its name.
	/// </summary>
	/// <param name="typeName">The name of the type to retrieve.</param>
	/// <returns>The resolved type.</returns>
	/// <exception cref="TypeLoadException">Thrown when the type cannot be found.</exception>
	Type GetType(string typeName);

	/// <summary>
	/// Retrieves all registered message types.
	/// </summary>
	/// <returns>An enumerable of all registered types.</returns>
	IEnumerable<Type> GetAllMessageTypes();

	/// <summary>
	/// Registers a type in the registry.
	/// </summary>
	/// <param name="type">The type to register.</param>
	void RegisterType(Type type);

	/// <summary>
	/// Registers a type in the registry (generic version).
	/// </summary>
	/// <typeparam name="T">The type to register.</typeparam>
	void RegisterType<T>() where T : IDispatchMessage;
}
