// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.RegularExpressions;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Hosting.Configuration.Validators;

/// <summary>
/// Validates Azure-specific configuration settings.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="AzureConfigurationValidator" /> class. </remarks>
/// <param name="configSection"> The configuration section to validate. </param>
public sealed partial class AzureConfigurationValidator(string configSection = "Azure") : CloudProviderValidator($"Azure:{configSection}")
{
	/// <inheritdoc />
	public override Task<ConfigurationValidationResult> ValidateAsync(
		IConfiguration configuration,
		CancellationToken cancellationToken)
	{
		var errors = new List<ConfigurationValidationError>();
		var azureConfig = configuration.GetSection(configSection);

		// Validate authentication
		var tenantId = azureConfig["TenantId"];
		var clientId = azureConfig["ClientId"];
		var clientSecret = azureConfig["ClientSecret"];
		var subscriptionId = azureConfig["SubscriptionId"];

		// Validate Tenant ID (GUID format)
		if (!string.IsNullOrWhiteSpace(tenantId) && !Guid.TryParse(tenantId, out _))
		{
			errors.Add(new ConfigurationValidationError(
				"Invalid Azure Tenant ID format",
				$"{configSection}:TenantId",
				tenantId,
				"Tenant ID should be a valid GUID"));
		}

		// Validate Client ID (GUID format)
		if (!string.IsNullOrWhiteSpace(clientId) && !Guid.TryParse(clientId, out _))
		{
			errors.Add(new ConfigurationValidationError(
				"Invalid Azure Client ID format",
				$"{configSection}:ClientId",
				clientId,
				"Client ID should be a valid GUID"));
		}

		// Validate Subscription ID (GUID format)
		if (!string.IsNullOrWhiteSpace(subscriptionId) && !Guid.TryParse(subscriptionId, out _))
		{
			errors.Add(new ConfigurationValidationError(
				"Invalid Azure Subscription ID format",
				$"{configSection}:SubscriptionId",
				subscriptionId,
				"Subscription ID should be a valid GUID"));
		}

		// Check for complete authentication configuration
		if (!string.IsNullOrWhiteSpace(clientId) && string.IsNullOrWhiteSpace(clientSecret))
		{
			errors.Add(new ConfigurationValidationError(
				"Azure Client Secret is required when Client ID is provided",
				$"{configSection}:ClientSecret",
				value: null,
				"Provide the client secret for the service principal"));
		}

		// Validate Storage Account settings if present
		var storageConfig = azureConfig.GetSection("Storage");
		if (storageConfig.Exists())
		{
			ValidateStorageConfiguration(storageConfig, errors);
		}

		// Validate Service Bus settings if present
		var serviceBusConfig = azureConfig.GetSection("ServiceBus");
		if (serviceBusConfig.Exists())
		{
			ValidateServiceBusConfiguration(serviceBusConfig, errors);
		}

		// Validate Event Hubs settings if present
		var eventHubsConfig = azureConfig.GetSection("EventHubs");
		if (eventHubsConfig.Exists())
		{
			ValidateEventHubsConfiguration(eventHubsConfig, errors);
		}

		return Task.FromResult(errors.Count == 0
			? ConfigurationValidationResult.Success()
			: ConfigurationValidationResult.Failure(errors));
	}

	[GeneratedRegex(@"^[a-z0-9]{3,24}$")]
	private static partial Regex MyRegex();

