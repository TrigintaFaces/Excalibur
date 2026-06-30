// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Provides extension methods to register Dispatch core health checks.</summary>
public static class DispatchCoreHealthChecksBuilderExtensions
{
	/// <summary>Adds the serialization health check.</summary>
	/// <param name="builder">The health checks builder.</param>
	/// <param name="name">The health check name.</param>
	/// <param name="failureStatus">Optional failure status.</param>
	/// <param name="tags">Optional tags for filtering.</param>
	/// <returns>The health checks builder for chaining.</returns>
	public static IHealthChecksBuilder AddSerializationHealthCheck(
		this IHealthChecksBuilder builder,
		string name = "serialization",
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		tags ??= DefaultTags;
		return builder.Add(new HealthCheckRegistration(
			name,
			sp => ActivatorUtilities.CreateInstance<SerializationHealthCheck>(sp),
			failureStatus,
			tags));
	}

	/// <summary>Adds the pipeline integrity health check.</summary>
	/// <param name="builder">The health checks builder.</param>
	/// <param name="name">The health check name.</param>
	/// <param name="failureStatus">Optional failure status.</param>
	/// <param name="tags">Optional tags for filtering.</param>
	/// <returns>The health checks builder for chaining.</returns>
	public static IHealthChecksBuilder AddPipelineIntegrityHealthCheck(
		this IHealthChecksBuilder builder,
		string name = "pipeline-integrity",
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		tags ??= DefaultTags;
		return builder.Add(new HealthCheckRegistration(
			name,
			sp => ActivatorUtilities.CreateInstance<PipelineIntegrityHealthCheck>(sp),
			failureStatus,
			tags));
	}

	/// <summary>Adds the streaming handler health check.</summary>
	/// <param name="builder">The health checks builder.</param>
	/// <param name="name">The health check name.</param>
	/// <param name="failureStatus">Optional failure status.</param>
	/// <param name="tags">Optional tags for filtering.</param>
	/// <returns>The health checks builder for chaining.</returns>
	public static IHealthChecksBuilder AddStreamingHandlerHealthCheck(
		this IHealthChecksBuilder builder,
		string name = "streaming-handler",
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		tags ??= DefaultTags;
		return builder.Add(new HealthCheckRegistration(
			name,
			sp => ActivatorUtilities.CreateInstance<StreamingHandlerHealthCheck>(sp),
			failureStatus,
			tags));
	}

	/// <summary>Adds all Dispatch core health checks (serialization, pipeline integrity, streaming handler).</summary>
	/// <param name="builder">The health checks builder.</param>
	/// <returns>The health checks builder for chaining.</returns>
	public static IHealthChecksBuilder AddDispatchCoreHealthChecks(this IHealthChecksBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return builder
			.AddSerializationHealthCheck()
			.AddPipelineIntegrityHealthCheck()
			.AddStreamingHandlerHealthCheck();
	}

	private static readonly string[] DefaultTags = ["excalibur", "dispatch"];
}
