// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Transport.Google;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for adding streaming pull health checks.
/// </summary>
public static class StreamingPullHealthCheckExtensions
{
	/// <summary>
	/// Adds a streaming pull health check to the health checks builder.
	/// </summary>
	/// <param name="builder"> The health checks builder. </param>
	/// <param name="name"> The name of the health check. Default is "streaming-pull". </param>
	/// <param name="failureStatus">
	/// The health status to report when the check fails. If <see langword="null" />, the default failure status is used.
	/// </param>
	/// <param name="tags"> Optional tags to associate with the health check. </param>
	/// <returns> The health checks builder for chaining. </returns>
	/// <remarks>
	/// <para>
	/// This health check requires a <see cref="StreamHealthMonitor" /> to be registered in DI.
	/// Use <c>AddGooglePubSubStreamingPull</c> to register the required services.
	/// </para>
	/// <para>
	/// Example usage:
	/// <code>
	/// services.AddGooglePubSubStreamingPull(configuration);
	/// services.AddHealthChecks()
	///     .AddStreamingPullHealthCheck();
	/// </code>
	/// </para>
	/// </remarks>
	public static IHealthChecksBuilder AddStreamingPullHealthCheck(
		this IHealthChecksBuilder builder,
		string name = "streaming-pull",
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.Add(new HealthCheckRegistration(
			name,
			sp => new StreamingPullHealthCheck(
				sp.GetRequiredService<StreamHealthMonitor>()),
			failureStatus,
			tags));
	}
}
