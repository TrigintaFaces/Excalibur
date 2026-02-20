// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Initialization;

/// <summary>
/// Static helper methods for lazy initialization patterns.
/// </summary>
public static class LazyServiceInitializerFactory
{
	/// <summary>
	/// Creates a lazy initializer for a service.
	/// </summary>
	/// <typeparam name="TService"> The type of service to initialize. </typeparam>
	/// <param name="factory"> Factory function to create the service. </param>
	/// <returns> A lazy service initializer. </returns>
	public static LazyServiceInitializer<TService> Create<TService>(Func<TService> factory)
		where TService : class =>
		new(factory);

	/// <summary>
	/// Ensures that a value is initialized exactly once using double-check locking.
	/// </summary>
	/// <typeparam name="T"> The type of value to initialize. </typeparam>
	/// <param name="target"> Reference to the target field. </param>
	/// <param name="initialized"> Reference to the initialization flag. </param>
	/// <param name="syncLock"> Lock object for synchronization. </param>
	/// <param name="valueFactory"> Factory to create the value. </param>
	/// <returns> The initialized value. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static T EnsureInitialized<T>(
		ref T? target,
		ref bool initialized,
		ref object syncLock,
		Func<T> valueFactory)
		where T : class
	{
		ArgumentNullException.ThrowIfNull(valueFactory);

		// Fast path
		if (Volatile.Read(ref initialized))
		{
			return target!;
		}

		return EnsureInitializedCore(ref target, ref initialized, ref syncLock, valueFactory);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static T EnsureInitializedCore<T>(
		ref T? target,
		ref bool initialized,
		ref object syncLock,
		Func<T> valueFactory)
		where T : class
	{
		ArgumentNullException.ThrowIfNull(valueFactory);
		lock (syncLock)
		{
			if (!Volatile.Read(ref initialized))
			{
				target = valueFactory();
				Volatile.Write(ref initialized, value: true);
			}
		}

		return target!;
	}
}
