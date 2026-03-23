// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.Diagnostics;
using Excalibur.Data.Abstractions.CloudNative;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods to add CDC health checks.
/// </summary>
public static class CdcHealthChecksBuilderExtensions
{
	/// <summary>
	/// Adds a health check for a CDC (Change Data Capture) processor.
	/// </summary>
	/// <param name="builder">The health checks builder.</param>
	/// <param name="configure">Optional configuration for health check thresholds.</param>
	/// <param name="name">The health check name. Default is "cdc".</param>
	/// <param name="failureStatus">The failure status. Default is null (uses context default).</param>
	/// <param name="tags">Optional tags for filtering health checks.</param>
	/// <returns>The health checks builder for chaining.</returns>
	public static IHealthChecksBuilder AddCdcHealthCheck(
		this IHealthChecksBuilder builder,
		Action<CdcHealthCheckOptions>? configure = null,
		string name = "cdc",
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		if (configure is not null)
		{
			_ = builder.Services.Configure(configure);
		}

		builder.Services.TryAddSingleton<CdcHealthState>();

		return builder.Add(new HealthCheckRegistration(
			name,
			sp => ActivatorUtilities.CreateInstance<CdcHealthCheck>(sp),
			failureStatus,
			tags));
	}
}
