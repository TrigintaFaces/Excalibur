// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Data.Abstractions.Persistence;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.SqlServer.Persistence;

/// <summary>
/// Extension methods for configuring SQL Server persistence services.
/// </summary>
public static class SqlServerPersistenceExtensions
{
	/// <summary>
	/// Adds SQL Server persistence provider to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration instance. </param>
	/// <param name="sectionName"> The configuration section name containing SQL Server settings. </param>
	/// <returns> The service collection for chaining. </returns>
	[RequiresUnreferencedCode("Configuration binding for SQL Server persistence may reference types not preserved during trimming. Ensure options types are annotated with DynamicallyAccessedMembers.")]
	[RequiresDynamicCode("Configuration binding for SQL Server persistence settings requires dynamic code generation for property reflection and value conversion.")]
	public static IServiceCollection AddSqlServerPersistence(
		this IServiceCollection services,
		IConfiguration configuration,
		string sectionName = "SqlServerPersistence")
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		// Configure options from configuration
		_ = services.AddOptions<SqlServerPersistenceOptions>()
			.Bind(configuration.GetSection(sectionName))
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register core services
		RegisterCoreServices(services);

		return services;
	}

	/// <summary>
	/// Adds SQL Server persistence provider to the service collection with explicit options.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Action to configure the persistence options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddSqlServerPersistence(
		this IServiceCollection services,
		Action<SqlServerPersistenceOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		// Configure options
		_ = services.AddOptions<SqlServerPersistenceOptions>()
			.Configure(configureOptions)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register core services
		RegisterCoreServices(services);

		return services;
	}

	/// <summary>
	/// Adds SQL Server persistence provider with connection string.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="connectionString"> The SQL Server connection string. </param>
	/// <param name="configureOptions"> Optional action to configure additional options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddSqlServerPersistence(
		this IServiceCollection services,
		string connectionString,
		Action<SqlServerPersistenceOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		_ = services.AddOptions<SqlServerPersistenceOptions>()
			.Configure(options =>
			{
				options.ConnectionString = connectionString;
				configureOptions?.Invoke(options);
			})
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register core services
		RegisterCoreServices(services);

		return services;
	}

	/// <summary>
	/// Adds SQL Server persistence health checks.
	/// </summary>
	/// <param name="builder"> The health checks builder. </param>
	/// <param name="name"> The name of the health check. </param>
	/// <param name="failureStatus"> The failure status to report. </param>
	/// <param name="tags"> Tags for the health check. </param>
	/// <param name="timeout"> The timeout for the health check. </param>
	/// <returns> The health checks builder for chaining. </returns>
	public static IHealthChecksBuilder AddSqlServerPersistenceHealthCheck(
		this IHealthChecksBuilder builder,
		string name = "sqlserver-persistence",
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null,
		TimeSpan? timeout = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.Add(new HealthCheckRegistration(
			name,
			static sp => sp.GetRequiredService<SqlServerPersistenceHealthCheck>(),
			failureStatus,
			tags,
			timeout));
	}

	/// <summary>
	/// Configures SQL Server persistence with CDC support.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration instance. </param>
	/// <param name="cdcHandlerAssemblies"> Assemblies containing CDC handlers. </param>
	/// <returns> The service collection for chaining. </returns>
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	[RequiresDynamicCode("This method uses dynamic code generation and may not work correctly with AOT")]
	public static IServiceCollection AddSqlServerPersistenceWithCdc(
		this IServiceCollection services,
		IConfiguration configuration,
		params Assembly[] cdcHandlerAssemblies)
	{
		// Add base persistence
		_ = services.AddSqlServerPersistence(configuration);

		// Add CDC processor
		if (cdcHandlerAssemblies?.Length > 0)
		{
			_ = services.AddCdcProcessor(cdcHandlerAssemblies);
		}

		return services;
	}

	/// <summary>
	/// Adds SQL Server transaction scope factory.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="defaultIsolationLevel"> The default isolation level for transactions. </param>
	/// <param name="defaultTimeout"> The default timeout for transactions. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddSqlServerTransactionScope(
		this IServiceCollection services,
		IsolationLevel defaultIsolationLevel = IsolationLevel.ReadCommitted,
		TimeSpan? defaultTimeout = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var timeout = defaultTimeout ?? TimeSpan.FromSeconds(30);

		// Register transaction scope factory
		services.TryAddTransient<ITransactionScope>(sp =>
		{
			var logger = sp.GetRequiredService<ILogger<SqlServerTransactionScope>>();
			return new SqlServerTransactionScope(defaultIsolationLevel, timeout, logger);
		});

		// Register transaction scope factory delegate
		services.TryAddSingleton<Func<IsolationLevel, TimeSpan, ITransactionScope>>(sp => (isolationLevel, timeoutOverride) =>
		{
			var logger = sp.GetRequiredService<ILogger<SqlServerTransactionScope>>();
			return new SqlServerTransactionScope(isolationLevel, timeoutOverride, logger);
		});

		return services;
	}

	/// <summary>
	/// Configures SQL Server persistence provider options.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Action to configure the options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection ConfigureSqlServerPersistence(
		this IServiceCollection services,
		Action<SqlServerPersistenceOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		_ = services.PostConfigure(configureOptions);

		return services;
	}

	/// <summary>
	/// Adds SQL Server persistence with custom retry policy configuration.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="connectionString"> The connection string. </param>
	/// <param name="maxRetryAttempts"> Maximum number of retry attempts. </param>
	/// <param name="retryDelayMilliseconds"> Base delay between retries in milliseconds. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddSqlServerPersistenceWithRetry(
		this IServiceCollection services,
		string connectionString,
		int maxRetryAttempts = 3,
		int retryDelayMilliseconds = 1000) =>
		services.AddSqlServerPersistence(connectionString, options =>
		{
			options.MaxRetryAttempts = maxRetryAttempts;
			options.RetryDelayMilliseconds = retryDelayMilliseconds;
			options.Resiliency.EnableConnectionResiliency = true;
			options.Resiliency.ConnectRetryCount = maxRetryAttempts;
			options.Resiliency.ConnectRetryInterval = retryDelayMilliseconds / 1000;
		});

	/// <summary>
	/// Adds SQL Server persistence with Always Encrypted support.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="connectionString"> The connection string. </param>
	/// <param name="columnEncryptionSetting"> The column encryption setting. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddSqlServerPersistenceWithEncryption(
		this IServiceCollection services,
		string connectionString,
		SqlConnectionColumnEncryptionSetting columnEncryptionSetting = SqlConnectionColumnEncryptionSetting.Enabled) =>
		services.AddSqlServerPersistence(connectionString, options =>
		{
			options.Security.EnableAlwaysEncrypted = true;
			options.Security.ColumnEncryptionSetting = columnEncryptionSetting;
			options.Security.EncryptConnection = true;
			options.Security.TrustServerCertificate = false;
		});

	/// <summary>
	/// Adds SQL Server persistence optimized for read-only workloads.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="connectionString"> The connection string. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddSqlServerPersistenceReadOnly(
		this IServiceCollection services,
		string connectionString) =>
		services.AddSqlServerPersistence(connectionString, static options =>
		{
			options.Connection.ApplicationIntent = ApplicationIntent.ReadOnly;
			options.Connection.EnableMars = false; // Not needed for read-only
			options.CommandTimeout = 60; // Allow longer read queries
		});

	/// <summary>
	/// Adds SQL Server persistence with high availability configuration.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="connectionString"> The connection string. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddSqlServerPersistenceHighAvailability(
		this IServiceCollection services,
		string connectionString) =>
		services.AddSqlServerPersistence(connectionString, static options =>
		{
			options.Connection.MultiSubnetFailover = true;
			options.Resiliency.EnableConnectionResiliency = true;
			options.Resiliency.ConnectRetryCount = 5;
			options.Resiliency.ConnectRetryInterval = 10;
			options.Connection.EnableTransparentNetworkIPResolution = true;
			options.Connection.LoadBalanceTimeout = 30;
		});

	private static void RegisterCoreServices(IServiceCollection services)
	{
		// Register cross-property validator
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SqlServerPersistenceOptions>, SqlServerPersistenceOptionsValidator>());

		// Register metrics collector
		services.TryAddSingleton<SqlServerPersistenceMetrics>();

		// Register health check
		services.TryAddSingleton<SqlServerPersistenceHealthCheck>();

		// Register the main provider
		services.TryAddSingleton<SqlServerPersistenceProvider>();
		services.TryAddSingleton<ISqlPersistenceProvider>(static sp => sp.GetRequiredService<SqlServerPersistenceProvider>());
		services.TryAddSingleton<IPersistenceProvider>(static sp => sp.GetRequiredService<SqlServerPersistenceProvider>());

		// Register transaction scope with default settings
		_ = services.AddSqlServerTransactionScope();

		// Ensure base SQL services are registered
		_ = services.AddExcaliburSqlServices();
	}
}
