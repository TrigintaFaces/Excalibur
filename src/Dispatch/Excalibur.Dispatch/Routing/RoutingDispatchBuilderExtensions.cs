// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Options.Routing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Routing;

/// <summary>
/// Provides extension methods for configuring message routing capabilities in the Excalibur framework.
/// </summary>
/// <remarks>
/// <para>
/// For advanced routing configuration, use the fluent builder API via <c>UseRouting()</c>
/// from the <c>RoutingBuilderExtensions</c> class.
/// </para>
/// </remarks>
public static class RoutingDispatchBuilderExtensions
{
	/// <summary>
	/// Adds message routing services to the Dispatch builder configuration with default settings.
	/// </summary>
	/// <param name="builder">The dispatch builder to configure.</param>
	/// <returns>The same <see cref="IDispatchBuilder"/> instance for method chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
	/// <remarks>
	/// <para>
	/// This method registers a default <see cref="IDispatchRouter"/> that routes all messages
	/// to the "local" transport. For custom routing configuration, use <c>UseRouting()</c> instead.
	/// </para>
	/// </remarks>
	public static IDispatchBuilder AddDispatchRouting(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddDispatchRouting();

		return builder;
	}

	/// <summary>
	/// Configures routing options using a delegate-based approach.
	/// </summary>
	/// <param name="builder">The dispatch builder to configure.</param>
	/// <param name="configure">A delegate that configures the routing options.</param>
	/// <returns>The same <see cref="IDispatchBuilder"/> instance for method chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
	public static IDispatchBuilder WithRoutingOptions(this IDispatchBuilder builder, Action<RoutingOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddOptions<RoutingOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return builder;
	}

	/// <summary>
	/// Configures routing options from a configuration section.
	/// </summary>
	/// <param name="builder">The dispatch builder to configure.</param>
	/// <param name="configuration">The configuration section containing routing options.</param>
	/// <returns>The same <see cref="IDispatchBuilder"/> instance for method chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="configuration"/> is null.</exception>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification =
			"RoutingOptions is a simple POCO configuration class with public properties. The Configure<T> method is safe for AOT when T has a parameterless constructor and public properties, which RoutingOptions satisfies.")]
	[RequiresDynamicCode(
		"Configuration binding for RoutingOptions requires dynamic code generation for property reflection and value conversion.")]
	public static IDispatchBuilder WithRoutingOptions(this IDispatchBuilder builder, IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = builder.Services.AddOptions<RoutingOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return builder;
	}
}
