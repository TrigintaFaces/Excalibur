// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Jobs.Workflows;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for configuring workflow services in an <see cref="IServiceCollection" />.
/// </summary>
public static class WorkflowServiceCollectionExtensions
{
	/// <summary>
	/// Adds workflow orchestration services to the specified service collection.
	/// </summary>
	/// <param name="services"> The service collection to configure. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="services" /> is null. </exception>
	public static IServiceCollection AddWorkflows(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register core workflow services
		_ = services.AddScoped<IWorkflowContext, WorkflowContext>();

		return services;
	}

	/// <summary>
	/// Registers a workflow implementation for dependency injection.
	/// </summary>
	/// <typeparam name="TWorkflow"> The workflow implementation type. </typeparam>
	/// <typeparam name="TInput"> The type of input data for the workflow. </typeparam>
	/// <typeparam name="TOutput"> The type of output data from the workflow. </typeparam>
	/// <param name="services"> The service collection to configure. </param>
	/// <param name="lifetime"> The service lifetime for the workflow. Defaults to Scoped. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="services" /> is null. </exception>
	public static IServiceCollection AddWorkflow<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TWorkflow, TInput, TOutput>(
		this IServiceCollection services,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
		where TWorkflow : class, IWorkflow<TInput, TOutput>
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register the workflow
		services.Add(new ServiceDescriptor(typeof(TWorkflow), typeof(TWorkflow), lifetime));
		services.Add(new ServiceDescriptor(typeof(IWorkflow<TInput, TOutput>), typeof(TWorkflow), lifetime));

		// Register the associated workflow job
		services.Add(new ServiceDescriptor(typeof(WorkflowJob<TWorkflow, TInput, TOutput>), typeof(WorkflowJob<TWorkflow, TInput, TOutput>),
			lifetime));

		return services;
	}

	/// <summary>
	/// Registers a simple workflow implementation with no input parameters.
	/// </summary>
	/// <typeparam name="TWorkflow"> The workflow implementation type. </typeparam>
	/// <typeparam name="TOutput"> The type of output data from the workflow. </typeparam>
	/// <param name="services"> The service collection to configure. </param>
	/// <param name="lifetime"> The service lifetime for the workflow. Defaults to Scoped. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="services" /> is null. </exception>
	public static IServiceCollection AddWorkflow<TWorkflow, TOutput>(
		this IServiceCollection services,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
		where TWorkflow : class, IWorkflow<TOutput> =>
		services.AddWorkflow<TWorkflow, object?, TOutput>(lifetime);
}
