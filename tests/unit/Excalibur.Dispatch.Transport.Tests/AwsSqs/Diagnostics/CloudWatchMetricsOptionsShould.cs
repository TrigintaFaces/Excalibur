// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.AwsSqs;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class CloudWatchMetricsOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new CloudWatchMetricsOptions();

		// Assert
		options.Namespace.ShouldBe(string.Empty);
		options.Region.ShouldBeNull();
		options.PublishInterval.ShouldBe(TimeSpan.FromSeconds(60));
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new CloudWatchMetricsOptions
		{
			Namespace = "MyApp/Dispatch",
			Region = "eu-west-1",
			PublishInterval = TimeSpan.FromSeconds(30),
		};

		// Assert
		options.Namespace.ShouldBe("MyApp/Dispatch");
		options.Region.ShouldBe("eu-west-1");
		options.PublishInterval.ShouldBe(TimeSpan.FromSeconds(30));
	}
}
