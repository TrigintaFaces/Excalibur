// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.A3.Abstractions.Authorization;

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
}
