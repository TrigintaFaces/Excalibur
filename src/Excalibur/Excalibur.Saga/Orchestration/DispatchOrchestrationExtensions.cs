// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Orchestration;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering dispatch orchestration services with dependency injection. Configures saga management, workflow
/// coordination, and orchestration middleware components.
/// </summary>
public static class DispatchOrchestrationExtensions
{
	/// <summary>
	/// Registers dispatch orchestration services with the service collection. Adds the saga coordinator and saga
	/// handling middleware for managing long-running business processes and distributed transactions. The saga
	/// store is NOT registered here (iuv3s1) — register a persistent provider or opt into the in-memory store
	/// via <see cref="SagaServiceCollectionExtensions.AddInMemorySagaStore"/>; startup validation fails fast if neither is present.
	/// </summary>
	/// <param name="services"> The service collection to configure. </param>
	/// <returns> The service collection for method chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is null. </exception>
	public static IServiceCollection AddExcaliburOrchestration(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// iuv3s1: do NOT silently bind an in-memory saga store as the "default". Saga state is lost on
		// restart/scale-out, so the store is a required deployment decision — register a persistent
		// provider or opt in explicitly via AddInMemorySagaStore() / ISagaBuilder.UseInMemoryStore().
		// SagaPrerequisiteValidator fails loud at host startup if neither registered a "default" store.
		// (Consistent with the AddExcaliburSaga() site — same fail-fast mechanism, no fork.)
		services.TryAddSingleton<ISagaCoordinator, SagaCoordinator>();
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IDispatchMiddleware, SagaHandlingMiddleware>());
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, Excalibur.Saga.DependencyInjection.SagaPrerequisiteValidator>());

		return services;
	}
}
