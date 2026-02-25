// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Versioning;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring message upcasting services.
/// </summary>
public static class UpcastingServiceCollectionExtensions
{
	/// <summary>
	/// Adds message upcasting services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers:
	/// </para>
	/// <list type="bullet">
	///   <item><see cref="IUpcastingPipeline"/> - Singleton pipeline for message upcasting</item>
	/// </list>
	/// <para>
	/// Use <see cref="AddMessageUpcasting(IServiceCollection, Action{UpcastingBuilder})"/> to configure
	/// upcasters and enable features like auto-upcasting on replay.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddMessageUpcasting(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register options infrastructure
		_ = services.AddOptions<UpcastingOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register the pipeline as singleton with deferred configuration
		services.TryAddSingleton<IUpcastingPipeline>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<UpcastingOptions>>().Value;
			var pipeline = new UpcastingPipeline();

			// Execute all registration actions
			foreach (var action in options.RegistrationActions)
			{
				action(pipeline, sp);
			}

			return pipeline;
		});

		return services;
	}

	/// <summary>
	/// Adds message upcasting services with configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for the upcasting builder.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This is the primary method for configuring message upcasting. It allows you to
	/// register upcasters, scan assemblies, and enable features.
	/// </para>
	/// <para>
	/// <b>Usage:</b>
	/// <code>
	/// services.AddMessageUpcasting(builder =>
	/// {
	///     // Register individual upcasters
	///     builder.RegisterUpcaster&lt;UserEventV1, UserEventV2&gt;(new UserEventV1ToV2());
	///
	///     // Or scan assemblies for auto-discovery
	///     builder.ScanAssembly(typeof(Program).Assembly);
	///
	///     // Enable auto-upcasting during event store replay
	///     builder.EnableAutoUpcastOnReplay();
	/// });
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddMessageUpcasting(
		this IServiceCollection services,
		Action<UpcastingBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		// Ensure base services are registered
		_ = services.AddMessageUpcasting();

		// Configure options using the builder pattern
		_ = services.Configure<UpcastingOptions>(options =>
		{
			var builder = new UpcastingBuilder(options);
			configure(builder);
		});

		return services;
	}

	/// <summary>
	/// Checks if message upcasting services have been registered.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>True if upcasting services are registered; otherwise false.</returns>
	public static bool HasMessageUpcasting(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);
		return services.Any(s => s.ServiceType == typeof(IUpcastingPipeline));
	}
}
