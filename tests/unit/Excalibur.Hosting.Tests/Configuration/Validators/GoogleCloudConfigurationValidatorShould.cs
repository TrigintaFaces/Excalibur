// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Configuration.Validators;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Hosting.Tests.Configuration.Validators;

/// <summary>
/// Unit tests for <see cref="GoogleCloudConfigurationValidator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Feature", "Configuration")]
public sealed class GoogleCloudConfigurationValidatorShould : UnitTestBase
{
	private const string ValidProjectId = "my-gcp-project-123";
	private const string ValidBucketName = "my-bucket-123";
	private const string ValidTopicName = "my-topic";
	private const string ValidSubscriptionName = "my-subscription";

	#region Constructor Tests

	[Fact]
	public void SetDefaultConfigurationName()
	{
		// Act
		var validator = new GoogleCloudConfigurationValidator();

		// Assert
		validator.ConfigurationName.ShouldBe("GoogleCloud:GoogleCloud");
	}

	[Fact]
	public void SetCustomConfigurationName()
	{
		// Act
		var validator = new GoogleCloudConfigurationValidator("CustomSection");

		// Assert
		validator.ConfigurationName.ShouldBe("GoogleCloud:CustomSection");
	}

	[Fact]
	public void SetPriorityTo20()
	{
		// Act
		var validator = new GoogleCloudConfigurationValidator();

		// Assert
		validator.Priority.ShouldBe(20);
	}

	#endregion

	#region Project ID Tests

