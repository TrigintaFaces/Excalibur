// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Security.Diagnostics;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Provides extension methods to register security health checks.</summary>
public static class SecurityHealthChecksBuilderExtensions
{
	private static readonly string[] DefaultTags = ["excalibur", "security"];

	/// <summary>Adds the security health check.</summary>
	/// <param name="builder">The health checks builder.</param>
	/// <param name="name">The health check name.</param>
	/// <param name="failureStatus">Optional failure status.</param>
	/// <param name="tags">Optional tags for filtering.</param>
	/// <returns>The health checks builder for chaining.</returns>
	public static IHealthChecksBuilder AddSecurityHealthCheck(
		this IHealthChecksBuilder builder,
		string name = "security",
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		tags ??= DefaultTags;
		return builder.Add(new HealthCheckRegistration(
			name,
			sp => ActivatorUtilities.CreateInstance<SecurityHealthCheck>(sp),
			failureStatus,
			tags));
	}
}
