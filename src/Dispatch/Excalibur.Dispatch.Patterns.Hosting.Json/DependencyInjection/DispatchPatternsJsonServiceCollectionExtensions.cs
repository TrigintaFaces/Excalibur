// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Patterns;
using Excalibur.Dispatch.Patterns.ClaimCheck;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring JSON serialization services for Dispatch Patterns.
/// </summary>
public static class DispatchPatternsJsonServiceCollectionExtensions
{
	/// <summary>
	/// Registers the System.Text.Json-based <see cref="IJsonSerializer" /> implementation for Excalibur.Dispatch.Patterns hosting scenarios.
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
			.ValidateDataAnnotations()
			.ValidateOnStart();
		if (configure is not null)
		{
			_ = optionsBuilder.Configure(configure);
		}

		services.TryAddSingleton<IJsonSerializer, SystemTextJsonSerializer>();
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
		services.TryAddTransient<IBinaryMessageSerializer>(sp =>
		{
			var provider = sp.GetRequiredService<IClaimCheckProvider>();
			return new ClaimCheckMessageSerializer(provider);
		});
		return services;
	}
}
