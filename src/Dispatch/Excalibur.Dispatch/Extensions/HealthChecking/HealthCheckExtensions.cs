// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Dispatch.Extensions.HealthChecking;

/// <summary>
/// Provides extension methods for health check registration.
/// </summary>
public static class HealthCheckExtensions
{
	/// <summary>
	/// Adds a health check with the specified name and tags.
	/// </summary>
	/// <typeparam name="THealthCheck"> The health check implementation type. </typeparam>
	/// <param name="builder"> The health checks builder. </param>
	/// <param name="name"> The health check name. </param>
	/// <param name="failureStatus"> The failure status to report. </param>
	/// <param name="tags"> Optional tags for the health check. </param>
	/// <param name="timeout"> Optional timeout for the health check. </param>
	/// <returns> The health checks builder for chaining. </returns>
	public static IHealthChecksBuilder AddCheck<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THealthCheck>(
		this IHealthChecksBuilder builder,
		string name,
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null,
		TimeSpan? timeout = null)
		where THealthCheck : class, IHealthCheck
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(name);

		return builder.AddCheck<THealthCheck>(name, failureStatus, tags, timeout);
	}

	/// <summary>
	/// Adds a health check conditionally based on a predicate.
	/// </summary>
	/// <typeparam name="THealthCheck"> The health check implementation type. </typeparam>
	/// <param name="builder"> The health checks builder. </param>
	/// <param name="condition"> The condition to evaluate. </param>
	/// <param name="name"> The health check name. </param>
	/// <param name="failureStatus"> The failure status to report. </param>
	/// <param name="tags"> Optional tags for the health check. </param>
	/// <param name="timeout"> Optional timeout for the health check. </param>
	/// <returns> The health checks builder for chaining. </returns>
	public static IHealthChecksBuilder AddCheckWhen<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THealthCheck>(
		this IHealthChecksBuilder builder,
		bool condition,
		string name,
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null,
		TimeSpan? timeout = null)
		where THealthCheck : class, IHealthCheck
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(name);

		if (condition)
		{
			_ = builder.AddCheck<THealthCheck>(name, failureStatus, tags, timeout);
		}

		return builder;
	}

	/// <summary>
	/// Adds common transport health check tags.
	/// </summary>
	/// <param name="builder"> The health checks builder. </param>
	/// <param name="name"> The health check name. </param>
	/// <param name="factory"> Factory to create the health check instance. </param>
	/// <param name="failureStatus"> The failure status to report. </param>
	/// <param name="timeout"> Optional timeout for the health check. </param>
	/// <returns> The health checks builder for chaining. </returns>
	public static IHealthChecksBuilder AddTransportHealthCheck(
		this IHealthChecksBuilder builder,
		string name,
		Func<IServiceProvider, IHealthCheck> factory,
		HealthStatus? failureStatus = null,
		TimeSpan? timeout = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(name);
		ArgumentNullException.ThrowIfNull(factory);

		return builder.Add(new HealthCheckRegistration(
			name,
			factory,
			failureStatus,
			["transport", "messaging", "ready"],
			timeout));
	}

	/// <summary>
	/// Adds common persistence health check tags.
	/// </summary>
	/// <param name="builder"> The health checks builder. </param>
	/// <param name="name"> The health check name. </param>
	/// <param name="factory"> Factory to create the health check instance. </param>
	/// <param name="failureStatus"> The failure status to report. </param>
	/// <param name="timeout"> Optional timeout for the health check. </param>
	/// <returns> The health checks builder for chaining. </returns>
	public static IHealthChecksBuilder AddPersistenceHealthCheck(
		this IHealthChecksBuilder builder,
		string name,
		Func<IServiceProvider, IHealthCheck> factory,
		HealthStatus? failureStatus = null,
		TimeSpan? timeout = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(name);
		ArgumentNullException.ThrowIfNull(factory);

		return builder.Add(new HealthCheckRegistration(
			name,
			factory,
			failureStatus,
			["persistence", "database", "ready"],
			timeout));
	}

	/// <summary>
	/// Creates a new health check data builder for constructing health check results.
	/// </summary>
	/// <returns> A new health check data builder. </returns>
	public static HealthCheckDataBuilder CreateDataBuilder()
	{
		return new HealthCheckDataBuilder();
	}
}
