// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Serialization;

/// <summary>
/// Extension methods for <see cref="ISerializerRegistry"/>.
/// </summary>
/// <remarks>
/// Provides convenience query methods that delegate to the core
/// <see cref="ISerializerRegistry"/> methods.
/// </remarks>
public static class SerializerRegistryExtensions
{
	/// <summary>
	/// Gets a serializer by its name.
	/// </summary>
	/// <param name="registry">The serializer registry.</param>
	/// <param name="name">The serializer name.</param>
	/// <returns>The serializer, or null if not registered.</returns>
	/// <exception cref="ArgumentNullException">Thrown when registry is null.</exception>
	/// <exception cref="ArgumentException">Thrown when name is null or whitespace.</exception>
	public static IPluggableSerializer? GetByName(this ISerializerRegistry registry, string name)
	{
		ArgumentNullException.ThrowIfNull(registry);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		foreach (var entry in registry.GetAll())
		{
			if (string.Equals(entry.Name, name, StringComparison.Ordinal))
			{
				return entry.Serializer;
			}
		}

		return null;
	}

	/// <summary>
	/// Checks whether a serializer with the specified ID is registered.
	/// </summary>
	/// <param name="registry">The serializer registry.</param>
	/// <param name="id">The serializer ID to check.</param>
	/// <returns>True if a serializer with the specified ID is registered, false otherwise.</returns>
	/// <exception cref="ArgumentNullException">Thrown when registry is null.</exception>
	public static bool IsRegistered(this ISerializerRegistry registry, byte id)
	{
		ArgumentNullException.ThrowIfNull(registry);
		return registry.GetById(id) is not null;
	}

	/// <summary>
	/// Checks whether a serializer with the specified name is registered.
	/// </summary>
	/// <param name="registry">The serializer registry.</param>
	/// <param name="name">The serializer name to check.</param>
	/// <returns>True if a serializer with the specified name is registered, false otherwise.</returns>
	/// <exception cref="ArgumentNullException">Thrown when registry is null.</exception>
	public static bool IsRegistered(this ISerializerRegistry registry, string name)
	{
		ArgumentNullException.ThrowIfNull(registry);

		if (string.IsNullOrWhiteSpace(name))
		{
			return false;
		}

		return registry.GetByName(name) is not null;
	}
}
