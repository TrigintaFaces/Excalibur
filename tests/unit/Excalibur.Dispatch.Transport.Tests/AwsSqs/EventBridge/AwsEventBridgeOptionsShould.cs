// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.EventBridge;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AwsEventBridgeOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new AwsEventBridgeOptions();

		// Assert
		options.EventBusName.ShouldBe(string.Empty);
		options.EnableEncryption.ShouldBeFalse();
		options.DefaultSource.ShouldBe("Excalibur.Dispatch.Transport");
		options.DefaultDetailType.ShouldBe(string.Empty);
		options.RuleNames.ShouldBeEmpty();
		options.RetryPolicy.ShouldBeNull();
		options.EnableArchiving.ShouldBeFalse();
		options.ArchiveName.ShouldBeNull();
		options.ArchiveRetentionDays.ShouldBe(7);
	}

	[Fact]
	public void AllowSettingEventBusConfiguration()
	{
		// Arrange & Act
		var options = new AwsEventBridgeOptions
		{
			EventBusName = "my-bus",
			DefaultSource = "my-app",
			DefaultDetailType = "OrderCreated",
		};

		// Assert
		options.EventBusName.ShouldBe("my-bus");
		options.DefaultSource.ShouldBe("my-app");
		options.DefaultDetailType.ShouldBe("OrderCreated");
	}

	[Fact]
	public void AllowAddingRuleNames()
	{
		// Arrange
		var options = new AwsEventBridgeOptions();

		// Act
		options.RuleNames.Add("rule-1");
		options.RuleNames.Add("rule-2");

		// Assert
		options.RuleNames.Count.ShouldBe(2);
		options.RuleNames.ShouldContain("rule-1");
		options.RuleNames.ShouldContain("rule-2");
	}

	[Fact]
	public void AllowSettingArchivingConfiguration()
	{
		// Arrange & Act
		var options = new AwsEventBridgeOptions
		{
			EnableArchiving = true,
			ArchiveName = "my-archive",
			ArchiveRetentionDays = 30,
		};

		// Assert
		options.EnableArchiving.ShouldBeTrue();
		options.ArchiveName.ShouldBe("my-archive");
		options.ArchiveRetentionDays.ShouldBe(30);
	}

	[Fact]
	public void InheritFromAwsProviderOptions()
	{
		// Arrange & Act
		var options = new AwsEventBridgeOptions
		{
			Region = "eu-west-1",
			MaxRetries = 5,
		};

		// Assert
		options.Region.ShouldBe("eu-west-1");
		options.MaxRetries.ShouldBe(5);
	}
}
