// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Configuration;
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
			.ValidateOnStart();
		if (configureOptions is not null)
		{
			_ = optionsBuilder.Configure(configureOptions);
		}

		services.TryAddScoped<IDataPortabilityService, DataPortabilityService>();
		return services;
	}

	/// <summary>
	/// Adds data portability export services using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="DataPortabilityOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddDataPortability(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<DataPortabilityOptions>()
			.Bind(configuration)
			.ValidateOnStart();

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
			.ValidateOnStart();
		if (configureOptions is not null)
		{
			_ = optionsBuilder.Configure(configureOptions);
		}

		services.TryAddScoped<ISubjectAccessService, SubjectAccessService>();
		return services;
	}

	/// <summary>
	/// Adds subject access request services using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="SubjectAccessOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddSubjectAccessRequests(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<SubjectAccessOptions>()
			.Bind(configuration)
			.ValidateOnStart();

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
			.ValidateOnStart();
		if (configureOptions is not null)
		{
			_ = optionsBuilder.Configure(configureOptions);
		}

		services.TryAddScoped<IAuditLogEncryptor, AuditLogEncryptionService>();
		return services;
	}

	/// <summary>
	/// Adds audit log encryption at rest services using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="AuditLogEncryptionOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddAuditLogEncryption(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<AuditLogEncryptionOptions>()
			.Bind(configuration)
			.ValidateOnStart();

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
			.ValidateOnStart();
		if (configureOptions is not null)
		{
			_ = optionsBuilder.Configure(configureOptions);
		}

		services.TryAddSingleton<IKeyEscrowService, KeyEscrowBackupService>();
		return services;
	}

	/// <summary>
	/// Adds key escrow and backup services using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="KeyEscrowBackupOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddKeyEscrow(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<KeyEscrowBackupOptions>()
			.Bind(configuration)
			.ValidateOnStart();

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
			.ValidateOnStart();
		if (configureOptions is not null)
		{
			_ = optionsBuilder.Configure(configureOptions);
		}

		services.TryAddSingleton<IBreachNotificationService, BreachNotificationService>();
		return services;
	}

	/// <summary>
	/// Adds breach notification services using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="BreachNotificationOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddBreachNotification(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<BreachNotificationOptions>()
			.Bind(configuration)
			.ValidateOnStart();

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
	/// Adds retention enforcement services using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="RetentionEnforcementOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddRetentionEnforcement(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<RetentionEnforcementOptions>()
			.Bind(configuration)
			.ValidateOnStart();

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
			.ValidateOnStart();
		if (configureOptions is not null)
		{
			_ = optionsBuilder.Configure(configureOptions);
		}

		services.TryAddSingleton<IConsentService, ConsentService>();
		return services;
	}

	/// <summary>
	/// Adds consent management services using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="ConsentOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddConsentManagement(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<ConsentOptions>()
			.Bind(configuration)
			.ValidateOnStart();

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
			.ValidateOnStart();
		_ = optionsBuilder.Configure(configureOptions);

		services.TryAddSingleton<IComplianceStore, PostgresComplianceStore>();
		return services;
	}

	/// <summary>
	/// Adds the Postgres compliance store using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="PostgresComplianceOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddPostgresComplianceStore(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<PostgresComplianceOptions>()
			.Bind(configuration)
			.ValidateOnStart();

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
			.ValidateOnStart();
		_ = optionsBuilder.Configure(configureOptions);

		services.TryAddSingleton<IComplianceStore, MongoDbComplianceStore>();
		return services;
	}

	/// <summary>
	/// Adds the MongoDB compliance store using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="MongoDbComplianceOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddMongoDbComplianceStore(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<MongoDbComplianceOptions>()
			.Bind(configuration)
			.ValidateOnStart();

		services.TryAddSingleton<IComplianceStore, MongoDbComplianceStore>();
		return services;
	}
}
