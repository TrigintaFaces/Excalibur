// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Excalibur Azure Functions serverless hosting services.
/// Composes on top of the Dispatch Azure Functions bridge by calling
/// <see cref="AzureFunctionsServiceCollectionExtensions.AddAzureFunctionsServerless(IServiceCollection)"/> first,
/// and today adds NOTHING of its own on top.
/// </summary>
/// <remarks>
/// This type exists as the seam where Excalibur-layer wiring would go, and that wiring does not exist
/// yet. It is documented as absent rather than described in the present tense, because an IntelliSense
/// summary claiming it registers saga, event sourcing and outbox services would tell a consumer their
/// sagas and outbox were wired for this platform when nothing had registered them.
/// <para>
/// Those subsystems are not platform-specific and need nothing from here: register them the ordinary
/// way, through <c>AddExcalibur</c> and its builder, alongside this call. Calling this method is
/// equivalent to calling <c>AddAzureFunctionsServerless</c> directly, plus the serverless host options the
/// configuring overloads bind.
/// </para>
/// </remarks>
public static class ExcaliburAzureFunctionsServiceCollectionExtensions
{
	/// <summary>
	/// Adds Excalibur Azure Functions serverless hosting services to the specified service collection.
	/// Registers the Dispatch bridge (host provider, cold-start optimizer). Adds no Excalibur-layer
	/// services: see the remarks on this class for why that is stated rather than implied, and where
	/// saga, event sourcing and outbox registration actually belong.
	/// </summary>
	/// <param name="services"> The service collection to add services to. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services is null. </exception>
	public static IServiceCollection AddExcaliburAzureFunctionsServerless(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Dispatch bridge: registers IServerlessHostProvider, IColdStartOptimizer
		_ = services.AddAzureFunctionsServerless();

		// No Excalibur-layer registrations. This is deliberately empty rather than forgotten: saga, event
		// sourcing and outbox wiring is not platform-specific, so it is composed through AddExcalibur by
		// the host instead. If something genuinely platform-specific is ever needed, it goes here AND in
		// this class's documentation, which currently states the absence.

		return services;
	}

	/// <summary>
	/// Adds Excalibur Azure Functions serverless hosting services to the specified service collection with configuration.
	/// </summary>
	/// <param name="services"> The service collection to add services to. </param>
	/// <param name="configureOptions"> An action to configure the serverless host options. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services or configureOptions is null. </exception>
	public static IServiceCollection AddExcaliburAzureFunctionsServerless(
		this IServiceCollection services,
		Action<ServerlessHostOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		_ = services.AddExcaliburAzureFunctionsServerless();
		_ = services.Configure(configureOptions);

		return services;
	}

	/// <summary>
	/// Adds Excalibur Azure Functions serverless hosting services to the specified service collection
	/// using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services"> The service collection to add services to. </param>
	/// <param name="configuration"> The configuration section to bind options from. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services or configuration is null. </exception>
	[RequiresUnreferencedCode("Binding configuration to the options type reflects over its members, which trimming may remove. Configure the options in code instead of binding IConfiguration.")]
	[RequiresDynamicCode("Binding configuration to the options type can require runtime code generation, which native AOT does not support. Configure the options in code instead of binding IConfiguration.")]
	public static IServiceCollection AddExcaliburAzureFunctionsServerless(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddExcaliburAzureFunctionsServerless();
		_ = services.AddOptions<ServerlessHostOptions>()
			.Bind(configuration);

		return services;
	}
}
