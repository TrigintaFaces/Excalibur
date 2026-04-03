// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

namespace Excalibur.Caching.Projections;

/// <summary>
/// AOT-safe static registry for pre-registered <see cref="IProjectionTagResolver{T}"/> types,
/// eliminating the need for <see cref="Type.MakeGenericType"/> at runtime.
/// </summary>
/// <remarks>
/// <para>
/// Populated at DI registration time. When a projection tag resolver is registered,
/// the closed generic type <c>IProjectionTagResolver&lt;TMessage&gt;</c> is stored here
/// keyed by <c>typeof(TMessage)</c>.
/// </para>
/// <para>
/// Registration happens at DI composition time only. Call <see cref="Freeze"/>
/// after build to prevent accidental runtime registration.
/// </para>
/// </remarks>
internal static class ProjectionResolverTypeRegistry
{
	private static readonly ConcurrentDictionary<Type, Type> ResolverTypes = new();
	private static volatile bool _frozen;

	/// <summary>
	/// Gets the pre-registered <c>IProjectionTagResolver&lt;TMessage&gt;</c> type for the given message type.
	/// </summary>
	/// <param name="messageType">The message type.</param>
	/// <returns>The closed generic resolver type, or <see langword="null"/> if not registered.</returns>
	internal static Type? GetResolverType(Type messageType)
		=> ResolverTypes.GetValueOrDefault(messageType);

	/// <summary>
	/// Registers the resolver type for a message type.
	/// </summary>
	/// <typeparam name="TMessage">The message type.</typeparam>
	internal static void Register<TMessage>()
		where TMessage : class
	{
		if (_frozen)
		{
			throw new InvalidOperationException(
				$"Cannot register projection resolver type for '{typeof(TMessage).Name}' " +
				"after the registry has been frozen.");
		}

		ResolverTypes[typeof(TMessage)] = typeof(IProjectionTagResolver<TMessage>);
	}

	/// <summary>
	/// Freezes the registry, preventing further registrations.
	/// </summary>
	internal static void Freeze() => _frozen = true;

	/// <summary>
	/// Clears the registry. Intended for testing only.
	/// </summary>
	internal static void Clear()
	{
		_frozen = false;
		ResolverTypes.Clear();
	}
}
