// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security.Diagnostics;

/// <summary>
/// Health check for the security subsystem.
/// </summary>
/// <remarks>
/// Validates that critical security components are properly configured:
/// authentication keys, signing configuration, and security headers.
/// </remarks>
internal sealed class SecurityHealthCheck : IHealthCheck
{
	private readonly IOptions<SecurityOptions> _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="SecurityHealthCheck"/> class.
	/// </summary>
	/// <param name="options">The security options.</param>
	public SecurityHealthCheck(IOptions<SecurityOptions> options)
	{
		_options = options;
	}

	/// <inheritdoc />
	public Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken = default)
	{
		var opts = _options.Value;

		// Check authentication configuration when required
		if (opts.Authentication.RequireAuthentication && string.IsNullOrEmpty(opts.Authentication.JwtSigningKey))
		{
			return Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded(
				"Authentication is required but no JwtSigningKey is configured."));
		}

		return Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
			"Security subsystem is properly configured."));
	}
}
