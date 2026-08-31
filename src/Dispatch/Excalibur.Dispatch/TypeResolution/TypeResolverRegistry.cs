// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

namespace Excalibur.Dispatch.TypeResolution;

/// <summary>
/// Static registry for type resolvers.
/// </summary>
/// <remarks>
/// This registry allows registration of type resolvers at runtime, breaking the circular dependency between Excalibur.Dispatch.Common and Excalibur.Dispatch.Messaging.
/// </remarks>
internal static class TypeResolverRegistry
{
	private static readonly Lock Lock = new();

	/// <summary>
	/// Successful resolutions, keyed by type name. Populated only on a hit, so the entry count is
	/// bounded by the set of types the registered resolvers can actually produce; an unresolvable
	/// name never allocates an entry.
	/// </summary>
	private static readonly ConcurrentDictionary<string, Type> Cache = new(StringComparer.Ordinal);

	/// <summary>
	/// The registered resolvers, replaced wholesale on every mutation. Readers take the reference once
	/// and walk a snapshot, so resolution never takes a lock and never blocks a concurrent registration.
	/// </summary>
	private static volatile ITypeResolver[] _resolvers = [];


	/// <summary>
	/// Registers a type resolver.
	/// </summary>
	/// <param name="resolver"> The resolver to register. </param>
	public static void Register(ITypeResolver resolver)
	{
		ArgumentNullException.ThrowIfNull(resolver);

		lock (Lock)
		{
			if (Array.IndexOf(_resolvers, resolver) >= 0)
			{
				return;
			}

			// Appended, so a later resolver can never shadow a name an earlier one already answered --
			// which is why an existing cached answer stays valid across a registration.
			_resolvers = [.. _resolvers, resolver];
		}
	}

	/// <summary>
	/// Tries to resolve a type using all registered resolvers.
	/// </summary>
	/// <param name="typeName"> The name of the type to resolve. </param>
	/// <param name="type"> The resolved type if found. </param>
	/// <returns> True if the type was resolved, false otherwise. </returns>
	public static bool TryResolveType(string typeName, out Type? type)
	{
		if (Cache.TryGetValue(typeName, out var cached))
		{
			type = cached;
			return true;
		}

		foreach (var resolver in _resolvers)
		{
			if (resolver.TryGetType(typeName, out type) && type is not null)
			{
				Cache[typeName] = type;
				return true;
			}
		}

		type = null;
		return false;
	}

	/// <summary>
	/// Clears all registered resolvers.
	/// </summary>
	/// <remarks> This method is primarily for testing purposes. </remarks>
	public static void Clear()
	{
		lock (Lock)
		{
			_resolvers = [];
			Cache.Clear();
		}
	}
}
