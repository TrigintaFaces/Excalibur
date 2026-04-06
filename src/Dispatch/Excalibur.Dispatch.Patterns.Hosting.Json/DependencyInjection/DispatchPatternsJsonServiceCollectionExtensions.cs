// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Patterns;
using Excalibur.Dispatch.Patterns.ClaimCheck;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring JSON serialization services for Dispatch Patterns.
/// </summary>
public static class DispatchPatternsJsonServiceCollectionExtensions
{
	/// <summary>
	/// Registers the System.Text.Json-based <see cref="DispatchJsonSerializer" /> for Excalibur.Dispatch.Patterns hosting scenarios.
	/// </summary>
	/// <param name="services">The service collection to configure.</param>
	/// <param name="configure">
	/// Optional delegate to customize <see cref="DispatchPatternsJsonOptions" />, including serializer options and source-generated contexts.
	/// </param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddJsonSerialization(
		this IServiceCollection services,
		Action<DispatchPatternsJsonOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var optionsBuilder = services.AddOptions<DispatchPatternsJsonOptions>()
			.ValidateOnStart();
		if (configure is not null)
		{
			_ = optionsBuilder.Configure(configure);
		}

		services.TryAddSingleton<DispatchJsonSerializer>();
		return services;
	}

	/// <summary>
	/// Registers the System.Text.Json-based <see cref="DispatchJsonSerializer" /> for Excalibur.Dispatch.Patterns hosting scenarios
	/// using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection to configure.</param>
	/// <param name="configuration">The configuration section to bind JSON serialization options from.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddJsonSerialization(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<DispatchPatternsJsonOptions>()
			.Bind(configuration)
			.ValidateOnStart();

		services.TryAddSingleton<DispatchJsonSerializer>();
		return services;
	}

	/// <summary>
	/// Registers a ClaimCheck serializer that uses the JSON abstraction for JSON handling.
	/// Requires an <see cref="IClaimCheckProvider" /> to be registered.
	/// </summary>
	/// <param name="services">The service collection to configure.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddDispatchPatternsClaimCheckJson(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);
		services.TryAddTransient<ISerializer>(sp =>
		{
			var provider = sp.GetRequiredService<IClaimCheckProvider>();
			return new ClaimCheckMessageSerializer(provider);
		});
		return services;
	}
}
