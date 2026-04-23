// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing.Diagnostics;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the data processing health check.
/// </summary>
public static class DataProcessingHealthChecksBuilderExtensions
{
	/// <summary>
	/// Adds the data processing health check to the health checks builder.
	/// </summary>
	/// <param name="builder">The health checks builder.</param>
	/// <param name="name">The health check name. Defaults to "data_processing".</param>
	/// <param name="failureStatus">
	/// The <see cref="HealthStatus"/> that should be reported when the health check reports a failure.
	/// Defaults to <see langword="null"/>, which causes the health check infrastructure to use
	/// <see cref="HealthStatus.Unhealthy"/>.
	/// </param>
	/// <param name="tags">Optional tags for filtering health checks.</param>
	/// <returns>The health checks builder for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers the <see cref="DataProcessingHealthState"/> singleton (if not already
	/// registered) so the data processing hosted service and health check share the same instance.
	/// </para>
	/// <para>
	/// Usage:
	/// <code>
	/// builder.Services.AddHealthChecks()
	///     .AddDataProcessingHealthCheck();
	/// </code>
	/// </para>
	/// </remarks>
	public static IHealthChecksBuilder AddDataProcessingHealthCheck(
		this IHealthChecksBuilder builder,
		string name = "data_processing",
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Ensure the shared health state singleton exists
		builder.Services.TryAddSingleton<DataProcessingHealthState>();

		builder.Add(new HealthCheckRegistration(
			name,
			sp => new DataProcessingHealthCheck(
				sp.GetRequiredService<DataProcessingHealthState>(),
				sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DataProcessingHealthCheckOptions>>()),
			failureStatus,
			tags));

		return builder;
	}
}
