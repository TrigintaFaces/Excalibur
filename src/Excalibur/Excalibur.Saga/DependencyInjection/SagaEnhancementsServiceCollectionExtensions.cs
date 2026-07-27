// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Saga.Handlers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering saga enhancement services.
/// </summary>
public static class SagaEnhancementsServiceCollectionExtensions
{
	/// <summary>
	/// Adds the default logging-based saga not-found handler for a specific saga type.
	/// </summary>
	/// <typeparam name="TSaga">The saga type to register the handler for.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSagaNotFoundHandler<TSaga>(this IServiceCollection services)
		where TSaga : class
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<ISagaNotFoundHandler<TSaga>, LoggingNotFoundHandler<TSaga>>();

		return services;
	}

	/// <summary>
	/// Adds a custom saga not-found handler for a specific saga type.
	/// </summary>
	/// <typeparam name="TSaga">The saga type to register the handler for.</typeparam>
	/// <typeparam name="THandler">The handler implementation type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSagaNotFoundHandler<TSaga,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
		this IServiceCollection services)
		where TSaga : class
		where THandler : class, ISagaNotFoundHandler<TSaga>
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<ISagaNotFoundHandler<TSaga>, THandler>();

		return services;
	}
}
