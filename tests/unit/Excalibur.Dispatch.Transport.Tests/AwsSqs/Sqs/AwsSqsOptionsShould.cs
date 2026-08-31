// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Sqs;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Platform")]
public sealed class AwsSqsOptionsShould
{
	[Fact]
	public void AllowSettingQueueUrl()
	{
		// Arrange & Act
		var options = new AwsSqsOptions
		{
			QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/my-queue"),
		};

		// Assert
		options.QueueUrl.ShouldNotBeNull();
	}

	[Fact]
	public void AllowSettingKmsEncryption()
	{
		// Arrange & Act
		var options = new AwsSqsOptions
		{
			KmsMasterKeyId = "alias/my-sqs-key",
			KmsDataKeyReusePeriodSeconds = 600,
		};

		// Assert
		options.KmsMasterKeyId.ShouldBe("alias/my-sqs-key");
		options.KmsDataKeyReusePeriodSeconds.ShouldBe(600);
	}

}
