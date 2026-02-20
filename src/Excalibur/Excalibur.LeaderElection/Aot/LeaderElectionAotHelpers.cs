// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Diagnostics;
using Excalibur.LeaderElection.Health;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// AOT-friendly registration helpers for leader election services.
/// Provides explicit generic registration methods that avoid reflection-based type discovery,
/// ensuring compatibility with Native AOT trimming and ahead-of-time compilation.
/// </summary>
/// <remarks>
/// <para>
/// These methods use explicit <c>AddSingleton&lt;TService, TImplementation&gt;()</c> registrations
/// instead of assembly-scanning or reflection-based registration, which are not safe for trimming.
/// </para>
/// <para>
/// Reference: Follows the pattern established by <c>Microsoft.Extensions.DependencyInjection</c>
/// where AOT-compatible APIs use concrete type parameters rather than <c>Type</c> objects.
/// </para>
/// </remarks>
public static class LeaderElectionAotHelpers
{
	/// <summary>
	/// Registers a specific <see cref="ILeaderElection"/> implementation in an AOT-safe manner.
	/// </summary>
	/// <typeparam name="TImplementation">The concrete leader election implementation type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddLeaderElection<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
		this IServiceCollection services)
		where TImplementation : class, ILeaderElection
	{
		ArgumentNullException.ThrowIfNull(services);

		services.AddSingleton<ILeaderElection, TImplementation>();
		return services;
	}

	/// <summary>
	/// Registers a specific <see cref="ILeaderElectionFactory"/> implementation in an AOT-safe manner.
	/// </summary>
	/// <typeparam name="TImplementation">The concrete leader election factory implementation type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddLeaderElectionFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
		this IServiceCollection services)
		where TImplementation : class, ILeaderElectionFactory
	{
		ArgumentNullException.ThrowIfNull(services);

		services.AddSingleton<ILeaderElectionFactory, TImplementation>();
		return services;
	}

	/// <summary>
	/// Registers the telemetry-instrumented leader election factory in an AOT-safe manner.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddLeaderElectionTelemetry(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// TelemetryLeaderElectionFactory wraps the existing ILeaderElectionFactory
		// via constructor injection, so it must be registered as a decorator.
		services.AddSingleton<TelemetryLeaderElectionFactory>();
		return services;
	}

	/// <summary>
	/// Registers the leader election health check in an AOT-safe manner.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddLeaderElectionHealthCheck(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.AddSingleton<LeaderElectionHealthCheck>();
		return services;
	}
}
