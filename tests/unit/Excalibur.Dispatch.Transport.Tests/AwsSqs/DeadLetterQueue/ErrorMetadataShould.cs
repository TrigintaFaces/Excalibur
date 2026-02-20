// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.DeadLetterQueue;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class ErrorMetadataShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var metadata = new ErrorMetadata();

		// Assert
		metadata.Source.ShouldBeNull();
		metadata.Category.ShouldBeNull();
		metadata.Properties.ShouldNotBeNull();
		metadata.Properties.ShouldBeEmpty();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var metadata = new ErrorMetadata
		{
			Source = "SqsReceiver",
			Category = "Infrastructure",
		};
		metadata.Properties["retryable"] = true;
		metadata.Properties["code"] = "AWS_SQS_TIMEOUT";

		// Assert
		metadata.Source.ShouldBe("SqsReceiver");
		metadata.Category.ShouldBe("Infrastructure");
		metadata.Properties.Count.ShouldBe(2);
		metadata.Properties["retryable"].ShouldBe(true);
		metadata.Properties["code"].ShouldBe("AWS_SQS_TIMEOUT");
	}
}
