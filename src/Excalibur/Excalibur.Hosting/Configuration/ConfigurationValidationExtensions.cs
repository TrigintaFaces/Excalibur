// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Hosting.Configuration.Validators;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Hosting.Configuration;

/// <summary>
/// Extension methods for registering configuration validation services.
/// </summary>
public static class ConfigurationValidationExtensions
{
	/// <summary>
	/// Adds configuration validation services to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> An action to configure validation options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddConfigurationValidation(
		this IServiceCollection services,
		Action<ConfigurationValidationOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register the validation options
		var options = new ConfigurationValidationOptions();
		configureOptions?.Invoke(options);
		_ = services.AddSingleton(options);

		// Register the validation service as a hosted service
		_ = services.AddHostedService<ConfigurationValidationService>();

		return services;
	}

	/// <summary>
	/// Adds a configuration validator to the service collection.
	/// </summary>
	/// <typeparam name="TValidator"> The type of validator to add. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddConfigurationValidator<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TValidator>(this IServiceCollection services)
		where TValidator : class, IConfigurationValidator
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddSingleton<IConfigurationValidator, TValidator>();

		return services;
	}

	/// <summary>
	/// Adds a configuration validator instance to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="validator"> The validator instance. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddConfigurationValidator(
		this IServiceCollection services,
		IConfigurationValidator validator)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(validator);

		_ = services.AddSingleton(validator);

		return services;
	}

	/// <summary>
	/// Adds database connection string validation.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="connectionStringKey"> The connection string key. </param>
	/// <param name="provider"> The database provider. </param>
	/// <param name="testConnection"> Whether to test the actual connection. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddConnectionStringValidation(
		this IServiceCollection services,
		string connectionStringKey,
		DatabaseProvider provider,
		bool testConnection = false)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionStringKey);

		return services.AddConfigurationValidator(
			new ConnectionStringValidator(connectionStringKey, provider, testConnection));
	}

	/// <summary>
	/// Adds AWS configuration validation.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configSection"> The configuration section name. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddAwsConfigurationValidation(
		this IServiceCollection services,
		string configSection = "AWS")
	{
		ArgumentNullException.ThrowIfNull(services);

		return services.AddConfigurationValidator(new AwsConfigurationValidator(configSection));
	}

	/// <summary>
	/// Adds Azure configuration validation.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configSection"> The configuration section name. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddAzureConfigurationValidation(
		this IServiceCollection services,
		string configSection = "Azure")
	{
		ArgumentNullException.ThrowIfNull(services);

		return services.AddConfigurationValidator(new AzureConfigurationValidator(configSection));
	}

	/// <summary>
	/// Adds Google Cloud configuration validation.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configSection"> The configuration section name. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddGoogleCloudConfigurationValidation(
		this IServiceCollection services,
		string configSection = "GoogleCloud")
	{
		ArgumentNullException.ThrowIfNull(services);

		return services.AddConfigurationValidator(new GoogleCloudConfigurationValidator(configSection));
	}

	/// <summary>
	/// Adds RabbitMQ configuration validation.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configSection"> The configuration section name. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddRabbitMqConfigurationValidation(
		this IServiceCollection services,
		string configSection = "RabbitMQ")
	{
		ArgumentNullException.ThrowIfNull(services);

		return services.AddConfigurationValidator(new RabbitMqConfigurationValidator(configSection));
	}

	/// <summary>
	/// Adds Kafka configuration validation.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configSection"> The configuration section name. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddKafkaConfigurationValidation(
		this IServiceCollection services,
		string configSection = "Kafka")
	{
		ArgumentNullException.ThrowIfNull(services);

		return services.AddConfigurationValidator(new KafkaConfigurationValidator(configSection));
	}

	/// <summary>
	/// Adds configuration validation to the host builder.
	/// </summary>
	/// <param name="hostBuilder"> The host builder. </param>
	/// <param name="configureValidation"> An action to configure validation. </param>
	/// <returns> The host builder for chaining. </returns>
	public static IHostBuilder UseConfigurationValidation(
		this IHostBuilder hostBuilder,
		Action<IServiceCollection>? configureValidation = null)
	{
		ArgumentNullException.ThrowIfNull(hostBuilder);

		return hostBuilder.ConfigureServices((context, services) =>
		{
			_ = services.AddConfigurationValidation();
			configureValidation?.Invoke(services);
		});
	}

	/// <summary>
	/// Adds configuration validation to the host application builder.
	/// </summary>
	/// <param name="builder"> The host application builder. </param>
	/// <param name="configureValidation"> An action to configure validation. </param>
	/// <returns> The host application builder for chaining. </returns>
	public static HostApplicationBuilder UseConfigurationValidation(
		this HostApplicationBuilder builder,
		Action<IServiceCollection>? configureValidation = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddConfigurationValidation();
		configureValidation?.Invoke(builder.Services);

		return builder;
	}

	/// <summary>
	/// Adds common Excalibur configuration validators.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="options"> Options for common validators. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddExcaliburConfigurationValidation(
		this IServiceCollection services,
		ExcaliburValidationOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		options ??= new ExcaliburValidationOptions();

		// Add the base validation framework
		_ = services.AddConfigurationValidation(opt =>
		{
			opt.Enabled = options.Enabled;
			opt.FailFast = options.FailFast;
		});

		// Add database validation if configured
		if (options.ValidateDatabases)
		{
			foreach (var db in options.DatabaseConnections)
			{
				_ = services.AddConnectionStringValidation(db.Key, db.Value, options.TestDatabaseConnections);
			}
		}

		// Add cloud provider validation if configured
		if (options.ValidateCloudProviders)
		{
			if (options.UseAws)
			{
				_ = services.AddAwsConfigurationValidation();
			}

			if (options.UseAzure)
			{
				_ = services.AddAzureConfigurationValidation();
			}

			if (options.UseGoogleCloud)
			{
				_ = services.AddGoogleCloudConfigurationValidation();
			}
		}

		// Add message broker validation if configured
		if (options.ValidateMessageBrokers)
		{
			if (options.UseRabbitMq)
			{
				_ = services.AddRabbitMqConfigurationValidation();
			}

			if (options.UseKafka)
			{
				_ = services.AddKafkaConfigurationValidation();
			}
		}

		return services;
	}
}
