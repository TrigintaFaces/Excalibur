// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.A3.Authorization;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.A3;

/// <summary>
/// Builder for configuring A3 authorization store providers.
/// </summary>
/// <remarks>
/// Follows the Microsoft ASP.NET Core Identity <c>IdentityBuilder</c> pattern:
/// a builder returned from <c>AddExcaliburA3()</c> that exposes <c>Use*()</c>
/// methods for registering store implementations.
/// </remarks>
public interface IA3Builder
{
	/// <summary>
	/// Gets the underlying service collection.
	/// </summary>
	/// <value>The service collection being configured.</value>
	IServiceCollection Services { get; }

	/// <summary>
	/// Registers a custom grant store implementation.
	/// </summary>
	/// <typeparam name="TStore">The grant store implementation type.</typeparam>
	/// <returns>The builder for chaining.</returns>
	IA3Builder UseGrantStore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TStore>() where TStore : class, IGrantStore;

	/// <summary>
	/// Registers a custom activity group store implementation.
	/// </summary>
	/// <typeparam name="TStore">The activity group store implementation type.</typeparam>
	/// <returns>The builder for chaining.</returns>
	IA3Builder UseActivityGroupStore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TStore>() where TStore : class, IActivityGroupStore;

	/// <summary>
	/// Requires the configured grant store to survive a process restart, refusing to start if it does not.
	/// </summary>
	/// <returns>The builder for chaining.</returns>
	/// <remarks>
	/// <para>
	/// <b>Opt-in, and it must be called to have any effect.</b> Without it a host that falls back to the
	/// in-memory grant store starts normally and loses every grant on restart — which surfaces as a silent
	/// deny-all rather than an error, because a store that has forgotten its grants is indistinguishable
	/// from one whose grants were never made.
	/// </para>
	/// <para>
	/// Call it when grants must outlive the process. A host that has deliberately accepted a volatile store
	/// sets <c>GrantDurabilityOptions.AllowVolatileGrantStore</c> instead; the two together mean "I know,
	/// and I accept it", which is a different statement from never having asked.
	/// </para>
	/// <para>
	/// The verb lives on the builder rather than as a separate service-collection extension because the
	/// builder already models this package's configuration; a parallel registration surface for one option
	/// would be a second way to say the same thing.
	/// </para>
	/// </remarks>
	IA3Builder RequireDurableGrants();
}
