// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// AOT-safe registry that maps message types to their <see cref="IResultCachePolicy{TMessage}"/> delegates.
/// Populated at DI composition time via <see cref="CachePolicyRegistryPopulator"/>, eliminating the need
/// for <see cref="Type.MakeGenericType"/> at runtime.
/// </summary>
/// <remarks>
/// <para>
/// This follows the Explicit-Generic-DI Registry pattern established in Excalibur.Saga.
/// During DI composition, each <c>AddCachePolicy&lt;TMessage, TPolicy&gt;()</c> call accumulates a
/// typed registration action. On first <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/>
/// resolution, the <see cref="CachePolicyRegistryPopulator"/> drains the accumulated actions and
/// freezes the registry.
/// </para>
/// </remarks>
internal sealed class CachePolicyRegistry
{
	private readonly ConcurrentDictionary<Type, Func<IServiceProvider, IDispatchMessage, object?, bool>> _policies = new();
	private volatile bool _frozen;

	/// <summary>
	/// Registers a cache policy delegate for a specific message type.
	/// </summary>
	/// <param name="messageType">The message type to register a policy for.</param>
	/// <param name="shouldCacheDelegate">
	/// A delegate that resolves the policy from DI and invokes <c>ShouldCache</c>.
	/// </param>
	/// <exception cref="InvalidOperationException">Thrown if the registry has been frozen.</exception>
	internal void Register(Type messageType, Func<IServiceProvider, IDispatchMessage, object?, bool> shouldCacheDelegate)
	{
		if (_frozen)
		{
			throw new InvalidOperationException(
				$"Cannot register cache policy for '{messageType.Name}' after registry is frozen.");
		}

		_policies[messageType] = shouldCacheDelegate;
	}

	/// <summary>
	/// Gets the cache policy delegate for a message type, or <see langword="null"/> if none is registered.
	/// </summary>
	/// <param name="messageType">The message type to look up.</param>
	/// <returns>The policy delegate, or <see langword="null"/>.</returns>
	internal Func<IServiceProvider, IDispatchMessage, object?, bool>? GetPolicy(Type messageType)
		=> _policies.GetValueOrDefault(messageType);

	/// <summary>
	/// Freezes the registry, preventing further registrations.
	/// </summary>
	internal void Freeze() => _frozen = true;
}
