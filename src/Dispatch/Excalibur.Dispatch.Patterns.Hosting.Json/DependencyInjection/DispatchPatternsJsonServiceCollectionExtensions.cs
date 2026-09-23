// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Patterns;
using Excalibur.Dispatch.Patterns.ClaimCheck;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Options;
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
	/// Optional delegate to customize <see cref="DispatchPatternsJsonOptions" />, including the serializer
	/// configuration delegate and a source-generated context.
	/// </param>
	/// <returns>The service collection for chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddJsonSerialization(o =&gt; o.ConfigureSerializer = json =&gt; json.WriteIndented = true);
	/// </code>
	/// </example>
	public static IServiceCollection AddJsonSerialization(
		this IServiceCollection services,
		Action<DispatchPatternsJsonOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// No ValidateOnStart(): DispatchPatternsJsonOptions has nothing an IValidateOptions<T> could
		// reject (both members are legitimately nullable). ValidateOnStart() with no attached validator
		// runs the pipeline but validates nothing, which is false safety.
		var optionsBuilder = services.AddOptions<DispatchPatternsJsonOptions>();
		if (configure is not null)
		{
			_ = optionsBuilder.Configure(configure);
		}

		// Construct through a factory so the configured delegate and context actually reach the
		// serializer. It layers ConfigureSerializer over its own transport defaults, so a plain
		// TryAddSingleton<DispatchJsonSerializer>() would resolve the parameterless path and discard
		// everything the caller configured.
		services.TryAddSingleton(static sp =>
		{
			var options = sp.GetRequiredService<IOptions<DispatchPatternsJsonOptions>>().Value;
			return new DispatchJsonSerializer(
				options.ConfigureSerializer,
				options.SerializerContext,
				sp.GetService<IPooledBufferService>());
		});

		return services;
	}

	/// <summary>
	/// Registers a ClaimCheck serializer that uses the JSON abstraction for JSON handling.
	/// Requires an <see cref="IClaimCheckProvider" /> to be registered.
	/// </summary>
	/// <param name="services">The service collection to configure.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "ClaimCheck serializer construction uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "ClaimCheck serializer construction uses reflection by design.")]
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
