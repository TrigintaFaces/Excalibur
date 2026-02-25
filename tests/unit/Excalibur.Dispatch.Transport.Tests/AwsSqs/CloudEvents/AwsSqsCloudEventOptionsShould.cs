// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.CloudEvents;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AwsSqsCloudEventOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new AwsSqsCloudEventOptions();

		// Assert
		options.MaxBatchSize.ShouldBe(10);
		options.UseFifoFeatures.ShouldBeFalse();
		options.DefaultMessageGroupId.ShouldBeNull();
		options.EnableContentBasedDeduplication.ShouldBeFalse();
		options.DelaySeconds.ShouldBe(0);
		options.EnablePayloadCompression.ShouldBeFalse();
		options.CompressionThreshold.ShouldBe(64 * 1024);
		options.EnableDoDCompliance.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new AwsSqsCloudEventOptions
		{
			MaxBatchSize = 5,
			UseFifoFeatures = true,
			DefaultMessageGroupId = "group-1",
			EnableContentBasedDeduplication = true,
			DelaySeconds = 60,
			EnablePayloadCompression = true,
			CompressionThreshold = 128 * 1024,
			EnableDoDCompliance = true,
		};

		// Assert
		options.MaxBatchSize.ShouldBe(5);
		options.UseFifoFeatures.ShouldBeTrue();
		options.DefaultMessageGroupId.ShouldBe("group-1");
		options.EnableContentBasedDeduplication.ShouldBeTrue();
		options.DelaySeconds.ShouldBe(60);
		options.EnablePayloadCompression.ShouldBeTrue();
		options.CompressionThreshold.ShouldBe(128 * 1024);
		options.EnableDoDCompliance.ShouldBeTrue();
	}
}
