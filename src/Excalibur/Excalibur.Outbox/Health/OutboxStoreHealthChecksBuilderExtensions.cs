// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.Health;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the outbox store connectivity health check.
/// </summary>
public static class OutboxStoreHealthChecksBuilderExtensions
{
	/// <summary>
	/// Adds a health check that probes outbox store connectivity via
	/// <see cref="IOutboxStoreAdmin.GetStatisticsAsync"/>.
	/// </summary>
	/// <param name="builder">The health checks builder.</param>
	/// <param name="name">The health check name. Default is "outbox-store".</param>
	/// <param name="failureStatus">The failure status. Default is <see langword="null"/> (context default).</param>
	/// <param name="tags">Optional tags for filtering health checks.</param>
	/// <returns>The health checks builder for chaining.</returns>
	public static IHealthChecksBuilder AddOutboxStoreHealthCheck(
		this IHealthChecksBuilder builder,
		string name = "outbox-store",
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// IOutboxStoreAdmin is registered keyed "default" by every outbox provider; resolve it by that key
		// (a plain ActivatorUtilities resolution would not see the keyed registration).
		return builder.Add(new HealthCheckRegistration(
			name,
			sp => new OutboxStoreHealthCheck(sp.GetRequiredKeyedService<IOutboxStoreAdmin>("default")),
			failureStatus,
			tags));
	}
}
