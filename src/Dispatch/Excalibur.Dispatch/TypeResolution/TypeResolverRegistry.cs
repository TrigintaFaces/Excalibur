// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.TypeResolution;

/// <summary>
/// Static registry for type resolvers.
/// </summary>
/// <remarks>
/// This registry allows registration of type resolvers at runtime, breaking the circular dependency between Excalibur.Dispatch.Common and Excalibur.Dispatch.Messaging.
/// </remarks>
public static class TypeResolverRegistry
{
	private static readonly List<ITypeResolver> Resolvers = [];
#if NET9_0_OR_GREATER

	private static readonly Lock Lock = new();

#else

	private static readonly object Lock = new();

#endif

	/// <summary>
	/// Registers a type resolver.
	/// </summary>
	/// <param name="resolver"> The resolver to register. </param>
	public static void Register(ITypeResolver resolver)
	{
		ArgumentNullException.ThrowIfNull(resolver);

		lock (Lock)
		{
			if (!Resolvers.Contains(resolver))
			{
				Resolvers.Add(resolver);
			}
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
		lock (Lock)
		{
			foreach (var resolver in Resolvers)
			{
				if (resolver.TryGetType(typeName, out type))
				{
					return true;
				}
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
			Resolvers.Clear();
		}
	}
}