	private void ValidateStorageConfiguration(IConfigurationSection storageConfig, List<ConfigurationValidationError> errors)
	{
		var connectionString = storageConfig["ConnectionString"];
		var accountName = storageConfig["AccountName"];
		var accountKey = storageConfig["AccountKey"];

		if (!string.IsNullOrWhiteSpace(connectionString))
		{
			// Validate connection string format
			if (!connectionString.Contains("AccountName=", StringComparison.OrdinalIgnoreCase))
			{
				errors.Add(new ConfigurationValidationError(
					"Azure Storage connection string missing AccountName",
					$"{configSection}:Storage:ConnectionString",
					value: null,
					"Connection string should contain AccountName=..."));
			}
		}
		else if (!string.IsNullOrWhiteSpace(accountName))
		{
			// Validate account name format (3-24 characters, lowercase letters and numbers only)
			if (!MyRegex().IsMatch(accountName))
			{
				errors.Add(new ConfigurationValidationError(
					"Invalid Azure Storage account name format",
					$"{configSection}:Storage:AccountName",
					accountName,
					"Account name must be 3-24 characters, lowercase letters and numbers only"));
			}

			if (string.IsNullOrWhiteSpace(accountKey))
			{
				errors.Add(new ConfigurationValidationError(
					"Azure Storage account key is required when account name is provided",
					$"{configSection}:Storage:AccountKey",
					value: null,
					"Provide the account key or use a connection string instead"));
			}
		}
	}

	private void ValidateServiceBusConfiguration(IConfigurationSection serviceBusConfig, List<ConfigurationValidationError> errors)
	{
		var connectionString = serviceBusConfig["ConnectionString"];
		var fullyQualifiedNamespace = serviceBusConfig["FullyQualifiedNamespace"];

		if (!string.IsNullOrWhiteSpace(connectionString))
		{
			// Validate connection string contains endpoint
			if (!connectionString.Contains("Endpoint=sb://", StringComparison.OrdinalIgnoreCase))
			{
				errors.Add(new ConfigurationValidationError(
					"Azure Service Bus connection string missing Endpoint",
					$"{configSection}:ServiceBus:ConnectionString",
					value: null,
					"Connection string should start with Endpoint=sb://..."));
			}
		}
		else if (!string.IsNullOrWhiteSpace(fullyQualifiedNamespace))
		{
			// Validate namespace format
			if (!fullyQualifiedNamespace.EndsWith(".servicebus.windows.net", StringComparison.OrdinalIgnoreCase))
			{
				errors.Add(new ConfigurationValidationError(
					"Invalid Azure Service Bus namespace format",
					$"{configSection}:ServiceBus:FullyQualifiedNamespace",
					fullyQualifiedNamespace,
					"Use format: Excalibur.Dispatch.Transport.Aws.Advanced.SessionManagement.servicebus.windows.net"));
			}
		}
	}

	private void ValidateEventHubsConfiguration(IConfigurationSection eventHubsConfig, List<ConfigurationValidationError> errors)
	{
		var connectionString = eventHubsConfig["ConnectionString"];
		var fullyQualifiedNamespace = eventHubsConfig["FullyQualifiedNamespace"];
		var eventHubName = eventHubsConfig["EventHubName"];

		if (!string.IsNullOrWhiteSpace(connectionString))
		{
			// Validate connection string contains endpoint
			if (!connectionString.Contains("Endpoint=sb://", StringComparison.OrdinalIgnoreCase))
			{
				errors.Add(new ConfigurationValidationError(
					"Azure Event Hubs connection string missing Endpoint",
					$"{configSection}:EventHubs:ConnectionString",
					value: null,
					"Connection string should start with Endpoint=sb://..."));
			}
		}
		else if (!string.IsNullOrWhiteSpace(fullyQualifiedNamespace))
		{
			// Validate namespace format
			if (!fullyQualifiedNamespace.EndsWith(".servicebus.windows.net", StringComparison.OrdinalIgnoreCase))
			{
				errors.Add(new ConfigurationValidationError(
					"Invalid Azure Event Hubs namespace format",
					$"{configSection}:EventHubs:FullyQualifiedNamespace",
					fullyQualifiedNamespace,
					"Use format: Excalibur.Dispatch.Transport.Aws.Advanced.SessionManagement.servicebus.windows.net"));
			}

			if (string.IsNullOrWhiteSpace(eventHubName))
			{
				errors.Add(new ConfigurationValidationError(
					"Azure Event Hub name is required when namespace is provided",
					$"{configSection}:EventHubs:EventHubName",
					value: null,
					"Provide the Event Hub name"));
			}
		}
	}
}
