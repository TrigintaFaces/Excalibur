// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering GDPR compliance services with dependency injection.
/// </summary>
public static class GdprServiceCollectionExtensions
{
	/// <summary>
	/// Adds cascade erasure services for GDPR Article 17 with relationship graph traversal.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Requires an <see cref="IErasureService"/> and <see cref="ICascadeRelationshipResolver"/>
	/// to be registered.
	/// </remarks>
	public static IServiceCollection AddCascadeErasure(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);
		services.TryAddScoped<ICascadeErasureService, CascadeErasureService>();
		return services;
	}

	/// <summary>
	/// Adds data portability export services for GDPR Article 20.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Optional configuration for data portability options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddDataPortability(
		this IServiceCollection services,
		Action<DataPortabilityOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var optionsBuilder = services.AddOptions<DataPortabilityOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();
		if (configureOptions is not null)
		{
			_ = optionsBuilder.Configure(configureOptions);
		}

		services.TryAddScoped<IDataPortabilityService, DataPortabilityService>();
		return services;
	}

	/// <summary>
	/// Adds subject access request services for GDPR Article 15.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Optional configuration for subject access options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSubjectAccessRequests(
		this IServiceCollection services,
		Action<SubjectAccessOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var optionsBuilder = services.AddOptions<SubjectAccessOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();
		if (configureOptions is not null)
		{
			_ = optionsBuilder.Configure(configureOptions);
		}

		services.TryAddScoped<ISubjectAccessService, SubjectAccessService>();
		return services;
	}

	/// <summary>
	/// Adds audit log encryption at rest services.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Optional configuration for audit log encryption options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Requires an <see cref="IEncryptionProvider"/> and <see cref="IKeyManagementProvider"/>
	/// to be registered.
	/// </remarks>
	public static IServiceCollection AddAuditLogEncryption(
		this IServiceCollection services,
		Action<AuditLogEncryptionOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var optionsBuilder = services.AddOptions<AuditLogEncryptionOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();
		if (configureOptions is not null)
		{
			_ = optionsBuilder.Configure(configureOptions);
		}

		services.TryAddScoped<IAuditLogEncryptor, AuditLogEncryptionService>();
		return services;
	}

	/// <summary>
	/// Adds key escrow and backup services with Shamir's Secret Sharing.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Optional configuration for key escrow options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Requires an <see cref="IEncryptionProvider"/> and <see cref="IKeyManagementProvider"/>
	/// to be registered.
	/// </remarks>
	public static IServiceCollection AddKeyEscrow(
		this IServiceCollection services,
		Action<KeyEscrowBackupOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var optionsBuilder = services.AddOptions<KeyEscrowBackupOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();
		if (configureOptions is not null)
		{
			_ = optionsBuilder.Configure(configureOptions);
		}

		services.TryAddSingleton<IKeyEscrowService, KeyEscrowBackupService>();
		return services;
	}

	/// <summary>
	/// Adds breach notification services for GDPR Article 33/34.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Optional configuration for breach notification options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddBreachNotification(
		this IServiceCollection services,
		Action<BreachNotificationOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var optionsBuilder = services.AddOptions<BreachNotificationOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();
		if (configureOptions is not null)
		{
			_ = optionsBuilder.Configure(configureOptions);
		}

		services.TryAddSingleton<IBreachNotificationService, BreachNotificationService>();
		return services;
	}

	/// <summary>
	/// Adds retention enforcement services with background scanning.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Optional configuration for retention enforcement options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddRetentionEnforcement(
		this IServiceCollection services,
		Action<RetentionEnforcementOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var optionsBuilder = services.AddOptions<RetentionEnforcementOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();
		if (configureOptions is not null)
		{
			_ = optionsBuilder.Configure(configureOptions);
		}

		services.TryAddScoped<IRetentionEnforcementService, RetentionEnforcementService>();
		_ = services.AddSingleton<RetentionEnforcementBackgroundService>();
		_ = services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<RetentionEnforcementBackgroundService>());
		return services;
	}

	/// <summary>
	/// Adds consent management services for GDPR Article 7.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Optional configuration for consent options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddConsentManagement(
		this IServiceCollection services,
		Action<ConsentOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var optionsBuilder = services.AddOptions<ConsentOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();
		if (configureOptions is not null)
		{
			_ = optionsBuilder.Configure(configureOptions);
		}

		services.TryAddSingleton<IConsentService, ConsentService>();
		return services;
	}

	/// <summary>
	/// Adds the Postgres compliance store for durable GDPR record storage.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Configuration for Postgres compliance options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddPostgresComplianceStore(
		this IServiceCollection services,
		Action<PostgresComplianceOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		var optionsBuilder = services.AddOptions<PostgresComplianceOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();
		_ = optionsBuilder.Configure(configureOptions);

		services.TryAddSingleton<IComplianceStore, PostgresComplianceStore>();
		return services;
	}

	/// <summary>
	/// Adds the MongoDB compliance store for durable GDPR record storage.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Configuration for MongoDB compliance options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMongoDbComplianceStore(
		this IServiceCollection services,
		Action<MongoDbComplianceOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		var optionsBuilder = services.AddOptions<MongoDbComplianceOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();
		_ = optionsBuilder.Configure(configureOptions);

		services.TryAddSingleton<IComplianceStore, MongoDbComplianceStore>();
		return services;
	}
}
