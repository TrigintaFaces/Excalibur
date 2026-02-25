// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring compliance monitoring services.
/// </summary>
public static class MonitoringServiceCollectionExtensions
{
	/// <summary>
	/// Adds compliance metrics services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddComplianceMetrics();
	///
	/// // Later, inject IComplianceMetrics
	/// public class MyService
	/// {
	///     public MyService(IComplianceMetrics metrics)
	///     {
	///         metrics.RecordKeyRotation("my-key", "AzureKeyVault");
	///     }
	/// }
	/// </code>
	/// </example>
	public static IServiceCollection AddComplianceMetrics(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<ComplianceMetrics>();
		services.TryAddSingleton<IComplianceMetrics>(sp => sp.GetRequiredService<ComplianceMetrics>());

		return services;
	}

	/// <summary>
	/// Adds key rotation alert services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Optional action to configure alert options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddKeyRotationAlerts(options =>
	/// {
	///     options.AlertAfterFailures = 3;
	///     options.ExpirationWarningDays = 7;
	///     options.NotifyOnSuccess = true;
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddKeyRotationAlerts(
		this IServiceCollection services,
		Action<KeyRotationAlertOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var options = new KeyRotationAlertOptions();
		configureOptions?.Invoke(options);

		services.TryAddSingleton(options);
		services.TryAddSingleton<KeyRotationAlertService>();
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IKeyRotationAlertHandler, LoggingAlertHandler>());

		return services;
	}

	/// <summary>
	/// Adds a custom key rotation alert handler.
	/// </summary>
	/// <typeparam name="THandler">The handler type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddKeyRotationAlerts()
	///         .AddKeyRotationAlertHandler&lt;SlackAlertHandler&gt;()
	///         .AddKeyRotationAlertHandler&lt;PagerDutyAlertHandler&gt;();
	/// </code>
	/// </example>
	public static IServiceCollection AddKeyRotationAlertHandler<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	THandler>(this IServiceCollection services)
		where THandler : class, IKeyRotationAlertHandler
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddSingleton<IKeyRotationAlertHandler, THandler>();

		return services;
	}

	/// <summary>
	/// Adds all compliance monitoring services including metrics and alerts.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureAlertOptions">Optional action to configure alert options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddComplianceMonitoring(options =>
	/// {
	///     options.AlertAfterFailures = 2;
	///     options.ExpirationWarningDays = 14;
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddComplianceMonitoring(
		this IServiceCollection services,
		Action<KeyRotationAlertOptions>? configureAlertOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddComplianceMetrics();
		_ = services.AddKeyRotationAlerts(configureAlertOptions);

		return services;
	}
}
