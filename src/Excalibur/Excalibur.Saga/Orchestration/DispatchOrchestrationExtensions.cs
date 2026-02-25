// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.Saga.Orchestration;

/// <summary>
/// Extension methods for registering dispatch orchestration services with dependency injection. Configures saga management, workflow
/// coordination, and orchestration middleware components.
/// </summary>
public static class DispatchOrchestrationExtensions
{
	/// <summary>
	/// Registers dispatch orchestration services with the service collection. Adds saga store, saga coordinator, and saga handling
	/// middleware for managing long-running business processes and distributed transactions.
	/// </summary>
	/// <param name="services"> The service collection to configure. </param>
	/// <returns> The service collection for method chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is null. </exception>
	public static IServiceCollection AddDispatchOrchestration(this IServiceCollection services)
	{
		services.TryAddSingleton<ISagaStore, InMemorySagaStore>();
		services.TryAddSingleton<ISagaCoordinator, SagaCoordinator>();
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IDispatchMiddleware, SagaHandlingMiddleware>());

		return services;
	}
}
