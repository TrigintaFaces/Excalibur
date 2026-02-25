// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for adding transport health checks to <see cref="IHealthChecksBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extension methods integrate Dispatch transport health monitoring with the
/// ASP.NET Core health checks system (<c>Microsoft.Extensions.Diagnostics.HealthChecks</c>).
/// </para>
/// <para>
/// Usage example:
/// <code>
/// services.AddHealthChecks()
///     .AddTransportHealthChecks();
/// </code>
/// </para>
/// </remarks>
public static class TransportHealthChecksBuilderExtensions
{
	/// <summary>
	/// The default health check name for transport monitoring.
	/// </summary>
	public const string DefaultHealthCheckName = "transports";

	/// <summary>
	/// Adds health checks for all registered transport adapters.
	/// </summary>
	/// <param name="builder">The <see cref="IHealthChecksBuilder"/> to add the health check to.</param>
	/// <param name="name">
	/// The name of the health check. Defaults to "transports".
	/// </param>
	/// <param name="failureStatus">
	/// The <see cref="HealthStatus"/> to report when the health check fails.
	/// Defaults to <see cref="HealthStatus.Unhealthy"/>.
	/// </param>
	/// <param name="tags">
	/// Optional tags for the health check. If not specified, defaults to "transport", "messaging", "ready".
	/// </param>
	/// <param name="timeout">
	/// Optional timeout for the health check execution.
	/// </param>
	/// <returns>The <see cref="IHealthChecksBuilder"/> for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This health check monitors all transports registered in the <see cref="TransportRegistry"/>
	/// and reports aggregate health status:
	/// </para>
	/// <list type="bullet">
	/// <item><description><b>Healthy</b>: All transports are running and healthy.</description></item>
	/// <item><description><b>Degraded</b>: At least one transport is not healthy, but the default transport is healthy.</description></item>
	/// <item><description><b>Unhealthy</b>: No transports registered, default transport not running, or critical failures.</description></item>
	/// </list>
	/// <para>
	/// The health check response includes detailed data about each transport's status,
	/// including transport type, running state, and any health checker-specific information.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Basic usage
	/// services.AddHealthChecks()
	///     .AddTransportHealthChecks();
	///
	/// // Custom configuration
	/// services.AddHealthChecks()
	///     .AddTransportHealthChecks(
	///         name: "message-transports",
	///         failureStatus: HealthStatus.Degraded,
	///         tags: ["transport", "critical"],
	///         timeout: TimeSpan.FromSeconds(10));
	/// </code>
	/// </example>
	public static IHealthChecksBuilder AddTransportHealthChecks(
		this IHealthChecksBuilder builder,
		string name = DefaultHealthCheckName,
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null,
		TimeSpan? timeout = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		var defaultTags = new[] { "transport", "messaging", "ready" };

		return builder.Add(new HealthCheckRegistration(
			name,
			sp =>
			{
				var registry = sp.GetRequiredService<TransportRegistry>();
				var options = sp.GetService<MultiTransportHealthCheckOptions>();
				return new MultiTransportHealthCheck(registry, options);
			},
			failureStatus,
			tags ?? defaultTags,
			timeout));
	}

	/// <summary>
	/// Adds health checks for all registered transport adapters with custom options.
	/// </summary>
	/// <param name="builder">The <see cref="IHealthChecksBuilder"/> to add the health check to.</param>
	/// <param name="configureOptions">An action to configure the health check options.</param>
	/// <param name="name">
	/// The name of the health check. Defaults to "transports".
	/// </param>
	/// <param name="failureStatus">
	/// The <see cref="HealthStatus"/> to report when the health check fails.
	/// Defaults to <see cref="HealthStatus.Unhealthy"/>.
	/// </param>
	/// <param name="tags">
	/// Optional tags for the health check. If not specified, defaults to "transport", "messaging", "ready".
	/// </param>
	/// <param name="timeout">
	/// Optional timeout for the health check execution.
	/// </param>
	/// <returns>The <see cref="IHealthChecksBuilder"/> for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Use this overload to customize health check behavior:
	/// </para>
	/// <list type="bullet">
	/// <item><description><c>RequireAtLeastOneTransport</c>: Whether to report unhealthy when no transports are registered.</description></item>
	/// <item><description><c>RequireDefaultTransportHealthy</c>: Whether the default transport must be healthy for overall healthy status.</description></item>
	/// </list>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddHealthChecks()
	///     .AddTransportHealthChecks(options =>
	///     {
	///         options.RequireAtLeastOneTransport = true;
	///         options.RequireDefaultTransportHealthy = true;
	///     });
	/// </code>
	/// </example>
	public static IHealthChecksBuilder AddTransportHealthChecks(
		this IHealthChecksBuilder builder,
		Action<MultiTransportHealthCheckOptions> configureOptions,
		string name = DefaultHealthCheckName,
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null,
		TimeSpan? timeout = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configureOptions);

		var options = new MultiTransportHealthCheckOptions();
		configureOptions(options);

		var defaultTags = new[] { "transport", "messaging", "ready" };

		return builder.Add(new HealthCheckRegistration(
			name,
			sp =>
			{
				var registry = sp.GetRequiredService<TransportRegistry>();
				return new MultiTransportHealthCheck(registry, options);
			},
			failureStatus,
			tags ?? defaultTags,
			timeout));
	}
}
