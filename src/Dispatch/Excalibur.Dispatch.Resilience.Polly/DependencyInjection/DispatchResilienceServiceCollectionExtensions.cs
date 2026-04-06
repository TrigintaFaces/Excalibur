// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Excalibur.Dispatch.Resilience.Polly;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Polly;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for integrating Dispatch resilience with Microsoft.Extensions.Resilience.
/// </summary>
public static class DispatchResilienceServiceCollectionExtensions
{
	/// <summary>
	/// Adds a Dispatch resilience adapter that wraps an existing <see cref="ResiliencePipeline"/>
	/// from Microsoft.Extensions.Resilience / Polly v8.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="pipelineName">The name for keyed service registration.</param>
	/// <param name="configurePipeline">Action to configure the resilience pipeline builder.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method bridges the gap between Dispatch's resilience abstractions and the
	/// <c>Microsoft.Extensions.Resilience</c> ecosystem. Consumers who already use Polly v8
	/// pipelines can integrate them directly with Dispatch.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.UseDispatchResilience("my-pipeline", builder =>
	/// {
	///     builder.AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 3 });
	///     builder.AddTimeout(TimeSpan.FromSeconds(10));
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection UseDispatchResilience(
		this IServiceCollection services,
		string pipelineName,
		Action<ResiliencePipelineBuilder> configurePipeline)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);
		ArgumentNullException.ThrowIfNull(configurePipeline);

		// Build the pipeline
		var builder = new ResiliencePipelineBuilder();
		configurePipeline(builder);
		var pipeline = builder.Build();

		// Register as keyed singleton
		_ = services.AddKeyedSingleton(pipelineName, (_, _) => new DispatchResilienceAdapter(pipeline));

		// Register the default (non-keyed) if this is the first registration
		services.TryAddSingleton(_ => new DispatchResilienceAdapter(pipeline));

		return services;
	}

	/// <summary>
	/// Adds the hedging policy to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Action to configure hedging options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddHedgingPolicy(
		this IServiceCollection services,
		Action<HedgingOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddOptions<HedgingOptions>()
			.Configure(options => configureOptions?.Invoke(options))
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<HedgingOptions>, HedgingOptionsValidator>());

		services.TryAddSingleton(sp =>
		{
			var options = sp.GetRequiredService<Options.IOptions<HedgingOptions>>().Value;
			var logger = sp.GetRequiredService<ILogger<HedgingPolicy>>();
			return new HedgingPolicy(options, logger);
		});

		return services;
	}

	/// <summary>
	/// Adds the hedging policy to the service collection
	/// using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind hedging options from.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddHedgingPolicy(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<HedgingOptions>()
			.Bind(configuration)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<HedgingOptions>, HedgingOptionsValidator>());

		services.TryAddSingleton(sp =>
		{
			var options = sp.GetRequiredService<Options.IOptions<HedgingOptions>>().Value;
			var logger = sp.GetRequiredService<ILogger<HedgingPolicy>>();
			return new HedgingPolicy(options, logger);
		});

		return services;
	}

	/// <summary>
	/// Adds the resilience telemetry pipeline to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="pipelineName">The pipeline name for metric tagging.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddResilienceTelemetry(
		this IServiceCollection services,
		string pipelineName)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);

		_ = services.AddKeyedSingleton(pipelineName, (sp, _) =>
		{
			var logger = sp.GetRequiredService<ILogger<TelemetryResiliencePipeline>>();
			var meterFactory = sp.GetService<System.Diagnostics.Metrics.IMeterFactory>();
			return new TelemetryResiliencePipeline(pipelineName, logger, meterFactory);
		});

		return services;
	}
}
