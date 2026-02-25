// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.DeadLetterQueue;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class DlqOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new DlqOptions();

		// Assert
		options.DeadLetterQueueUrl.ShouldBeNull();
		options.MaxRetries.ShouldBe(3);
		options.RetryDelay.ShouldBe(TimeSpan.FromMinutes(5));
		options.UseExponentialBackoff.ShouldBeTrue();
		options.MaxMessageAge.ShouldBe(TimeSpan.FromDays(14));
		options.ArchiveFailedMessages.ShouldBeTrue();
		options.ArchiveLocation.ShouldBeNull();
		options.BatchSize.ShouldBe(10);
		options.EnableAutomaticRedrive.ShouldBeFalse();
		options.AutomaticRedriveInterval.ShouldBe(TimeSpan.FromHours(1));
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new DlqOptions
		{
			DeadLetterQueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123/dlq"),
			MaxRetries = 5,
			RetryDelay = TimeSpan.FromMinutes(10),
			UseExponentialBackoff = false,
			MaxMessageAge = TimeSpan.FromDays(7),
			ArchiveFailedMessages = false,
			ArchiveLocation = "s3://my-bucket/archive",
			BatchSize = 5,
			EnableAutomaticRedrive = true,
			AutomaticRedriveInterval = TimeSpan.FromMinutes(30),
		};

		// Assert
		options.DeadLetterQueueUrl.ShouldNotBeNull();
		options.MaxRetries.ShouldBe(5);
		options.RetryDelay.ShouldBe(TimeSpan.FromMinutes(10));
		options.UseExponentialBackoff.ShouldBeFalse();
		options.MaxMessageAge.ShouldBe(TimeSpan.FromDays(7));
		options.ArchiveFailedMessages.ShouldBeFalse();
		options.ArchiveLocation.ShouldBe("s3://my-bucket/archive");
		options.BatchSize.ShouldBe(5);
		options.EnableAutomaticRedrive.ShouldBeTrue();
		options.AutomaticRedriveInterval.ShouldBe(TimeSpan.FromMinutes(30));
	}
}
