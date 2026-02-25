// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AwsMessageBusOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new AwsMessageBusOptions();

		// Assert
		options.ServiceUrl.ShouldBeNull();
		options.Region.ShouldBe("us-east-1");
		options.UseLocalStack.ShouldBeFalse();
		options.EnableSqs.ShouldBeTrue();
		options.EnableSns.ShouldBeTrue();
		options.EnableEventBridge.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingServiceUrl()
	{
		// Arrange & Act
		var options = new AwsMessageBusOptions
		{
			ServiceUrl = new Uri("http://localhost:4566"),
		};

		// Assert
		options.ServiceUrl.ShouldNotBeNull();
		options.ServiceUrl!.ToString().ShouldBe("http://localhost:4566/");
	}

	[Fact]
	public void AllowSettingRegion()
	{
		// Arrange & Act
		var options = new AwsMessageBusOptions { Region = "eu-west-1" };

		// Assert
		options.Region.ShouldBe("eu-west-1");
	}

	[Fact]
	public void AllowEnablingLocalStack()
	{
		// Arrange & Act
		var options = new AwsMessageBusOptions { UseLocalStack = true };

		// Assert
		options.UseLocalStack.ShouldBeTrue();
	}

	[Fact]
	public void AllowDisablingServices()
	{
		// Arrange & Act
		var options = new AwsMessageBusOptions
		{
			EnableSqs = false,
			EnableSns = false,
			EnableEventBridge = false,
		};

		// Assert
		options.EnableSqs.ShouldBeFalse();
		options.EnableSns.ShouldBeFalse();
		options.EnableEventBridge.ShouldBeFalse();
	}
}
