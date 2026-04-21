// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3;
using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Authorization.Stores.InMemory;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering lightweight A3 core services.
/// </summary>
public static class A3CoreServiceCollectionExtensions
{
	/// <summary>
	/// Adds core A3 authorization services with in-memory stores only.
	/// No CQRS pipeline, no Dispatch middleware, no external HTTP services.
	/// </summary>
	/// <param name="services">The service collection to add core A3 services to.</param>
	/// <returns>An <see cref="IA3Builder"/> for configuring store providers.</returns>
	/// <remarks>
	/// <para>
	/// Use this entry point for standalone A3 consumption where only grant management
	/// and authorization evaluation are needed, without the full Dispatch pipeline:
	/// </para>
	/// <code>
	/// services.AddExcaliburA3Core();
	/// // Stores are in-memory by default. Override with:
	/// // services.AddExcaliburA3Core().UseGrantStore&lt;MyStore&gt;();
	/// </code>
	/// <para>
	/// For the full-stack experience with CQRS commands, Dispatch middleware,
	/// and authentication services, use <c>Excalibur.A3</c> with
	/// <c>AddExcaliburA3()</c> instead.
	/// </para>
	/// </remarks>
	public static IA3Builder AddExcaliburA3Core(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Fallback in-memory stores: used when no persistent provider is registered.
		services.TryAddSingleton<IGrantStore, InMemoryGrantStore>();
		services.TryAddSingleton<IActivityGroupStore, InMemoryActivityGroupStore>();

		return new A3Builder(services);
	}
}
