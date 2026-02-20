// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.RegularExpressions;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Hosting.Configuration.Validators;

/// <summary>
/// Validates Google Cloud Platform configuration settings.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="GoogleCloudConfigurationValidator" /> class. </remarks>
/// <param name="configSection"> The configuration section to validate. </param>
public sealed partial class GoogleCloudConfigurationValidator(string configSection = "GoogleCloud")
	: CloudProviderValidator($"GoogleCloud:{configSection}")
{
	private static readonly IReadOnlySet<string> ValidRegions = new HashSet<string>(StringComparer.Ordinal)
	{
		"us-central1",
		"us-east1",
		"us-east4",
		"us-west1",
		"us-west2",
		"us-west3",
		"us-west4",
		"europe-west1",
		"europe-west2",
		"europe-west3",
		"europe-west4",
		"europe-west6",
		"europe-north1",
		"asia-east1",
		"asia-east2",
		"asia-northeast1",
		"asia-northeast2",
		"asia-northeast3",
		"asia-south1",
		"asia-south2",
		"asia-southeast1",
		"asia-southeast2",
		"australia-southeast1",
		"australia-southeast2",
		"southamerica-east1",
		"northamerica-northeast1",
	};

	/// <inheritdoc />
	public override Task<ConfigurationValidationResult> ValidateAsync(
		IConfiguration configuration,
		CancellationToken cancellationToken)
	{
		var errors = new List<ConfigurationValidationError>();
		var gcpConfig = configuration.GetSection(configSection);

		// Validate Project ID
		var projectId = gcpConfig["ProjectId"];
		if (!string.IsNullOrWhiteSpace(projectId))
		{
			// GCP project IDs must be 6-30 characters, lowercase letters, numbers, and hyphens
			if (!ProjectIdRegex().IsMatch(projectId))
			{
				errors.Add(new ConfigurationValidationError(
					"Invalid GCP project ID format",
					$"{configSection}:ProjectId",
					projectId,
					"Project ID must be 6-30 characters, start with a letter, use only lowercase letters, numbers, and hyphens"));
			}
		}
		else
		{
			errors.Add(new ConfigurationValidationError(
				"GCP Project ID is required",
				$"{configSection}:ProjectId",
				value: null,
				"Provide your Google Cloud project ID"));
		}

		// Validate credentials
		var credentialsPath = gcpConfig["CredentialsPath"];
		var credentialsJson = gcpConfig["CredentialsJson"];

		if (!string.IsNullOrWhiteSpace(credentialsPath))
		{
			if (!File.Exists(credentialsPath))
			{
				errors.Add(new ConfigurationValidationError(
					"GCP credentials file not found",
					$"{configSection}:CredentialsPath",
					credentialsPath,
					"Ensure the service account key file exists at the specified path"));
			}
		}
		else if (string.IsNullOrWhiteSpace(credentialsJson))
		{
			// Check for default application credentials environment variable
			var adcPath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
			if (string.IsNullOrWhiteSpace(adcPath))
			{
				errors.Add(new ConfigurationValidationError(
					"No GCP credentials configured",
					$"{configSection}:Credentials",
					value: null,
					"Provide CredentialsPath, CredentialsJson, or set GOOGLE_APPLICATION_CREDENTIALS environment variable"));
			}
		}

		// Validate region if specified
		var region = gcpConfig["Region"] ?? gcpConfig["DefaultRegion"];
		if (!string.IsNullOrWhiteSpace(region))
		{
			_ = ValidateRegion(region, ValidRegions, errors, $"{configSection}:Region");
		}

		// Validate Cloud Storage settings if present
		var storageConfig = gcpConfig.GetSection("Storage");
		if (storageConfig.Exists())
		{
			ValidateCloudStorageConfiguration(storageConfig, errors);
		}

		// Validate Pub/Sub settings if present
		var pubSubConfig = gcpConfig.GetSection("PubSub");
		if (pubSubConfig.Exists())
		{
			ValidatePubSubConfiguration(pubSubConfig, errors);
		}

		return Task.FromResult(errors.Count == 0
			? ConfigurationValidationResult.Success()
			: ConfigurationValidationResult.Failure(errors));
	}

	[GeneratedRegex(@"^[a-z][a-z0-9\-]{4,28}[a-z0-9]$")]
	private static partial Regex ProjectIdRegex();

	[GeneratedRegex(@"^[a-z0-9][a-z0-9\-_]*[a-z0-9]$")]
	private static partial Regex BucketNameRegex();

	[GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9\-_.]*$")]
	private static partial Regex PubSubNameRegex();

	private void ValidateCloudStorageConfiguration(IConfigurationSection storageConfig, List<ConfigurationValidationError> errors)
	{
		var bucketName = storageConfig["BucketName"];
		if (!string.IsNullOrWhiteSpace(bucketName))
		{
			// GCS bucket naming rules (similar to S3 but with some differences)
			if (bucketName.Length is < 3 or > 63)
			{
				errors.Add(new ConfigurationValidationError(
					"GCS bucket name must be between 3 and 63 characters",
					$"{configSection}:Storage:BucketName",
					bucketName,
					"Use lowercase letters, numbers, hyphens, and underscores"));
			}

			if (!BucketNameRegex().IsMatch(bucketName))
			{
				errors.Add(new ConfigurationValidationError(
					"Invalid GCS bucket name format",
					$"{configSection}:Storage:BucketName",
					bucketName,
					"Bucket names must start and end with a letter or number"));
			}
		}
	}

	private void ValidatePubSubConfiguration(IConfigurationSection pubSubConfig, List<ConfigurationValidationError> errors)
	{
		var topicName = pubSubConfig["TopicName"];
		var subscriptionName = pubSubConfig["SubscriptionName"];

		if (!string.IsNullOrWhiteSpace(topicName))
		{
			// Pub/Sub topic names must start with a letter and contain only letters, numbers, hyphens, underscores, and periods
			if (!PubSubNameRegex().IsMatch(topicName))
			{
				errors.Add(new ConfigurationValidationError(
					"Invalid Pub/Sub topic name format",
					$"{configSection}:PubSub:TopicName",
					topicName,
					"Topic names must start with a letter and contain only letters, numbers, hyphens, underscores, and periods"));
			}
		}

		if (!string.IsNullOrWhiteSpace(subscriptionName))
		{
			// Same rules for subscription names
			if (!PubSubNameRegex().IsMatch(subscriptionName))
			{
				errors.Add(new ConfigurationValidationError(
					"Invalid Pub/Sub subscription name format",
					$"{configSection}:PubSub:SubscriptionName",
					subscriptionName,
					"Subscription names must start with a letter and contain only letters, numbers, hyphens, underscores, and periods"));
			}
		}
	}
}
