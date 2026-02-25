// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Scheduling;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class AwsSchedulerOptionsShould
{
	[Fact]
	public void HaveCorrectEventBridgeSchedulerDefaults()
	{
		// Arrange & Act
		var options = new EventBridgeSchedulerOptions();

		// Assert
		options.Region.ShouldBe("us-east-1");
		options.RoleArn.ShouldBeNull();
		options.ScheduleGroupName.ShouldBe("default");
		options.TargetArn.ShouldBeNull();
		options.MaxRetries.ShouldBe(3);
		options.ScheduleTimeZone.ShouldBe("UTC");
		options.DeadLetterQueueArn.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingAllEventBridgeSchedulerProperties()
	{
		// Arrange & Act
		var options = new EventBridgeSchedulerOptions
		{
			Region = "eu-west-1",
			RoleArn = "arn:aws:iam::123456789:role/scheduler",
			ScheduleGroupName = "my-group",
			TargetArn = "arn:aws:sqs:eu-west-1:123456789:my-queue",
			MaxRetries = 5,
			ScheduleTimeZone = "Europe/London",
			DeadLetterQueueArn = "arn:aws:sqs:eu-west-1:123456789:dlq",
		};

		// Assert
		options.Region.ShouldBe("eu-west-1");
		options.RoleArn.ShouldBe("arn:aws:iam::123456789:role/scheduler");
		options.ScheduleGroupName.ShouldBe("my-group");
		options.TargetArn.ShouldBe("arn:aws:sqs:eu-west-1:123456789:my-queue");
		options.MaxRetries.ShouldBe(5);
		options.ScheduleTimeZone.ShouldBe("Europe/London");
		options.DeadLetterQueueArn.ShouldBe("arn:aws:sqs:eu-west-1:123456789:dlq");
	}

	[Fact]
	public void InheritFromBaseInAwsAlias()
	{
		// Arrange & Act
		var options = new AwsEventBridgeSchedulerOptions
		{
			Region = "ap-southeast-1",
			MaxRetries = 10,
		};

		// Assert - AwsEventBridgeSchedulerOptions inherits from EventBridgeSchedulerOptions
		options.ShouldBeAssignableTo<EventBridgeSchedulerOptions>();
		options.Region.ShouldBe("ap-southeast-1");
		options.MaxRetries.ShouldBe(10);
	}
}
