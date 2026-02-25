using Excalibur.Jobs.CloudProviders.Aws;

namespace Excalibur.Jobs.Tests.CloudProviders;

/// <summary>
/// Unit tests for AwsSchedulerOptions configuration.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AwsSchedulerOptionsShould : UnitTestBase
{
	[Fact]
	public void Create_WithRequiredProperties_SetsDefaultValues()
	{
		// Arrange & Act
		var options = new AwsSchedulerOptions
		{
			TargetArn = "arn:aws:lambda:us-east-1:123456789:function:test",
			ExecutionRoleArn = "arn:aws:iam::123456789:role/test"
		};

		// Assert
		options.TimeZone.ShouldBe("UTC");
		options.ScheduleGroup.ShouldBe("default");
		options.MaximumEventAgeInSeconds.ShouldBe(86400);
		options.RetryPolicyMaximumRetryAttempts.ShouldBe(3);
	}

	[Fact]
	public void TargetArn_CanBeSet()
	{
		// Arrange
		var options = new AwsSchedulerOptions
		{
			TargetArn = "arn:aws:sqs:us-west-2:123456789:my-queue",
			ExecutionRoleArn = "arn:aws:iam::123456789:role/test"
		};

		// Assert
		options.TargetArn.ShouldBe("arn:aws:sqs:us-west-2:123456789:my-queue");
	}

	[Fact]
	public void TimeZone_CanBeCustomized()
	{
		// Arrange
		var options = new AwsSchedulerOptions
		{
			TargetArn = "arn:aws:lambda:us-east-1:123456789:function:test",
			ExecutionRoleArn = "arn:aws:iam::123456789:role/test"
		};

		// Act
		options.TimeZone = "America/New_York";

		// Assert
		options.TimeZone.ShouldBe("America/New_York");
	}

	[Fact]
	public void ScheduleGroup_CanBeCustomized()
	{
		// Arrange
		var options = new AwsSchedulerOptions
		{
			TargetArn = "arn:aws:lambda:us-east-1:123456789:function:test",
			ExecutionRoleArn = "arn:aws:iam::123456789:role/test"
		};

		// Act
		options.ScheduleGroup = "production-jobs";

		// Assert
		options.ScheduleGroup.ShouldBe("production-jobs");
	}

	[Fact]
	public void MaximumEventAgeInSeconds_CanBeCustomized()
	{
		// Arrange
		var options = new AwsSchedulerOptions
		{
			TargetArn = "arn:aws:lambda:us-east-1:123456789:function:test",
			ExecutionRoleArn = "arn:aws:iam::123456789:role/test"
		};

		// Act
		options.MaximumEventAgeInSeconds = 3600;

		// Assert
		options.MaximumEventAgeInSeconds.ShouldBe(3600);
	}

	[Fact]
	public void RetryPolicyMaximumRetryAttempts_CanBeCustomized()
	{
		// Arrange
		var options = new AwsSchedulerOptions
		{
			TargetArn = "arn:aws:lambda:us-east-1:123456789:function:test",
			ExecutionRoleArn = "arn:aws:iam::123456789:role/test"
		};

		// Act
		options.RetryPolicyMaximumRetryAttempts = 5;

		// Assert
		options.RetryPolicyMaximumRetryAttempts.ShouldBe(5);
	}
}