	[Fact]
	public async Task ReturnSuccess_WhenProjectIdIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["GoogleCloud:ProjectId"] = ValidProjectId,
				["GoogleCloud:CredentialsJson"] = "{}"
			})
			.Build();

		var validator = new GoogleCloudConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Theory]
	[InlineData("valid-project-01")]
	[InlineData("project123456")]
	[InlineData("a12345")]
	[InlineData("abcdefghijklmnopqrstuvwxyz0123")]
	public async Task ReturnSuccess_WhenProjectIdHasValidFormat(string projectId)
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["GoogleCloud:ProjectId"] = projectId,
				["GoogleCloud:CredentialsJson"] = "{}"
			})
			.Build();

		var validator = new GoogleCloudConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenProjectIdIsMissing()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["GoogleCloud:CredentialsJson"] = "{}"
			})
			.Build();

		var validator = new GoogleCloudConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Project ID is required"));
	}

	[Fact]
	public async Task ReturnFailure_WhenProjectIdIsEmpty()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["GoogleCloud:ProjectId"] = "",
				["GoogleCloud:CredentialsJson"] = "{}"
			})
			.Build();

		var validator = new GoogleCloudConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Project ID is required"));
	}

	[Theory]
	[InlineData("abc")] // Too short (less than 6 chars)
	[InlineData("ab12")] // Too short
	[InlineData("12345")] // Too short and starts with number
	public async Task ReturnFailure_WhenProjectIdIsTooShort(string projectId)
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["GoogleCloud:ProjectId"] = projectId,
				["GoogleCloud:CredentialsJson"] = "{}"
			})
			.Build();

		var validator = new GoogleCloudConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Invalid GCP project ID format"));
	}

	[Fact]
	public async Task ReturnFailure_WhenProjectIdIsTooLong()
	{
		// Arrange - 31 characters is too long
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["GoogleCloud:ProjectId"] = "a" + new string('b', 30), // 31 chars
				["GoogleCloud:CredentialsJson"] = "{}"
			})
			.Build();

		var validator = new GoogleCloudConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Invalid GCP project ID format"));
	}

	[Theory]
	[InlineData("1project-name")] // Starts with number
	[InlineData("-project-name")] // Starts with hyphen
	public async Task ReturnFailure_WhenProjectIdStartsWithInvalidChar(string projectId)
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["GoogleCloud:ProjectId"] = projectId,
				["GoogleCloud:CredentialsJson"] = "{}"
			})
			.Build();

		var validator = new GoogleCloudConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Invalid GCP project ID format"));
	}

	[Theory]
	[InlineData("project-name-")] // Ends with hyphen
	public async Task ReturnFailure_WhenProjectIdEndsWithHyphen(string projectId)
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["GoogleCloud:ProjectId"] = projectId,
				["GoogleCloud:CredentialsJson"] = "{}"
			})
			.Build();

		var validator = new GoogleCloudConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Invalid GCP project ID format"));
	}

	[Theory]
	[InlineData("Project-Name")] // Uppercase
	[InlineData("my_project")] // Underscore
	[InlineData("my.project")] // Period
	public async Task ReturnFailure_WhenProjectIdHasInvalidChars(string projectId)
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["GoogleCloud:ProjectId"] = projectId,
				["GoogleCloud:CredentialsJson"] = "{}"
			})
			.Build();

		var validator = new GoogleCloudConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Invalid GCP project ID format"));
	}

	#endregion

	#region Credentials Tests

	[Fact]
	public async Task ReturnSuccess_WhenCredentialsJsonIsProvided()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["GoogleCloud:ProjectId"] = ValidProjectId,
				["GoogleCloud:CredentialsJson"] = "{\"type\": \"service_account\"}"
			})
			.Build();

		var validator = new GoogleCloudConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenCredentialsPathNotFound()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["GoogleCloud:ProjectId"] = ValidProjectId,
				["GoogleCloud:CredentialsPath"] = "/nonexistent/path/credentials.json"
			})
			.Build();

		var validator = new GoogleCloudConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("credentials file not found"));
	}

	[Fact]
	public async Task ReturnFailure_WhenNoCredentialsConfigured()
	{
		// Arrange - Remove GOOGLE_APPLICATION_CREDENTIALS if set
		var originalValue = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
		try
		{
			Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", null);

			var config = new ConfigurationBuilder()
				.AddInMemoryCollection(new Dictionary<string, string?>
				{
					["GoogleCloud:ProjectId"] = ValidProjectId
				})
				.Build();

			var validator = new GoogleCloudConfigurationValidator();

			// Act
			var result = await validator.ValidateAsync(config, CancellationToken.None);

			// Assert
			result.IsValid.ShouldBeFalse();
			result.Errors.ShouldContain(e => e.Message.Contains("No GCP credentials configured"));
		}
		finally
		{
			// Restore original value
			Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", originalValue);
		}
	}

	[Fact]
	public async Task ReturnSuccess_WhenGoogleApplicationCredentialsEnvVarIsSet()
	{
		// Arrange
		var originalValue = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
		try
		{
			Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", "/some/path/credentials.json");

			var config = new ConfigurationBuilder()
				.AddInMemoryCollection(new Dictionary<string, string?>
				{
					["GoogleCloud:ProjectId"] = ValidProjectId
				})
				.Build();

			var validator = new GoogleCloudConfigurationValidator();

			// Act
			var result = await validator.ValidateAsync(config, CancellationToken.None);

			// Assert
			result.IsValid.ShouldBeTrue();
		}
		finally
		{
			// Restore original value
			Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", originalValue);
		}
	}

	#endregion

	#region Region Tests

	[Theory]
	[InlineData("us-central1")]
	[InlineData("us-east1")]
	[InlineData("us-west1")]
	[InlineData("europe-west1")]
	[InlineData("asia-east1")]
	[InlineData("australia-southeast1")]
	[InlineData("southamerica-east1")]
	[InlineData("northamerica-northeast1")]
	public async Task ReturnSuccess_WhenRegionIsValid(string region)
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["GoogleCloud:ProjectId"] = ValidProjectId,
				["GoogleCloud:CredentialsJson"] = "{}",
				["GoogleCloud:Region"] = region
			})
			.Build();

		var validator = new GoogleCloudConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnSuccess_WhenDefaultRegionIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["GoogleCloud:ProjectId"] = ValidProjectId,
				["GoogleCloud:CredentialsJson"] = "{}",
				["GoogleCloud:DefaultRegion"] = "us-central1"
			})
			.Build();

		var validator = new GoogleCloudConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenRegionIsInvalid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["GoogleCloud:ProjectId"] = ValidProjectId,
				["GoogleCloud:CredentialsJson"] = "{}",
				["GoogleCloud:Region"] = "invalid-region"
			})
			.Build();

		var validator = new GoogleCloudConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Invalid cloud region"));
	}

	#endregion

	#region Cloud Storage Tests

	[Fact]
	public async Task ReturnSuccess_WhenStorageBucketNameIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["GoogleCloud:ProjectId"] = ValidProjectId,
				["GoogleCloud:CredentialsJson"] = "{}",
				["GoogleCloud:Storage:BucketName"] = ValidBucketName
			})
			.Build();

		var validator = new GoogleCloudConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Theory]
	[InlineData("my-bucket")]
	[InlineData("bucket123")]
	[InlineData("my_bucket_123")]
	[InlineData("a-b")]
	public async Task ReturnSuccess_WhenStorageBucketNameHasValidFormat(string bucketName)
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["GoogleCloud:ProjectId"] = ValidProjectId,
				["GoogleCloud:CredentialsJson"] = "{}",
				["GoogleCloud:Storage:BucketName"] = bucketName
			})
			.Build();

		var validator = new GoogleCloudConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenStorageBucketNameIsTooShort()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["GoogleCloud:ProjectId"] = ValidProjectId,
				["GoogleCloud:CredentialsJson"] = "{}",
				["GoogleCloud:Storage:BucketName"] = "ab" // Too short
			})
			.Build();

		var validator = new GoogleCloudConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("between 3 and 63 characters"));
	}

	[Fact]
	public async Task ReturnFailure_WhenStorageBucketNameIsTooLong()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["GoogleCloud:ProjectId"] = ValidProjectId,
				["GoogleCloud:CredentialsJson"] = "{}",
				["GoogleCloud:Storage:BucketName"] = new string('a', 64) // Too long
			})
			.Build();

		var validator = new GoogleCloudConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("between 3 and 63 characters"));
	}

	[Theory]
	[InlineData("-bucket")] // Starts with hyphen
	[InlineData("_bucket")] // Starts with underscore
	public async Task ReturnFailure_WhenStorageBucketNameStartsWithInvalidChar(string bucketName)
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["GoogleCloud:ProjectId"] = ValidProjectId,
				["GoogleCloud:CredentialsJson"] = "{}",
				["GoogleCloud:Storage:BucketName"] = bucketName
			})
			.Build();

		var validator = new GoogleCloudConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Invalid GCS bucket name format"));
	}

	[Theory]
	[InlineData("bucket-")] // Ends with hyphen
	[InlineData("bucket_")] // Ends with underscore
	public async Task ReturnFailure_WhenStorageBucketNameEndsWithInvalidChar(string bucketName)
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["GoogleCloud:ProjectId"] = ValidProjectId,
				["GoogleCloud:CredentialsJson"] = "{}",
				["GoogleCloud:Storage:BucketName"] = bucketName
			})
			.Build();

		var validator = new GoogleCloudConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Invalid GCS bucket name format"));
	}

	#endregion

	#region Pub/Sub Tests

	[Fact]
	public async Task ReturnSuccess_WhenPubSubTopicNameIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["GoogleCloud:ProjectId"] = ValidProjectId,
				["GoogleCloud:CredentialsJson"] = "{}",
				["GoogleCloud:PubSub:TopicName"] = ValidTopicName
			})
			.Build();

		var validator = new GoogleCloudConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Theory]
	[InlineData("MyTopic")]
	[InlineData("my-topic")]
	[InlineData("my_topic")]
	[InlineData("my.topic")]
	[InlineData("Topic123")]
	public async Task ReturnSuccess_WhenPubSubTopicNameHasValidFormat(string topicName)
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["GoogleCloud:ProjectId"] = ValidProjectId,
				["GoogleCloud:CredentialsJson"] = "{}",
				["GoogleCloud:PubSub:TopicName"] = topicName
			})
			.Build();

		var validator = new GoogleCloudConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenPubSubTopicNameStartsWithNumber()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["GoogleCloud:ProjectId"] = ValidProjectId,
				["GoogleCloud:CredentialsJson"] = "{}",
				["GoogleCloud:PubSub:TopicName"] = "123topic"
			})
			.Build();

		var validator = new GoogleCloudConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Invalid Pub/Sub topic name format"));
	}

	[Theory]
	[InlineData("-topic")] // Starts with hyphen
	[InlineData("_topic")] // Starts with underscore
	[InlineData(".topic")] // Starts with period
	public async Task ReturnFailure_WhenPubSubTopicNameStartsWithSpecialChar(string topicName)
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["GoogleCloud:ProjectId"] = ValidProjectId,
				["GoogleCloud:CredentialsJson"] = "{}",
				["GoogleCloud:PubSub:TopicName"] = topicName
			})
			.Build();

		var validator = new GoogleCloudConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Invalid Pub/Sub topic name format"));
	}

	[Fact]
	public async Task ReturnSuccess_WhenPubSubSubscriptionNameIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["GoogleCloud:ProjectId"] = ValidProjectId,
				["GoogleCloud:CredentialsJson"] = "{}",
				["GoogleCloud:PubSub:SubscriptionName"] = ValidSubscriptionName
			})
			.Build();

		var validator = new GoogleCloudConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Theory]
	[InlineData("MySubscription")]
	[InlineData("my-subscription")]
	[InlineData("my_subscription")]
	[InlineData("my.subscription")]
	[InlineData("Subscription123")]
	public async Task ReturnSuccess_WhenPubSubSubscriptionNameHasValidFormat(string subscriptionName)
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["GoogleCloud:ProjectId"] = ValidProjectId,
				["GoogleCloud:CredentialsJson"] = "{}",
				["GoogleCloud:PubSub:SubscriptionName"] = subscriptionName
			})
			.Build();

		var validator = new GoogleCloudConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenPubSubSubscriptionNameStartsWithNumber()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["GoogleCloud:ProjectId"] = ValidProjectId,
				["GoogleCloud:CredentialsJson"] = "{}",
				["GoogleCloud:PubSub:SubscriptionName"] = "123subscription"
			})
			.Build();

		var validator = new GoogleCloudConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Invalid Pub/Sub subscription name format"));
	}

	[Theory]
	[InlineData("-subscription")]
	[InlineData("_subscription")]
	[InlineData(".subscription")]
	public async Task ReturnFailure_WhenPubSubSubscriptionNameStartsWithSpecialChar(string subscriptionName)
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["GoogleCloud:ProjectId"] = ValidProjectId,
				["GoogleCloud:CredentialsJson"] = "{}",
				["GoogleCloud:PubSub:SubscriptionName"] = subscriptionName
			})
			.Build();

		var validator = new GoogleCloudConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Invalid Pub/Sub subscription name format"));
	}

	[Fact]
	public async Task ReturnSuccess_WhenBothTopicAndSubscriptionAreValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["GoogleCloud:ProjectId"] = ValidProjectId,
				["GoogleCloud:CredentialsJson"] = "{}",
				["GoogleCloud:PubSub:TopicName"] = ValidTopicName,
				["GoogleCloud:PubSub:SubscriptionName"] = ValidSubscriptionName
			})
			.Build();

		var validator = new GoogleCloudConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	#endregion

	#region Custom Config Section Tests

	[Fact]
	public async Task ValidateCustomConfigSection()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["CustomGcp:ProjectId"] = ValidProjectId,
				["CustomGcp:CredentialsJson"] = "{}"
			})
			.Build();

		var validator = new GoogleCloudConfigurationValidator("CustomGcp");

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	#endregion

	#region Complete Configuration Tests

	[Fact]
	public async Task ReturnSuccess_WhenCompleteConfigurationProvided()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["GoogleCloud:ProjectId"] = ValidProjectId,
				["GoogleCloud:CredentialsJson"] = "{}",
				["GoogleCloud:Region"] = "us-central1",
				["GoogleCloud:Storage:BucketName"] = ValidBucketName,
				["GoogleCloud:PubSub:TopicName"] = ValidTopicName,
				["GoogleCloud:PubSub:SubscriptionName"] = ValidSubscriptionName
			})
			.Build();

		var validator = new GoogleCloudConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnMultipleErrors_WhenMultipleValidationsFail()
	{
		// Arrange - Remove env var to ensure credential check fails
		var originalValue = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
		try
		{
			Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", null);

			var config = new ConfigurationBuilder()
				.AddInMemoryCollection(new Dictionary<string, string?>
				{
					["GoogleCloud:ProjectId"] = "INVALID", // Invalid - uppercase
					["GoogleCloud:Region"] = "invalid-region",
					["GoogleCloud:Storage:BucketName"] = "ab", // Too short
					["GoogleCloud:PubSub:TopicName"] = "123topic" // Starts with number
				})
				.Build();

			var validator = new GoogleCloudConfigurationValidator();

			// Act
			var result = await validator.ValidateAsync(config, CancellationToken.None);

			// Assert
			result.IsValid.ShouldBeFalse();
			result.Errors.Count.ShouldBeGreaterThanOrEqualTo(4);
		}
		finally
		{
			Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", originalValue);
		}
	}

	#endregion
}
