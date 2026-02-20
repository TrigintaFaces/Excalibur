// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Observability.Metrics;

/// <summary>
/// Provides extension methods for configuring Dispatch metrics instrumentation.
/// </summary>
public static class MetricsDispatchBuilderExtensions
{
	/// <summary>
	/// Adds Dispatch metrics instrumentation to the service collection.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The dispatch builder for method chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when builder is null. </exception>
	public static IDispatchBuilder AddDispatchMetricsInstrumentation(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);
		_ = builder.Services.AddSingleton<DispatchMetrics>();
		_ = builder.Services.AddOptions<ObservabilityOptions>()
			.Configure(static _ => { })
			.ValidateDataAnnotations()
			.ValidateOnStart();
		return builder;
	}

	/// <summary>
	/// Configures metrics options for Excalibur.Dispatch.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <param name="configure"> The configuration action. </param>
	/// <returns> The dispatch builder for method chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when builder or configure is null. </exception>
	public static IDispatchBuilder WithMetricsOptions(this IDispatchBuilder builder, Action<ObservabilityOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);
		_ = builder.Services.AddOptions<ObservabilityOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		return builder;
	}

	/// <summary>
	/// Configures metrics options for Dispatch using configuration.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <param name="configuration"> The configuration section. </param>
	/// <returns> The dispatch builder for method chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when builder or configuration is null. </exception>
	[RequiresUnreferencedCode(
		"Configuration binding may reference types not preserved during trimming. Ensure options types are annotated with DynamicallyAccessedMembers.")]
	[RequiresDynamicCode(
		"Configuration binding for metrics options requires dynamic code generation for property reflection and value conversion.")]
	public static IDispatchBuilder WithMetricsOptions(this IDispatchBuilder builder, IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);
		_ = builder.Services.AddOptions<ObservabilityOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		return builder;
	}
}
