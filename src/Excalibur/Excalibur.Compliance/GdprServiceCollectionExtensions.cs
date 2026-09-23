// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Compliance;
using Excalibur.Compliance.Audit;
using Excalibur.Compliance.Breach;
using Excalibur.Compliance.Consent;
using Excalibur.Compliance.Erasure;
using Excalibur.Compliance.KeyManagement;
using Excalibur.Compliance.Portability;
using Excalibur.Compliance.Retention;
using Excalibur.Compliance.SubjectAccess;

using Excalibur.Dispatch;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

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
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DataPortabilityOptions>, DataPortabilityOptionsValidator>());
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
	public static IServiceCollection AddDataPortability(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<DataPortabilityOptions>()
			.Bind(configuration)
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DataPortabilityOptions>, DataPortabilityOptionsValidator>());

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
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SubjectAccessOptions>, SubjectAccessOptionsValidator>());
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
	public static IServiceCollection AddSubjectAccessRequests(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<SubjectAccessOptions>()
			.Bind(configuration)
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SubjectAccessOptions>, SubjectAccessOptionsValidator>());

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
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<AuditLogEncryptionOptions>, AuditLogEncryptionOptionsValidator>());
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
	public static IServiceCollection AddAuditLogEncryption(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<AuditLogEncryptionOptions>()
			.Bind(configuration)
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<AuditLogEncryptionOptions>, AuditLogEncryptionOptionsValidator>());

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
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<KeyEscrowBackupOptions>, KeyEscrowBackupOptionsValidator>());
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
	public static IServiceCollection AddKeyEscrow(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<KeyEscrowBackupOptions>()
			.Bind(configuration)
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<KeyEscrowBackupOptions>, KeyEscrowBackupOptionsValidator>());

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
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<BreachNotificationOptions>, BreachNotificationOptionsValidator>());
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
	public static IServiceCollection AddBreachNotification(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<BreachNotificationOptions>()
			.Bind(configuration)
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<BreachNotificationOptions>, BreachNotificationOptionsValidator>());

		services.TryAddSingleton<IBreachNotificationService, BreachNotificationService>();
		return services;
	}

	/// <summary>
	/// Adds retention enforcement: a background pass that hands the declared retention policies to the
	/// registered <see cref="IRetentionContributor"/> implementations.
	/// </summary>
	/// <remarks>
	/// Only types declared with <see cref="AddRetentionPolicies{T}(IServiceCollection)"/> or
	/// <see cref="AddRetentionPoliciesFromAssembly(IServiceCollection, Assembly)"/> are in retention scope.
	/// With enforcement enabled and nothing declared, host startup fails, unless the only registered
	/// contributors are the built-in outbox and inbox ones (which delete by their own age bound and do not read
	/// the declared policies). Enforcement never falls back to scanning loaded assemblies.
	/// </remarks>
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
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<RetentionEnforcementOptions>, RetentionEnforcementOptionsValidator>());
		if (configureOptions is not null)
		{
			_ = optionsBuilder.Configure(configureOptions);
		}

		return AddRetentionEnforcementCore(services);
	}

	/// <summary>
	/// Adds retention enforcement services using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="RetentionEnforcementOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddRetentionEnforcement(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<RetentionEnforcementOptions>()
			.Bind(configuration)
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<RetentionEnforcementOptions>, RetentionEnforcementOptionsValidator>());

		return AddRetentionEnforcementCore(services);
	}

	/// <summary>
	/// Declares <typeparamref name="T"/> in retention scope: the <see cref="PersonalDataAttribute.RetentionDays"/>
	/// of its annotated public properties become retention policies handed to the registered contributors.
	/// </summary>
	/// <typeparam name="T">A type with at least one <see cref="PersonalDataAttribute"/> property whose
	/// <see cref="PersonalDataAttribute.RetentionDays"/> is positive.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentException"><typeparamref name="T"/> declares no retention period.</exception>
	public static IServiceCollection AddRetentionPolicies<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
		this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddSingleton(RetentionPolicyDeclaration.ForType(typeof(T)));
		return services;
	}

	/// <summary>
	/// Declares every type in <paramref name="assembly"/> in retention scope: the
	/// <see cref="PersonalDataAttribute.RetentionDays"/> of their annotated public properties become retention
	/// policies handed to the registered contributors. Types in other assemblies are not affected.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="assembly">The assembly whose types are placed in retention scope.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentException"><paramref name="assembly"/> declares no retention period.</exception>
	[RequiresUnreferencedCode("Enumerates the types of the assembly and reads their public properties, which trimming may remove. Use AddRetentionPolicies<T>() in trimmed applications.")]
	public static IServiceCollection AddRetentionPoliciesFromAssembly(
		this IServiceCollection services,
		Assembly assembly)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(assembly);

		_ = services.AddSingleton(RetentionPolicyDeclaration.ForAssembly(assembly));
		return services;
	}

	private static IServiceCollection AddRetentionEnforcementCore(IServiceCollection services)
	{
		services.TryAddSingleton(TimeProvider.System);
		services.TryAddScoped<IRetentionEnforcementService, RetentionEnforcementService>();
		if (!services.Any(sd => sd.ServiceType == typeof(RetentionEnforcementBackgroundService)))
		{
			_ = services.AddSingleton<RetentionEnforcementBackgroundService>();
			_ = services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<RetentionEnforcementBackgroundService>());
		}

		return services;
	}

	/// <summary>
	/// Registers an <see cref="IRetentionContributor"/> that deletes sent outbox messages older than
	/// <see cref="OutboxRetentionOptions.RetentionDays"/>, via the same <see cref="IOutboxStoreAdmin"/>
	/// admin surface every registered outbox provider already implements.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Optional configuration for outbox retention options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Call this in addition to <see cref="AddRetentionEnforcement(IServiceCollection, Action{RetentionEnforcementOptions}?)"/>
	/// -- retention enforcement never deletes outbox data unless this (or <c>AddInboxRetention</c>) is
	/// also registered. Requires an <see cref="IOutboxStoreAdmin"/> to already be registered (every
	/// first-party outbox provider registers one); if none is registered, resolution fails fast with a
	/// clear "unable to resolve IOutboxStoreAdmin" error rather than silently doing nothing.
	/// </remarks>
	public static IServiceCollection AddOutboxRetention(
		this IServiceCollection services,
		Action<OutboxRetentionOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var optionsBuilder = services.AddOptions<OutboxRetentionOptions>()
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<OutboxRetentionOptions>, OutboxRetentionOptionsValidator>());
		if (configureOptions is not null)
		{
			_ = optionsBuilder.Configure(configureOptions);
		}

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IRetentionContributor, OutboxRetentionContributor>());

		return services;
	}

	/// <summary>
	/// Registers an <see cref="IRetentionContributor"/> that deletes processed inbox entries older than
	/// <see cref="InboxRetentionOptions.RetentionDays"/>, via the same <see cref="IInboxStoreAdmin"/>
	/// admin surface every registered inbox provider already implements.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Optional configuration for inbox retention options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Call this in addition to <see cref="AddRetentionEnforcement(IServiceCollection, Action{RetentionEnforcementOptions}?)"/>
	/// -- retention enforcement never deletes inbox data unless this (or <c>AddOutboxRetention</c>) is
	/// also registered. Requires an <see cref="IInboxStoreAdmin"/> to already be registered (every
	/// first-party inbox provider registers one); if none is registered, resolution fails fast with a
	/// clear "unable to resolve IInboxStoreAdmin" error rather than silently doing nothing.
	/// </remarks>
	public static IServiceCollection AddInboxRetention(
		this IServiceCollection services,
		Action<InboxRetentionOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var optionsBuilder = services.AddOptions<InboxRetentionOptions>()
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<InboxRetentionOptions>, InboxRetentionOptionsValidator>());
		if (configureOptions is not null)
		{
			_ = optionsBuilder.Configure(configureOptions);
		}

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IRetentionContributor, InboxRetentionContributor>());

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
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ConsentOptions>, ConsentOptionsValidator>());
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
	public static IServiceCollection AddConsentManagement(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<ConsentOptions>()
			.Bind(configuration)
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ConsentOptions>, ConsentOptionsValidator>());

		services.TryAddSingleton<IConsentService, ConsentService>();
		return services;
	}
}
