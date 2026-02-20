// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.EventBridge;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class AwsEventBridgeTransportOptionsShould
{
	[Fact]
	public void HaveDefaultValues()
	{
		var options = new AwsEventBridgeTransportOptions();

		options.Name.ShouldBeNull();
		options.Region.ShouldBeNull();
		options.EventBusName.ShouldBeNull();
		options.DefaultSource.ShouldBeNull();
		options.DefaultDetailType.ShouldBeNull();
		options.EnableArchiving.ShouldBeFalse();
		options.ArchiveName.ShouldBeNull();
		options.ArchiveRetentionDays.ShouldBe(7);
		options.DetailTypeMappings.ShouldNotBeNull();
		options.DetailTypeMappings.ShouldBeEmpty();
	}

	[Fact]
	public void AllowSettingProperties()
	{
		var options = new AwsEventBridgeTransportOptions
		{
			Name = "test",
			Region = "us-east-1",
			EventBusName = "my-bus",
			DefaultSource = "com.test",
			DefaultDetailType = "TestEvent",
			EnableArchiving = true,
			ArchiveName = "my-archive",
			ArchiveRetentionDays = 30,
		};

		options.Name.ShouldBe("test");
		options.Region.ShouldBe("us-east-1");
		options.EventBusName.ShouldBe("my-bus");
		options.DefaultSource.ShouldBe("com.test");
		options.DefaultDetailType.ShouldBe("TestEvent");
		options.EnableArchiving.ShouldBeTrue();
		options.ArchiveName.ShouldBe("my-archive");
		options.ArchiveRetentionDays.ShouldBe(30);
	}

	[Fact]
	public void SupportDetailTypeMappings()
	{
		var options = new AwsEventBridgeTransportOptions();

		options.DetailTypeMappings[typeof(string)] = "StringEvent";
		options.DetailTypeMappings[typeof(int)] = "IntEvent";

		options.DetailTypeMappings.Count.ShouldBe(2);
		options.DetailTypeMappings[typeof(string)].ShouldBe("StringEvent");
	}
}
