// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Sns;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AwsSnsOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new AwsSnsOptions();

		// Assert
		options.TopicArn.ShouldBe(string.Empty);
		options.EnableEncryption.ShouldBeFalse();
		options.EnableDeduplication.ShouldBeFalse();
		options.ContentBasedDeduplication.ShouldBeFalse();
		options.DefaultAttributes.ShouldBeEmpty();
		options.RawMessageDelivery.ShouldBeFalse();
		options.DisplayName.ShouldBe(string.Empty);
		options.KmsMasterKeyId.ShouldBeNull();
		options.ServiceUrl.ShouldBeNull();
		options.MaxErrorRetry.ShouldBe(3);
		options.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.ReadWriteTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.UseHttp.ShouldBeFalse();
		options.RegionEndpoint.ShouldBeNull();
		options.UseLocalStack.ShouldBeFalse();
		options.AccessKey.ShouldBeNull();
		options.SecretKey.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingTopicConfiguration()
	{
		// Arrange & Act
		var options = new AwsSnsOptions
		{
			TopicArn = "arn:aws:sns:us-east-1:123456789:my-topic",
			DisplayName = "My Topic",
			ContentBasedDeduplication = true,
		};

		// Assert
		options.TopicArn.ShouldBe("arn:aws:sns:us-east-1:123456789:my-topic");
		options.DisplayName.ShouldBe("My Topic");
		options.ContentBasedDeduplication.ShouldBeTrue();
	}

	[Fact]
	public void AllowAddingDefaultAttributes()
	{
		// Arrange
		var options = new AwsSnsOptions();

		// Act
		options.DefaultAttributes["env"] = "production";
		options.DefaultAttributes["service"] = "orders";

		// Assert
		options.DefaultAttributes.Count.ShouldBe(2);
		options.DefaultAttributes["env"].ShouldBe("production");
	}

	[Fact]
	public void AllowSettingEncryptionConfiguration()
	{
		// Arrange & Act
		var options = new AwsSnsOptions
		{
			EnableEncryption = true,
			KmsMasterKeyId = "alias/my-key",
		};

		// Assert
		options.EnableEncryption.ShouldBeTrue();
		options.KmsMasterKeyId.ShouldBe("alias/my-key");
	}

	[Fact]
	public void AllowSettingConnectionConfiguration()
	{
		// Arrange & Act
		var options = new AwsSnsOptions
		{
			ServiceUrl = new Uri("http://localhost:4566"),
			UseLocalStack = true,
			UseHttp = true,
			AccessKey = "test-key",
			SecretKey = "test-secret",
			RegionEndpoint = "us-west-2",
		};

		// Assert
		options.ServiceUrl!.ToString().ShouldBe("http://localhost:4566/");
		options.UseLocalStack.ShouldBeTrue();
		options.UseHttp.ShouldBeTrue();
		options.AccessKey.ShouldBe("test-key");
		options.SecretKey.ShouldBe("test-secret");
		options.RegionEndpoint.ShouldBe("us-west-2");
	}

	[Fact]
	public void InheritFromAwsProviderOptions()
	{
		// Arrange & Act
		var options = new AwsSnsOptions
		{
			Region = "ap-southeast-1",
		};

		// Assert
		options.Region.ShouldBe("ap-southeast-1");
	}
}
