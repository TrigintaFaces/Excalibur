// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Excalibur.Hosting;

using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for adding memory-related health checks to the application's health monitoring system.
/// </summary>
public static class MemoryHealthChecksBuilderExtensions
{
	/// <summary>
	/// Adds health checks to monitor process-allocated memory and working set memory
	/// using the default thresholds from <see cref="MemoryHealthCheckOptions"/>.
	/// </summary>
	/// <param name="healthChecks"> The <see cref="IHealthChecksBuilder" /> to configure. </param>
	/// <returns> The configured <see cref="IHealthChecksBuilder" /> for chaining additional health checks. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="healthChecks" /> is null. </exception>
	public static IHealthChecksBuilder AddMemoryHealthChecks(this IHealthChecksBuilder healthChecks) =>
		AddMemoryHealthChecks(healthChecks, _ => { });

	/// <summary>
	/// Adds health checks to monitor process-allocated memory and working set memory
	/// with configurable thresholds.
	/// </summary>
	/// <param name="healthChecks"> The <see cref="IHealthChecksBuilder" /> to configure. </param>
	/// <param name="configure"> An action to configure the <see cref="MemoryHealthCheckOptions"/>. </param>
	/// <returns> The configured <see cref="IHealthChecksBuilder" /> for chaining additional health checks. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="healthChecks" /> or <paramref name="configure"/> is null. </exception>
	public static IHealthChecksBuilder AddMemoryHealthChecks(
		this IHealthChecksBuilder healthChecks,
		Action<MemoryHealthCheckOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(healthChecks);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new MemoryHealthCheckOptions();
		configure(options);

		return healthChecks.AddMemoryHealthChecksCore(options);
	}

	/// <summary>
	/// Adds health checks to monitor process-allocated memory and working set memory
	/// using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="healthChecks"> The <see cref="IHealthChecksBuilder" /> to configure. </param>
	/// <param name="configuration"> The configuration section to bind to <see cref="MemoryHealthCheckOptions"/>. </param>
	/// <returns> The configured <see cref="IHealthChecksBuilder" /> for chaining additional health checks. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="healthChecks" /> or <paramref name="configuration"/> is null. </exception>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IHealthChecksBuilder AddMemoryHealthChecks(
		this IHealthChecksBuilder healthChecks,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(healthChecks);
		ArgumentNullException.ThrowIfNull(configuration);

		var options = new MemoryHealthCheckOptions();
		configuration.Bind(options);

		return healthChecks.AddMemoryHealthChecksCore(options);
	}

	private static IHealthChecksBuilder AddMemoryHealthChecksCore(
		this IHealthChecksBuilder healthChecks,
		MemoryHealthCheckOptions options)
	{
		// Add a health check for process-allocated memory
		_ = healthChecks
			.AddProcessAllocatedMemoryHealthCheck(
				options.AllocatedMemoryThresholdKB,
				"process_allocated_memory");

		// Add a health check for working set memory
		_ = healthChecks
			.AddWorkingSetHealthCheck(
				options.WorkingSetThresholdBytes,
				"workingset");

		return healthChecks;
	}
}
