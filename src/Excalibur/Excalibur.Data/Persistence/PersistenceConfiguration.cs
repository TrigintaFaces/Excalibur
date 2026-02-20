// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Abstractions.Persistence;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Data.Persistence;

/// <summary>
/// Implementation of centralized persistence configuration.
/// </summary>
public sealed class PersistenceConfiguration : IPersistenceConfiguration
{
	private IConfigurationSection? _configurationSection;

	/// <inheritdoc />
	public string DefaultProvider { get; set; } = "default";

	/// <inheritdoc />
	public IDictionary<string, ProviderConfiguration> Providers { get; } = new Dictionary<string, ProviderConfiguration>(StringComparer.Ordinal);

	/// <inheritdoc />
	public PersistenceOptions GlobalOptions { get; } = new();

	/// <inheritdoc />
	public IConfigurationSection ConfigurationSection =>
		_configurationSection ?? throw new InvalidOperationException("Configuration section has not been set.");

	/// <inheritdoc />
	public string DefaultProviderName => DefaultProvider;

	/// <inheritdoc />
	public void RegisterProviderConfiguration(string providerName, IPersistenceOptions options)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
		ArgumentNullException.ThrowIfNull(options);

		// If it's already a ProviderConfiguration, use it directly
		if (options is ProviderConfiguration providerConfig)
		{
			Providers[providerName] = providerConfig;
		}
		else
		{
			// Convert to ProviderConfiguration for storage
			var config = new ProviderConfiguration
			{
				Name = providerName,
				ConnectionString = options.ConnectionString ?? string.Empty,
				Type = PersistenceProviderType.Custom, // Default type
				ConnectionTimeout = options.ConnectionTimeout,
				CommandTimeout = options.CommandTimeout,
				ProviderSpecificOptions = options.ProviderSpecificOptions,
			};

			// Copy ISP sub-interface properties when the source implements them
			if (options is IPersistencePoolingOptions pooling)
			{
				config.MaxPoolSize = pooling.MaxPoolSize;
				config.MinPoolSize = pooling.MinPoolSize;
				config.EnableConnectionPooling = pooling.EnableConnectionPooling;
			}

			if (options is IPersistenceResilienceOptions resilience)
			{
				config.MaxRetryAttempts = resilience.MaxRetryAttempts;
				config.RetryDelayMilliseconds = resilience.RetryDelayMilliseconds;
			}

			if (options is IPersistenceObservabilityOptions observability)
			{
				config.EnableDetailedLogging = observability.EnableDetailedLogging;
				config.EnableMetrics = observability.EnableMetrics;
			}

			Providers[providerName] = config;
		}
	}

	/// <inheritdoc />
	public bool RemoveProviderConfiguration(string providerName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
		return Providers.Remove(providerName);
	}

	/// <inheritdoc />
	public IPersistenceOptions GetProviderOptions(string providerName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

		if (Providers.TryGetValue(providerName, out var config))
		{
			return config;
		}

		throw new ArgumentException($"Provider '{providerName}' is not configured.", nameof(providerName));
	}

	/// <inheritdoc />
	public IEnumerable<string> GetConfiguredProviders() => Providers.Keys.ToList();

	/// <inheritdoc />
	public void Reload()
	{
		// Implementation for configuration reload This would typically reload from configuration source
		var results = Validate();

		// Check if there are any errors
		if (results.Any(static r => r is { IsValid: false, Severity: >= ValidationSeverity.Error }))
		{
			throw new InvalidOperationException(
				$"Configuration validation failed: {string.Join("; ", results.Where(static r => !r.IsValid).Select(static r => r.Message))}");
		}
	}

	/// <inheritdoc />
	public IEnumerable<ConfigurationValidationResult> Validate()
	{
		var results = new List<ConfigurationValidationResult>();

		// Validate default provider exists
		if (!string.IsNullOrWhiteSpace(DefaultProvider) && !Providers.ContainsKey(DefaultProvider))
		{
			results.Add(new ConfigurationValidationResult(
				isValid: false,
				DefaultProvider,
				$"Default provider '{DefaultProvider}' is not configured."));
		}

		// Validate each provider configuration
		foreach (var (name, config) in Providers)
		{
			var providerResults = ValidateProviderConfiguration(name, config);
			results.AddRange(providerResults);
		}

		// If no issues were found, add a success result
		if (results.Count == 0)
		{
			results.Add(new ConfigurationValidationResult(
				isValid: true,
				"Global",
				"All persistence configuration is valid.",
				ValidationSeverity.Info));
		}

		return results;
	}

	/// <summary>
	/// Sets the configuration section for persistence settings.
	/// </summary>
	/// <param name="section"> The configuration section to use. </param>
	/// <exception cref="ArgumentNullException"></exception>
	internal void SetConfigurationSection(IConfigurationSection section) =>
		_configurationSection = section ?? throw new ArgumentNullException(nameof(section));

	/// <summary>
	/// Validates a provider configuration.
	/// </summary>
	private static IEnumerable<ConfigurationValidationResult> ValidateProviderConfiguration(string name, ProviderConfiguration config)
	{
		var results = new List<ConfigurationValidationResult>();

		if (string.IsNullOrWhiteSpace(config.Name))
		{
			results.Add(new ConfigurationValidationResult(
				isValid: false,
				name,
				$"Provider name is required for provider '{name}'."));
		}

		if (string.IsNullOrWhiteSpace(config.ConnectionString))
		{
			results.Add(new ConfigurationValidationResult(
				isValid: false,
				name,
				$"Connection string is required for provider '{name}'."));
		}

		if (config.MaxPoolSize <= 0)
		{
			results.Add(new ConfigurationValidationResult(
				isValid: false,
				name,
				$"MaxPoolSize must be greater than 0 for provider '{name}'."));
		}

		if (config.ConnectionTimeout <= 0)
		{
			results.Add(new ConfigurationValidationResult(
				isValid: false,
				name,
				$"ConnectionTimeout must be greater than 0 for provider '{name}'."));
		}

		if (config.CommandTimeout <= 0)
		{
			results.Add(new ConfigurationValidationResult(
				isValid: false,
				name,
				$"CommandTimeout must be greater than 0 for provider '{name}'."));
		}

		return results;
	}
}
