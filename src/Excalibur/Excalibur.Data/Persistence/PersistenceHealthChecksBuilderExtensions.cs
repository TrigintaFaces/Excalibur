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
	/// Adds a health check that probes a named persistence provider's connectivity via the
	/// registered <see cref="IPersistenceProviderFactory"/>.
	/// </summary>
	/// <param name="builder">The health checks builder.</param>
	/// <param name="providerName">The persistence provider name to probe.</param>
	/// <param name="name">The health check name. Default is "persistence".</param>
	/// <param name="failureStatus">The failure status. Default is <see langword="null"/> (context default).</param>
	/// <param name="tags">Optional tags for filtering health checks.</param>
	/// <returns>The health checks builder for chaining.</returns>
	public static IHealthChecksBuilder AddPersistenceHealthCheck(
		this IHealthChecksBuilder builder,
		string providerName,
		string name = "persistence",
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

		// PersistenceHealthCheck takes a non-injectable string providerName, so construct it explicitly
		// rather than via ActivatorUtilities.
		return builder.Add(new HealthCheckRegistration(
			name,
			sp => new PersistenceHealthCheck(
				sp.GetRequiredService<IPersistenceProviderFactory>(),
				sp.GetRequiredService<ILogger<PersistenceHealthCheck>>(),
				providerName),
			failureStatus,
			tags));
	}
}
