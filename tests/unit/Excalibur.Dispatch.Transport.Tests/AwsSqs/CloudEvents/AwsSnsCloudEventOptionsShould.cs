// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.CloudEvents;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AwsSnsCloudEventOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new AwsSnsCloudEventOptions();

		// Assert
		options.IncludeMessageAttributes.ShouldBeTrue();
		options.EnableMessageFiltering.ShouldBeFalse();
		options.DefaultSubject.ShouldBe("CloudEvent");
		options.UseFifoFeatures.ShouldBeFalse();
		options.DefaultMessageGroupId.ShouldBeNull();
		options.EnableContentBasedDeduplication.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new AwsSnsCloudEventOptions
		{
			IncludeMessageAttributes = false,
			EnableMessageFiltering = true,
			DefaultSubject = "CustomSubject",
			UseFifoFeatures = true,
			DefaultMessageGroupId = "fifo-group",
			EnableContentBasedDeduplication = true,
		};

		// Assert
		options.IncludeMessageAttributes.ShouldBeFalse();
		options.EnableMessageFiltering.ShouldBeTrue();
		options.DefaultSubject.ShouldBe("CustomSubject");
		options.UseFifoFeatures.ShouldBeTrue();
		options.DefaultMessageGroupId.ShouldBe("fifo-group");
		options.EnableContentBasedDeduplication.ShouldBeTrue();
	}
}
