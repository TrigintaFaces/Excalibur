// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Dispatch.Patterns.ClaimCheck;

/// <summary>
/// Health check for the Claim Check provider subsystem.
/// </summary>
/// <remarks>
/// <para>
/// Verifies the underlying <see cref="IClaimCheckProvider"/> is operational by performing
/// a store-retrieve-delete round-trip with a small probe payload. Reports:
/// </para>
/// <list type="bullet">
///   <item><b>Healthy:</b> Round-trip completed successfully and data integrity verified</item>
///   <item><b>Degraded:</b> Provider is registered but no provider instance is available</item>
///   <item><b>Unhealthy:</b> Round-trip failed or data integrity check failed</item>
/// </list>
/// </remarks>
public sealed class ClaimCheckHealthCheck : IHealthCheck
{
	private static readonly byte[] ProbePayload = "healthcheck-probe"u8.ToArray();

	private readonly IClaimCheckProvider _provider;

	/// <summary>
	/// Initializes a new instance of the <see cref="ClaimCheckHealthCheck"/> class.
	/// </summary>
	/// <param name="provider">The claim check provider to health-check.</param>
	public ClaimCheckHealthCheck(IClaimCheckProvider provider)
	{
		_provider = provider ?? throw new ArgumentNullException(nameof(provider));
	}

	/// <inheritdoc />
	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken = default)
	{
		try
		{
			// Store a small probe payload
			var metadata = new ClaimCheckMetadata
			{
				MessageType = "HealthCheckProbe",
				ContentType = "application/octet-stream",
			};

			var reference = await _provider.StoreAsync(ProbePayload, cancellationToken, metadata).ConfigureAwait(false);

			// Retrieve and verify data integrity
			var retrieved = await _provider.RetrieveAsync(reference, cancellationToken).ConfigureAwait(false);

			if (!retrieved.AsSpan().SequenceEqual(ProbePayload))
			{
				return HealthCheckResult.Unhealthy(
					"Claim check data integrity check failed: retrieved payload does not match stored payload.");
			}

			// Clean up the probe
			_ = await _provider.DeleteAsync(reference, cancellationToken).ConfigureAwait(false);

			return HealthCheckResult.Healthy("Claim check provider is operational.");
		}
		catch (Exception ex)
		{
			return HealthCheckResult.Unhealthy(
				"Claim check provider health check failed.",
				exception: ex);
		}
	}
}
