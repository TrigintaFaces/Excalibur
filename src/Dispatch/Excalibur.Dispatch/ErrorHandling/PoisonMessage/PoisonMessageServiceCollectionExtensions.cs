// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Options.ErrorHandling;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring poison message handling services.
/// </summary>
public static class PoisonMessageServiceCollectionExtensions
{
	/// <summary>
	/// Adds poison message handling services to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration section for poison message options. </param>
	/// <returns> The service collection for chaining. </returns>
	[RequiresUnreferencedCode(
		"Configuration binding may reference types not preserved during trimming. Ensure options types are annotated with DynamicallyAccessedMembers.")]
	[RequiresDynamicCode("Configuration binding requires dynamic code generation for property reflection and value conversion.")]
	public static IServiceCollection AddPoisonMessageHandling(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<PoisonMessageOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return services.AddPoisonMessageHandling();
	}

	/// <summary>
	/// Adds poison message handling services to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Action to configure poison message options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddPoisonMessageHandling(
		this IServiceCollection services,
		Action<PoisonMessageOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var optionsBuilder = services.AddOptions<PoisonMessageOptions>();
		if (configureOptions != null)
		{
			_ = optionsBuilder.Configure(configureOptions);
		}

		_ = optionsBuilder.ValidateDataAnnotations().ValidateOnStart();

		// Register core services
		services.TryAddSingleton<IPoisonMessageHandler, PoisonMessageHandler>();

		// Register default detectors
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IPoisonMessageDetector, RetryCountPoisonDetector>());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IPoisonMessageDetector, ExceptionTypePoisonDetector>());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IPoisonMessageDetector, TimespanPoisonDetector>());

		// Register composite detector as the primary detector
		services.TryAddSingleton<IPoisonMessageDetector, CompositePoisonDetector>();

		// Register middleware
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IDispatchMiddleware, PoisonMessageMiddleware>());

		// Register default in-memory store if no store is registered
		services.TryAddSingleton<IDeadLetterStore, InMemoryDeadLetterStore>();

		// Register cleanup service if auto-cleanup is enabled
		_ = services.AddHostedService<PoisonMessageCleanupService>();

		return services;
	}

	/// <summary>
	/// Configures poison message handling to use an in-memory dead letter store.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddInMemoryDeadLetterStore(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.RemoveAll<IDeadLetterStore>();
		_ = services.AddSingleton<IDeadLetterStore, InMemoryDeadLetterStore>();

		return services;
	}

	// NOTE: SQL dead letter store moved to Excalibur.Data.SqlServer.AddSqlServerDeadLetterStore() (Sprint 306)

	/// <summary>
	/// Adds a custom poison message detector.
	/// </summary>
	/// <typeparam name="TDetector"> The type of the detector to add. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddPoisonMessageDetector<
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TDetector>(this IServiceCollection services)
		where TDetector : class, IPoisonMessageDetector
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddEnumerable(ServiceDescriptor.Singleton<IPoisonMessageDetector, TDetector>());

		return services;
	}

	/// <summary>
	/// Removes a poison message detector.
	/// </summary>
	/// <typeparam name="TDetector"> The type of the detector to remove. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection RemovePoisonMessageDetector<TDetector>(this IServiceCollection services)
		where TDetector : class, IPoisonMessageDetector
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.RemoveAll<TDetector>();

		return services;
	}
}
