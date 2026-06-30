// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Provides extension methods to register compliance health checks.</summary>
public static class ComplianceHealthChecksBuilderExtensions
{
	private static readonly string[] DefaultTags = ["excalibur", "compliance"];

	/// <summary>Adds the encryption health check.</summary>
	/// <param name="builder">The health checks builder.</param>
	/// <param name="name">The health check name.</param>
	/// <param name="failureStatus">Optional failure status.</param>
	/// <param name="tags">Optional tags for filtering.</param>
	/// <returns>The health checks builder for chaining.</returns>
	public static IHealthChecksBuilder AddEncryptionHealthCheck(
		this IHealthChecksBuilder builder,
		string name = "encryption",
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		tags ??= DefaultTags;
		return builder.Add(new HealthCheckRegistration(
			name,
			sp => ActivatorUtilities.CreateInstance<EncryptionHealthCheck>(sp),
			failureStatus,
			tags));
	}

	/// <summary>Adds the erasure health check.</summary>
	/// <param name="builder">The health checks builder.</param>
	/// <param name="name">The health check name.</param>
	/// <param name="failureStatus">Optional failure status.</param>
	/// <param name="tags">Optional tags for filtering.</param>
	/// <returns>The health checks builder for chaining.</returns>
	public static IHealthChecksBuilder AddErasureHealthCheck(
		this IHealthChecksBuilder builder,
		string name = "erasure",
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		tags ??= DefaultTags;
		return builder.Add(new HealthCheckRegistration(
			name,
			sp => ActivatorUtilities.CreateInstance<ErasureHealthCheck>(sp),
			failureStatus,
			tags));
	}

	/// <summary>Adds all compliance health checks (encryption, erasure).</summary>
	/// <param name="builder">The health checks builder.</param>
	/// <param name="failureStatus">Optional failure status.</param>
	/// <param name="tags">Optional tags for filtering.</param>
	/// <returns>The health checks builder for chaining.</returns>
	public static IHealthChecksBuilder AddComplianceHealthChecks(
		this IHealthChecksBuilder builder,
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return builder
			.AddEncryptionHealthCheck(failureStatus: failureStatus, tags: tags)
			.AddErasureHealthCheck(failureStatus: failureStatus, tags: tags);
	}
}
