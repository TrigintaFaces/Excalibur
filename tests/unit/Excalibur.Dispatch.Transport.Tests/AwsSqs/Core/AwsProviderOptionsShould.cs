// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Core;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Platform")]
public sealed class AwsProviderOptionsShould
{
	[Fact]
	public void AllowSettingRegion()
	{
		// Arrange & Act
		var options = new AwsProviderOptions { Region = "ap-southeast-1" };

		// Assert
		options.Region.ShouldBe("ap-southeast-1");
	}

	[Fact]
	public void AllowSettingLocalStackConfiguration()
	{
		// Arrange & Act
		var options = new AwsProviderOptions
		{
			Connection = new AwsSqsConnectionOptions
			{
				UseLocalStack = true,
				LocalStackUrl = new Uri("http://localstack:4566"),
			},
		};

		// Assert
		options.Connection.UseLocalStack.ShouldBeTrue();
		options.Connection.LocalStackUrl!.ToString().ShouldBe("http://localstack:4566/");
	}

	[Fact]
	public void AllowSettingRetryAndTimeout()
	{
		// Arrange & Act
		var options = new AwsProviderOptions
		{
			MaxRetryAttempts = 5,
			RequestTimeout = TimeSpan.FromSeconds(60),
		};

		// Assert
		options.MaxRetryAttempts.ShouldBe(5);
		options.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(60));
	}

}
