// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Data.InMemory;

/// <summary>
/// Health check for InMemory persistence provider.
/// </summary>
/// <remarks>
/// The InMemory provider is always available (no external connectivity required).
/// This health check verifies the provider is initialized and reports basic state.
/// </remarks>
internal sealed class InMemoryHealthCheck : IHealthCheck
{
	private readonly InMemoryPersistenceProvider _provider;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryHealthCheck"/> class.
	/// </summary>
	/// <param name="provider">The InMemory persistence provider.</param>
	public InMemoryHealthCheck(InMemoryPersistenceProvider provider)
	{
		_provider = provider ?? throw new ArgumentNullException(nameof(provider));
	}

	/// <inheritdoc/>
	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var connected = await _provider.TestConnectionAsync(cancellationToken).ConfigureAwait(false);

			var data = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["provider"] = _provider.Name,
				["providerType"] = _provider.ProviderType,
			};

			return connected
				? HealthCheckResult.Healthy("InMemory provider is healthy.", data)
				: HealthCheckResult.Unhealthy("InMemory provider is not available.");
		}
		catch (Exception ex)
		{
			return HealthCheckResult.Unhealthy(
				$"InMemory health check failed: {ex.Message}",
				ex);
		}
	}
}
