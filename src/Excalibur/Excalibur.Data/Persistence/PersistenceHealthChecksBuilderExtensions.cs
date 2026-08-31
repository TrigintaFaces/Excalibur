// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Persistence;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the persistence provider health check.
/// </summary>
public static class PersistenceHealthChecksBuilderExtensions
{
	/// <summary>
	/// Adds a health check that probes a keyed persistence provider's connectivity.
	/// </summary>
	/// <param name="builder">The health checks builder.</param>
	/// <param name="providerKey">
	/// The keyed-DI key the provider is registered under — the key its package uses (for example
	/// <c>"sqlserver"</c>, <c>"postgres"</c>, <c>"inmemory"</c>), or <c>"default"</c>.
	/// </param>
	/// <param name="name">The health check name. Default is "persistence".</param>
	/// <param name="failureStatus">The failure status. Default is <see langword="null"/> (context default).</param>
	/// <param name="tags">Optional tags for filtering health checks.</param>
	/// <returns>The health checks builder for chaining.</returns>
	public static IHealthChecksBuilder AddPersistenceHealthCheck(
		this IHealthChecksBuilder builder,
		string providerKey,
		string name = "persistence",
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);

		// PersistenceHealthCheck takes a non-injectable string providerKey, so construct it explicitly
		// rather than via ActivatorUtilities.
		return builder.Add(new HealthCheckRegistration(
			name,
			sp => new PersistenceHealthCheck(
				sp,
				providerKey,
				sp.GetRequiredService<ILogger<PersistenceHealthCheck>>()),
			failureStatus,
			tags));
	}
}
