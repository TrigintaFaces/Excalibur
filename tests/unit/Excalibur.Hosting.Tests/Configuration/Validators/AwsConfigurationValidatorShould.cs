// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Configuration.Validators;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Hosting.Tests.Configuration.Validators;

/// <summary>
/// Unit tests for <see cref="AwsConfigurationValidator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Feature", "Configuration")]
public sealed class AwsConfigurationValidatorShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void SetDefaultConfigurationName()
	{
		// Act
		var validator = new AwsConfigurationValidator();

		// Assert
		validator.ConfigurationName.ShouldBe("AWS:AWS");
	}

	[Fact]
	public void SetCustomConfigurationName()
	{
		// Act
		var validator = new AwsConfigurationValidator("CustomSection");

		// Assert
		validator.ConfigurationName.ShouldBe("AWS:CustomSection");
	}

	[Fact]
	public void SetPriorityTo20()
	{
		// Act
		var validator = new AwsConfigurationValidator();

		// Assert
		validator.Priority.ShouldBe(20);
	}

	#endregion

	#region Region Validation Tests

	[Theory]
	[InlineData("us-east-1")]
	[InlineData("us-west-2")]
	[InlineData("eu-west-1")]
	[InlineData("ap-southeast-1")]
	public async Task ReturnSuccess_WhenRegionIsValid(string region)
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AWS:Region"] = region,
				["AWS:AccessKeyId"] = "AKIAIOSFODNN7EXAMPLE",
				["AWS:SecretAccessKey"] = "secret"
			})
			.Build();

		var validator = new AwsConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnSuccess_WhenDefaultRegionIsUsed()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AWS:DefaultRegion"] = "us-east-1",
				["AWS:AccessKeyId"] = "AKIAIOSFODNN7EXAMPLE",
				["AWS:SecretAccessKey"] = "secret"
			})
			.Build();

		var validator = new AwsConfigurationValidator();

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
				["AWS:Region"] = "invalid-region",
				["AWS:AccessKeyId"] = "AKIAIOSFODNN7EXAMPLE",
				["AWS:SecretAccessKey"] = "secret"
			})
			.Build();

		var validator = new AwsConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Invalid cloud region"));
	}

	[Fact]
	public async Task ReturnFailure_WhenRegionIsMissing()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AWS:AccessKeyId"] = "AKIAIOSFODNN7EXAMPLE",
				["AWS:SecretAccessKey"] = "secret"
			})
			.Build();

		var validator = new AwsConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Cloud region is missing"));
	}

	#endregion

	#region Authentication Tests

	[Fact]
	public async Task ReturnSuccess_WhenAccessKeyAndSecretAreProvided()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AWS:Region"] = "us-east-1",
				["AWS:AccessKeyId"] = "AKIAIOSFODNN7EXAMPLE",
				["AWS:SecretAccessKey"] = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY"
			})
			.Build();

		var validator = new AwsConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnSuccess_WhenProfileIsProvided()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AWS:Region"] = "us-east-1",
				["AWS:Profile"] = "default"
			})
			.Build();

		var validator = new AwsConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnSuccess_WhenRoleArnIsProvided()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AWS:Region"] = "us-east-1",
				["AWS:RoleArn"] = "arn:aws:iam::123456789012:role/MyRole"
			})
			.Build();

		var validator = new AwsConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenNoAuthenticationMethodProvided()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AWS:Region"] = "us-east-1"
			})
			.Build();

		var validator = new AwsConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("No AWS authentication method"));
	}

	[Theory]
	[InlineData("AKIAIOSFODNN7EXAMPLE")] // 20 chars, starts with AKIA
	[InlineData("ASIAIOSFODNN7EXAMPLE")] // Temporary credentials
	[InlineData("AIDAIOSFODNN7EXAMPLE")] // IAM user
	public async Task ReturnSuccess_WhenAccessKeyIdIsValidFormat(string accessKeyId)
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AWS:Region"] = "us-east-1",
				["AWS:AccessKeyId"] = accessKeyId,
				["AWS:SecretAccessKey"] = "secret"
			})
			.Build();

		var validator = new AwsConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenAccessKeyIdIsInvalidFormat()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AWS:Region"] = "us-east-1",
				["AWS:AccessKeyId"] = "invalid-key",
				["AWS:SecretAccessKey"] = "secret"
			})
			.Build();

		var validator = new AwsConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Invalid AWS Access Key ID format"));
	}

	[Fact]
	public async Task ReturnFailure_WhenRoleArnIsInvalidFormat()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AWS:Region"] = "us-east-1",
				["AWS:RoleArn"] = "invalid-arn"
			})
			.Build();

		var validator = new AwsConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Invalid ARN format"));
	}

	#endregion

	#region S3 Configuration Tests

	[Fact]
	public async Task ReturnSuccess_WhenS3BucketNameIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AWS:Region"] = "us-east-1",
				["AWS:Profile"] = "default",
				["AWS:S3:BucketName"] = "my-valid-bucket"
			})
			.Build();

		var validator = new AwsConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenS3BucketNameIsTooShort()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AWS:Region"] = "us-east-1",
				["AWS:Profile"] = "default",
				["AWS:S3:BucketName"] = "ab"
			})
			.Build();

		var validator = new AwsConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("between 3 and 63 characters"));
	}

	[Fact]
	public async Task ReturnFailure_WhenS3BucketNameIsTooLong()
	{
		// Arrange
		var longName = new string('a', 64);
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AWS:Region"] = "us-east-1",
				["AWS:Profile"] = "default",
				["AWS:S3:BucketName"] = longName
			})
			.Build();

		var validator = new AwsConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("between 3 and 63 characters"));
	}

	[Fact]
	public async Task ReturnFailure_WhenS3BucketNameHasInvalidFormat()
	{
		// Arrange - bucket name with uppercase or invalid chars
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AWS:Region"] = "us-east-1",
				["AWS:Profile"] = "default",
				["AWS:S3:BucketName"] = "-invalid-bucket"
			})
			.Build();

		var validator = new AwsConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Invalid S3 bucket name format"));
	}

	#endregion

	#region SQS Configuration Tests

	[Fact]
	public async Task ReturnSuccess_WhenSqsQueueUrlIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AWS:Region"] = "us-east-1",
				["AWS:Profile"] = "default",
				["AWS:SQS:QueueUrl"] = "https://sqs.us-east-1.amazonaws.com/123456789012/my-queue"
			})
			.Build();

		var validator = new AwsConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenSqsQueueUrlIsInvalid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AWS:Region"] = "us-east-1",
				["AWS:Profile"] = "default",
				["AWS:SQS:QueueUrl"] = "https://example.com/queue"
			})
			.Build();

		var validator = new AwsConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Invalid SQS queue URL"));
	}

	[Fact]
	public async Task ReturnSuccess_WhenMessageRetentionPeriodIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AWS:Region"] = "us-east-1",
				["AWS:Profile"] = "default",
				["AWS:SQS:MessageRetentionPeriod"] = "86400" // 1 day
			})
			.Build();

		var validator = new AwsConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenMessageRetentionPeriodIsTooShort()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AWS:Region"] = "us-east-1",
				["AWS:Profile"] = "default",
				["AWS:SQS:MessageRetentionPeriod"] = "30" // Less than 60
			})
			.Build();

		var validator = new AwsConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("retention period out of range"));
	}

	[Fact]
	public async Task ReturnFailure_WhenMessageRetentionPeriodIsTooLong()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AWS:Region"] = "us-east-1",
				["AWS:Profile"] = "default",
				["AWS:SQS:MessageRetentionPeriod"] = "2000000" // More than 14 days
			})
			.Build();

		var validator = new AwsConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("retention period out of range"));
	}

	#endregion

	#region SNS Configuration Tests

	[Fact]
	public async Task ReturnSuccess_WhenSnsTopicArnIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AWS:Region"] = "us-east-1",
				["AWS:Profile"] = "default",
				["AWS:SNS:TopicArn"] = "arn:aws:sns:us-east-1:123456789012:my-topic"
			})
			.Build();

		var validator = new AwsConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenSnsTopicArnIsInvalid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AWS:Region"] = "us-east-1",
				["AWS:Profile"] = "default",
				["AWS:SNS:TopicArn"] = "invalid-arn"
			})
			.Build();

		var validator = new AwsConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Invalid ARN format"));
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
				["CustomAws:Region"] = "us-east-1",
				["CustomAws:Profile"] = "default"
			})
			.Build();

		var validator = new AwsConfigurationValidator("CustomAws");

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	#endregion
}
