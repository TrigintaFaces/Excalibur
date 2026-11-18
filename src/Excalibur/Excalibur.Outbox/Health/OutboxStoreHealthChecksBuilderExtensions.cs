// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
	/// <see cref="IOutboxStoreAdmin.GetAllTenantsStatisticsAsync"/>.
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

		// Resolve the admin surface the way the non-keyed alias in OutboxServiceCollectionExtensions
		// already does: keyed "default" first, then cast the keyed IOutboxStore.
		//
		// The previous comment here claimed IOutboxStoreAdmin is "registered keyed default by every
		// outbox provider". That was false, and it made this extension throw on every provider but one.
		// Measured: of the keyed registrations providers actually make, 29 are IOutboxStore and only
		// Elasticsearch registers IOutboxStoreAdmin under that key -- so GetRequiredKeyedService for the
		// admin surface failed at probe time on SQL Server, Postgres, Oracle, MongoDB and Redis. The
		// fallback is safe because every shipped IOutboxStore implementation also implements
		// IOutboxStoreAdmin on its own class declaration (all eight, InMemory and Marten included),
		// which is the same assumption the non-keyed alias already relies on.
		return builder.Add(new HealthCheckRegistration(
			name,
			sp => new OutboxStoreHealthCheck(
				sp.GetKeyedService<IOutboxStoreAdmin>("default")
				?? (IOutboxStoreAdmin)sp.GetRequiredKeyedService<Excalibur.Dispatch.IOutboxStore>("default")),
			failureStatus,
			tags));
	}
}
