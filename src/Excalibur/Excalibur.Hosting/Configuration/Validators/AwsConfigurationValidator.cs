// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.RegularExpressions;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Hosting.Configuration.Validators;

/// <summary>
/// Validates AWS-specific configuration settings.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="AwsConfigurationValidator" /> class. </remarks>
/// <param name="configSection"> The configuration section to validate. </param>
public sealed partial class AwsConfigurationValidator(string configSection = "AWS") : CloudProviderValidator($"AWS:{configSection}")
{
	private static readonly IReadOnlySet<string> ValidRegions = new HashSet<string>(StringComparer.Ordinal)
	{
		"us-east-1",
		"us-east-2",
		"us-west-1",
		"us-west-2",
		"eu-west-1",
		"eu-west-2",
		"eu-west-3",
		"eu-central-1",
		"eu-north-1",
		"ap-southeast-1",
		"ap-southeast-2",
		"ap-northeast-1",
		"ap-northeast-2",
		"ap-south-1",
		"sa-east-1",
		"ca-central-1",
		"me-south-1",
		"af-south-1",
	};

	/// <inheritdoc />
	public override Task<ConfigurationValidationResult> ValidateAsync(
		IConfiguration configuration,
		CancellationToken cancellationToken)
	{
		var errors = new List<ConfigurationValidationError>();
		var awsConfig = configuration.GetSection(configSection);

		// Validate Region
		var region = awsConfig["Region"] ?? awsConfig["DefaultRegion"];
		_ = ValidateRegion(region, ValidRegions, errors, $"{configSection}:Region");

		// Validate credentials
		var accessKey = awsConfig["AccessKeyId"];
		var secretKey = awsConfig["SecretAccessKey"];
		var profile = awsConfig["Profile"];
		var roleArn = awsConfig["RoleArn"];

		// Check for at least one authentication method
		var hasCredentials = !string.IsNullOrWhiteSpace(accessKey) && !string.IsNullOrWhiteSpace(secretKey);
		var hasProfile = !string.IsNullOrWhiteSpace(profile);
		var hasRole = !string.IsNullOrWhiteSpace(roleArn);

		if (!hasCredentials && !hasProfile && !hasRole)
		{
			errors.Add(new ConfigurationValidationError(
				"No AWS authentication method configured",
				$"{configSection}:Authentication",
				value: null,
				"Configure either AccessKeyId/SecretAccessKey, Profile, or RoleArn"));
		}

		// Validate access key format if provided
		if (!string.IsNullOrWhiteSpace(accessKey) && !IsValidAccessKeyId(accessKey))
		{
			errors.Add(new ConfigurationValidationError(
				"Invalid AWS Access Key ID format",
				$"{configSection}:AccessKeyId",
				$"***{accessKey.AsSpan(Math.Max(0, accessKey.Length - 4))}",
				"Access Key ID should be 20 characters and start with AKIA, ASIA, or AIDA"));
		}

		// Validate role ARN if provided
		if (!string.IsNullOrWhiteSpace(roleArn))
		{
			_ = ValidateArn(roleArn, errors, $"{configSection}:RoleArn");
		}

		// Validate S3 settings if present
		var s3Config = awsConfig.GetSection("S3");
		if (s3Config.Exists())
		{
			ValidateS3Configuration(s3Config, errors);
		}

		// Validate SQS settings if present
		var sqsConfig = awsConfig.GetSection("SQS");
		if (sqsConfig.Exists())
		{
			ValidateSqsConfiguration(sqsConfig, errors);
		}

		// Validate SNS settings if present
		var snsConfig = awsConfig.GetSection("SNS");
		if (snsConfig.Exists())
		{
			ValidateSnsConfiguration(snsConfig, errors);
		}

		return Task.FromResult(errors.Count == 0
			? ConfigurationValidationResult.Success()
			: ConfigurationValidationResult.Failure(errors));
	}

	private static bool IsValidAccessKeyId(string accessKeyId) =>

		// AWS Access Key IDs are 20 characters long and start with specific prefixes
		accessKeyId.Length == 20 &&
		(accessKeyId.StartsWith("AKIA", StringComparison.Ordinal) || // Long-term credentials
		 accessKeyId.StartsWith("ASIA", StringComparison.Ordinal) || // Temporary credentials
		 accessKeyId.StartsWith("AIDA", StringComparison.Ordinal)); // IAM user

	[GeneratedRegex(@"^[a-z0-9][a-z0-9\-]*[a-z0-9]$")]
	private static partial Regex MyRegex();

	private void ValidateS3Configuration(IConfigurationSection s3Config, List<ConfigurationValidationError> errors)
	{
		var bucketName = s3Config["BucketName"];
		if (!string.IsNullOrWhiteSpace(bucketName))
		{
			// S3 bucket naming rules
			if (bucketName.Length is < 3 or > 63)
			{
				errors.Add(new ConfigurationValidationError(
					"S3 bucket name must be between 3 and 63 characters",
					$"{configSection}:S3:BucketName",
					bucketName,
					"Use lowercase letters, numbers, and hyphens only"));
			}

			if (!MyRegex().IsMatch(bucketName))
			{
				errors.Add(new ConfigurationValidationError(
					"Invalid S3 bucket name format",
					$"{configSection}:S3:BucketName",
					bucketName,
					"Bucket names must start and end with a letter or number, use only lowercase letters, numbers, and hyphens"));
			}
		}
	}

	private void ValidateSqsConfiguration(IConfigurationSection sqsConfig, List<ConfigurationValidationError> errors)
	{
		var queueUrl = sqsConfig["QueueUrl"];
		if (!string.IsNullOrWhiteSpace(queueUrl))
		{
			if (!Uri.TryCreate(queueUrl, UriKind.Absolute, out var uri) ||
				!uri.Host.Contains("sqs", StringComparison.OrdinalIgnoreCase))
			{
				errors.Add(new ConfigurationValidationError(
					"Invalid SQS queue URL",
					$"{configSection}:SQS:QueueUrl",
					queueUrl,
					"Use format: https://sqs.region.amazonaws.com/account-id/queue-name"));
			}
		}

		// Validate message retention period if specified
		var retentionPeriod = sqsConfig["MessageRetentionPeriod"];
		if (!string.IsNullOrWhiteSpace(retentionPeriod) && int.TryParse(retentionPeriod, out var seconds))
		{
			if (seconds is < 60 or > 1_209_600) // 1 minute to 14 days
			{
				errors.Add(new ConfigurationValidationError(
					"SQS message retention period out of range",
					$"{configSection}:SQS:MessageRetentionPeriod",
					seconds,
					"Set between 60 and 1209600 seconds (1 minute to 14 days)"));
			}
		}
	}

	private void ValidateSnsConfiguration(IConfigurationSection snsConfig, List<ConfigurationValidationError> errors)
	{
		var topicArn = snsConfig["TopicArn"];
		if (!string.IsNullOrWhiteSpace(topicArn))
		{
			_ = ValidateArn(topicArn, errors, $"{configSection}:SNS:TopicArn");
		}
	}
}
