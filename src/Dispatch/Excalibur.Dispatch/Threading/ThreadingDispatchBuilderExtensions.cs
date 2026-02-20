// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Options.Threading;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Threading;

/// <summary>
/// Extension methods for configuring threading and concurrency features in the Dispatch message processing pipeline. Provides fluent API
/// for enabling parallel processing, thread pooling, keyed locking, and performance optimization.
/// </summary>
/// <remarks>
/// <para>
/// These extensions enable fine-grained control over message processing concurrency, allowing applications to optimize throughput, resource
/// utilization, and system responsiveness based on specific workload characteristics and infrastructure constraints.
/// </para>
/// <para>
/// Threading configuration should be coordinated with other system resources including:
/// - Database connection pooling limits
/// - External service rate limits and capacity
/// - Memory availability and garbage collection patterns
/// - CPU utilization and thermal constraints.
/// </para>
/// </remarks>
public static class ThreadingDispatchBuilderExtensions
{
	/// <summary>
	/// Adds essential threading services to the Dispatch message processing pipeline. Enables concurrent message processing, thread pool
	/// management, and synchronization primitives.
	/// </summary>
	/// <param name="builder"> The dispatch builder to configure with threading capabilities. </param>
	/// <returns> The dispatch builder for fluent configuration chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="builder" /> is null. </exception>
	/// <remarks>
	/// <para>
	/// This method registers core threading services including:
	/// - Parallel message processing coordinators
	/// - Thread-safe message queuing mechanisms
	/// - Synchronization primitives for resource coordination
	/// - Performance monitoring and metrics collection.
	/// </para>
	/// <para> Should be called early in the configuration pipeline before other features that depend on concurrent processing capabilities. </para>
	/// </remarks>
	public static IDispatchBuilder AddDispatchThreading(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddDispatchThreading();
		return builder;
	}

	/// <summary>
	/// Configures threading options using a delegate for programmatic configuration. Provides maximum flexibility for dynamic threading
	/// parameter adjustment based on runtime conditions.
	/// </summary>
	/// <param name="builder"> The dispatch builder to configure with threading options. </param>
	/// <param name="configure"> A delegate that receives a ThreadingOptions instance for configuration. </param>
	/// <returns> The dispatch builder for fluent configuration chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="builder" /> or <paramref name="configure" /> is null. </exception>
	/// <remarks>
	/// <para>
	/// This overload enables programmatic configuration of threading parameters, allowing applications to adjust concurrency settings based on:
	/// - Runtime environment detection (CPU cores, memory)
	/// - Application-specific performance requirements
	/// - Dynamic load balancing and auto-scaling scenarios.
	/// </para>
	/// <para>
	/// The configure delegate is executed during service registration, enabling access to dependency injection services for complex
	/// configuration logic.
	/// </para>
	/// </remarks>
	public static IDispatchBuilder WithThreadingOptions(this IDispatchBuilder builder, Action<ThreadingOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.Configure(configure);

		return builder;
	}

	/// <summary>
	/// Configures threading options from configuration sources (appsettings.json, environment variables, etc.). Enables externalized
	/// configuration management for threading parameters in deployment scenarios.
	/// </summary>
	/// <param name="builder"> The dispatch builder to configure with threading options. </param>
	/// <param name="configuration"> The configuration section containing threading options. </param>
	/// <returns> The dispatch builder for fluent configuration chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="builder" /> or <paramref name="configuration" /> is null. </exception>
	/// <remarks>
	/// <para>
	/// This method enables configuration binding from external sources, supporting:
	/// - Environment-specific configuration files (appsettings.Development.json, appsettings.Production.json)
	/// - Environment variable overrides for containerized deployments
	/// - Configuration providers (Azure Key Vault, AWS Systems Manager, Kubernetes ConfigMaps)
	/// - Hot configuration reloading in supported hosting environments.
	/// </para>
	/// <para>
	/// Configuration binding follows standard .NET conventions for property mapping and type conversion. See ThreadingOptions documentation
	/// for available settings.
	/// </para>
	/// </remarks>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification =
			"ThreadingOptions is a simple POCO configuration class with public properties. The Configure<T> method is safe for AOT when T has a parameterless constructor and public properties, which ThreadingOptions satisfies.")]
	[RequiresDynamicCode("Configuration binding requires dynamic code generation for property reflection and value conversion.")]
	public static IDispatchBuilder WithThreadingOptions(this IDispatchBuilder builder, IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = builder.Services.Configure<ThreadingOptions>(configuration);

		return builder;
	}

	/// <summary>
	/// Registers a custom keyed lock implementation for coordinating access to shared resources. Enables fine-grained synchronization based
	/// on message keys, entity IDs, or other business identifiers.
	/// </summary>
	/// <param name="builder"> The dispatch builder to configure with keyed locking. </param>
	/// <param name="keyedLock"> The keyed lock implementation to register as a singleton service. </param>
	/// <returns> The dispatch builder for fluent configuration chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="builder" /> or <paramref name="keyedLock" /> is null. </exception>
	/// <remarks>
	/// <para>
	/// Keyed locking enables sophisticated coordination patterns including:
	/// - Entity-level locking to prevent concurrent modification conflicts
	/// - Partition-based locking for scalable resource access control
	/// - Business rule enforcement through synchronized processing
	/// - Integration with external locking services (Redis, database locks, etc.)
	/// </para>
	/// <para>
	/// The provided implementation should be thread-safe and efficient, as it will be used concurrently across the entire message
	/// processing pipeline.
	/// </para>
	/// </remarks>
	public static IDispatchBuilder WithKeyedLock(this IDispatchBuilder builder, IKeyedLock keyedLock)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(keyedLock);

		_ = builder.Services.AddSingleton(keyedLock);
		return builder;
	}
}
