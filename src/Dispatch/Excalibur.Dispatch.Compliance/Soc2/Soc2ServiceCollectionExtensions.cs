// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering SOC 2 compliance services.
/// </summary>
public static class Soc2ServiceCollectionExtensions
{
	/// <summary>
	/// Adds SOC 2 compliance services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Optional configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSoc2Compliance(
		this IServiceCollection services,
		Action<Soc2Options>? configureOptions = null)
	{
		// Configure options
		var optionsBuilder = services.AddOptions<Soc2Options>();
		if (configureOptions != null)
		{
			_ = optionsBuilder.Configure(configureOptions);
		}

		_ = optionsBuilder.ValidateDataAnnotations().ValidateOnStart();

		// Register core services
		_ = services.AddScoped<ISoc2ComplianceService, Soc2ComplianceService>();
		_ = services.AddScoped<IControlValidationService, ControlValidationService>();

		// Register report generation services
		_ = services.AddScoped<ISoc2ReportGenerator, Soc2ReportGenerator>();

		// Register export services
		_ = services.AddScoped<ISoc2ReportExporter, Soc2ReportExporter>();

		return services;
	}

	/// <summary>
	/// Adds the in-memory SOC 2 report store for development and testing.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// The in-memory store is NOT suitable for production use as data is lost
	/// when the application restarts. Use a persistent implementation for production.
	/// </remarks>
	public static IServiceCollection AddInMemorySoc2ReportStore(this IServiceCollection services)
	{
		_ = services.AddSingleton<ISoc2ReportStore, InMemorySoc2ReportStore>();
		return services;
	}

	/// <summary>
	/// Adds a custom SOC 2 report store implementation.
	/// </summary>
	/// <typeparam name="TStore">The store implementation type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSoc2ReportStore<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TStore>(this IServiceCollection services)
		where TStore : class, ISoc2ReportStore
	{
		_ = services.AddScoped<ISoc2ReportStore, TStore>();
		return services;
	}

	/// <summary>
	/// Adds a control validator to the service collection.
	/// </summary>
	/// <typeparam name="TValidator">The validator type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddControlValidator<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TValidator>(this IServiceCollection services)
		where TValidator : class, IControlValidator
	{
		_ = services.AddSingleton<IControlValidator, TValidator>();
		return services;
	}

	/// <summary>
	/// Adds SOC 2 compliance with all built-in validators.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Optional configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSoc2ComplianceWithBuiltInValidators(
		this IServiceCollection services,
		Action<Soc2Options>? configureOptions = null)
	{
		_ = services.AddSoc2Compliance(configureOptions);

		// Register built-in validators
		_ = services.AddSingleton<IControlValidator, EncryptionControlValidator>();
		_ = services.AddSingleton<IControlValidator, AuditLogControlValidator>();
		_ = services.AddSingleton<IControlValidator, AvailabilityControlValidator>();
		_ = services.AddSingleton<IControlValidator, ProcessingIntegrityControlValidator>();
		_ = services.AddSingleton<IControlValidator, ConfidentialityControlValidator>();

		return services;
	}

	/// <summary>
	/// Adds continuous compliance monitoring as a background service.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This enables continuous monitoring that:
	/// </para>
	/// <list type="bullet">
	/// <item><description>Runs control validation on a configurable interval</description></item>
	/// <item><description>Detects compliance gaps and status changes</description></item>
	/// <item><description>Generates alerts for gaps exceeding severity threshold</description></item>
	/// </list>
	/// <para>
	/// If no <see cref="IComplianceAlertHandler"/> is registered, a default logging
	/// handler will be used. Register your own handler(s) for integration with
	/// SIEM, PagerDuty, email, Slack, etc.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSoc2ContinuousMonitoring(this IServiceCollection services)
	{
		// Add default logging handler if no handler is registered
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IComplianceAlertHandler, LoggingComplianceAlertHandler>());

		// Add background service
		_ = services.AddHostedService<ComplianceMonitoringService>();

		return services;
	}

	/// <summary>
	/// Adds a custom compliance alert handler.
	/// </summary>
	/// <typeparam name="THandler">The handler implementation type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Multiple handlers can be registered. All handlers will receive alerts.
	/// </remarks>
	public static IServiceCollection AddComplianceAlertHandler<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	THandler>(this IServiceCollection services)
		where THandler : class, IComplianceAlertHandler
	{
		_ = services.AddSingleton<IComplianceAlertHandler, THandler>();
		return services;
	}

	/// <summary>
	/// Adds SOC 2 compliance with built-in validators and continuous monitoring.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Optional configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// This is a convenience method that combines:
	/// <list type="bullet">
	/// <item><description><see cref="AddSoc2ComplianceWithBuiltInValidators"/></description></item>
	/// <item><description><see cref="AddSoc2ContinuousMonitoring"/></description></item>
	/// </list>
	/// </remarks>
	public static IServiceCollection AddSoc2ComplianceWithMonitoring(
		this IServiceCollection services,
		Action<Soc2Options>? configureOptions = null)
	{
		_ = services.AddSoc2ComplianceWithBuiltInValidators(configureOptions);
		_ = services.AddSoc2ContinuousMonitoring();
		return services;
	}
}
