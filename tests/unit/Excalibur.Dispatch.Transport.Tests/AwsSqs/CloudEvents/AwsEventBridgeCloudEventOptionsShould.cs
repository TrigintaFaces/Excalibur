// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.CloudEvents;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AwsEventBridgeCloudEventOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new AwsEventBridgeCloudEventOptions();

		// Assert
		options.EventBusName.ShouldBe("default");
		options.SourcePrefix.ShouldBe("dispatch.cloudevents");
		options.UseCloudEventTypeAsDetailType.ShouldBeTrue();
		options.IncludeExtensionsInDetail.ShouldBeTrue();
		options.MaxBatchSize.ShouldBe(10);
		options.EnableReplay.ShouldBeFalse();
		options.ReplayArchiveName.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new AwsEventBridgeCloudEventOptions
		{
			EventBusName = "custom-bus",
			SourcePrefix = "myapp.events",
			UseCloudEventTypeAsDetailType = false,
			IncludeExtensionsInDetail = false,
			MaxBatchSize = 5,
			EnableReplay = true,
			ReplayArchiveName = "archive-1",
		};

		// Assert
		options.EventBusName.ShouldBe("custom-bus");
		options.SourcePrefix.ShouldBe("myapp.events");
		options.UseCloudEventTypeAsDetailType.ShouldBeFalse();
		options.IncludeExtensionsInDetail.ShouldBeFalse();
		options.MaxBatchSize.ShouldBe(5);
		options.EnableReplay.ShouldBeTrue();
		options.ReplayArchiveName.ShouldBe("archive-1");
	}
}
