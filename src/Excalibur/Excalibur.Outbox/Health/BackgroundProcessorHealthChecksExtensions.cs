// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.Health;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering background processor health checks.
/// </summary>
public static class BackgroundProcessorHealthChecksExtensions
{
	/// <summary>
	/// Adds a health check for the outbox background service.
	/// </summary>
	/// <param name="builder">The health checks builder.</param>
	/// <param name="configure">Optional configuration for health check thresholds.</param>
	/// <param name="name">The health check name. Default is "outbox".</param>
	/// <param name="failureStatus">The failure status. Default is null (uses context default).</param>
	/// <param name="tags">Optional tags for filtering health checks.</param>
	/// <returns>The health checks builder for chaining.</returns>
	public static IHealthChecksBuilder AddOutboxHealthCheck(
		this IHealthChecksBuilder builder,
		Action<OutboxHealthCheckOptions>? configure = null,
		string name = "outbox",
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		if (configure is not null)
		{
			_ = builder.Services.Configure(configure);
		}

		builder.Services.TryAddSingleton<BackgroundServiceHealthState>();

		return builder.Add(new HealthCheckRegistration(
			name,
			sp => ActivatorUtilities.CreateInstance<OutboxHealthCheck>(sp),
			failureStatus,
			tags));
	}

	/// <summary>
	/// Adds a health check for the inbox background service.
	/// </summary>
	/// <param name="builder">The health checks builder.</param>
	/// <param name="configure">Optional configuration for health check thresholds.</param>
	/// <param name="name">The health check name. Default is "inbox".</param>
	/// <param name="failureStatus">The failure status. Default is null (uses context default).</param>
	/// <param name="tags">Optional tags for filtering health checks.</param>
	/// <returns>The health checks builder for chaining.</returns>
	public static IHealthChecksBuilder AddInboxHealthCheck(
		this IHealthChecksBuilder builder,
		Action<InboxHealthCheckOptions>? configure = null,
		string name = "inbox",
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		if (configure is not null)
		{
			_ = builder.Services.Configure(configure);
		}

		builder.Services.TryAddSingleton<BackgroundServiceHealthState>();

		return builder.Add(new HealthCheckRegistration(
			name,
			sp => ActivatorUtilities.CreateInstance<InboxHealthCheck>(sp),
			failureStatus,
			tags));
	}
}
