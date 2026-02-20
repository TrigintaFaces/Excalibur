// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.Pooling;
using Excalibur.Dispatch.Pooling.Telemetry;
using Excalibur.Dispatch.ZeroAlloc;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring pool infrastructure. Part of PERF-001.4: Pool configuration integration with DI.
/// </summary>
public static partial class PoolServiceCollectionExtensions
{
	/// <summary>
	/// Adds pool infrastructure with configuration.
	/// </summary>
	public static IServiceCollection AddDispatchPools(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configuration);
		return services.AddDispatchPools(configuration.GetSection("Dispatch:Pools"));
	}

	/// <summary>
	/// Adds pool infrastructure with configuration section.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
		"Configuration binding may reference types not preserved during trimming. Ensure options types are annotated with DynamicallyAccessedMembers.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(
		"Configuration binding for PoolOptions requires dynamic code generation for property reflection and value conversion.")]
	public static IServiceCollection AddDispatchPools(
		this IServiceCollection services,
		IConfigurationSection configurationSection)
	{
		// Add options
		_ = services.AddOptions<PoolOptions>()
			.Bind(configurationSection)
			.ValidateOnStart();

		// Add MessageContextPool for message context object pooling
		services.TryAddSingleton<IMessageContextPool>(static sp => new MessageContextPool(sp));

		// Add telemetry if configured
		_ = services.AddPoolTelemetry();

		// Add diagnostics if configured
		_ = services.AddPoolDiagnostics();

		return services;
	}

	/// <summary>
	/// Adds pool infrastructure with inline configuration.
	/// </summary>
	public static IServiceCollection AddDispatchPools(
		this IServiceCollection services,
		Action<PoolOptions> configureOptions)
	{
		_ = services.Configure(configureOptions);

		// Add MessageContextPool for message context object pooling
		services.TryAddSingleton<IMessageContextPool>(static sp => new MessageContextPool(sp));

		return services;
	}

	/// <summary>
	/// Configures pool options post-registration.
	/// </summary>
	public static IServiceCollection ConfigureDispatchPools(
		this IServiceCollection services,
		Action<PoolOptions> configureOptions)
	{
		_ = services.PostConfigure(configureOptions);
		return services;
	}

	// Source-generated logging methods
	[LoggerMessage(CoreEventId.PoolHealthReport, LogLevel.Information, "Pool health report: {Report}")]
	private static partial void LogPoolHealthReport(ILogger logger, object report);

	/// <summary>
	/// Adds pool telemetry services.
	/// </summary>
	private static IServiceCollection AddPoolTelemetry(this IServiceCollection services)
	{
		services.TryAddSingleton(static sp =>
		{
			var options = sp.GetRequiredService<IOptions<PoolOptions>>().Value;
			return new PoolTelemetryProvider();
		});

		return services;
	}

	/// <summary>
	/// Adds pool diagnostics services.
	/// </summary>
	private static IServiceCollection AddPoolDiagnostics(this IServiceCollection services)
	{
		services.TryAddSingleton(static sp =>
		{
			var options = sp.GetRequiredService<IOptions<PoolOptions>>().Value;
			if (!options.Global.EnableDiagnostics)
			{
				return new PoolDiagnostics(); // Return instance even if disabled
			}

			var diagnostics = new PoolDiagnostics();

			// Set up timer for periodic reporting if diagnostics interval is configured
			if (options.Global.DiagnosticsInterval > TimeSpan.Zero)
			{
				var timer = new Timer(
					_ =>
					{
						var report = diagnostics.GetReport();
						var logger = sp.GetService<ILogger<PoolDiagnostics>>();
						if (logger is not null)
						{
							LogPoolHealthReport(logger, report);
						}
					}, state: null, options.Global.DiagnosticsInterval, options.Global.DiagnosticsInterval);
			}

			return diagnostics;
		});

		return services;
	}
}
