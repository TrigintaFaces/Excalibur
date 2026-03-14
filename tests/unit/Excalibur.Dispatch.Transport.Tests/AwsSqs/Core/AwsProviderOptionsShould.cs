// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AwsProviderOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new AwsProviderOptions();

		// Assert
		options.Region.ShouldBe("us-east-1");
		options.Connection.Credentials.ShouldBeNull();
		options.Connection.ServiceUrl.ShouldBeNull();
		options.Connection.UseLocalStack.ShouldBeFalse();
		options.Connection.LocalStackUrl.ShouldNotBeNull();
		options.Connection.LocalStackUrl!.ToString().ShouldBe("http://localhost:4566/");
		options.MaxRetries.ShouldBe(3);
		options.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.Connection.ValidateOnStartup.ShouldBeTrue();
		options.Consumer.VisibilityTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.Consumer.WaitTimeSeconds.ShouldBe(TimeSpan.FromSeconds(20));
		options.Consumer.MaxNumberOfMessages.ShouldBe(10);
		options.EnableDeduplication.ShouldBeFalse();
		options.EnableEncryption.ShouldBeFalse();
		options.KmsKeyId.ShouldBeNull();
	}

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
			MaxRetries = 5,
			RequestTimeout = TimeSpan.FromSeconds(60),
		};

		// Assert
		options.MaxRetries.ShouldBe(5);
		options.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(60));
	}

	[Fact]
	public void AllowSettingEncryption()
	{
		// Arrange & Act
		var options = new AwsProviderOptions
		{
			EnableEncryption = true,
			KmsKeyId = "alias/my-key",
		};

		// Assert
		options.EnableEncryption.ShouldBeTrue();
		options.KmsKeyId.ShouldBe("alias/my-key");
	}
}
