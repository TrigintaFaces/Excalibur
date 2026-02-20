// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.LongPolling;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class ReceiveOptionsShould
{
	[Fact]
	public void HaveNullDefaults()
	{
		// Arrange & Act
		var options = new ReceiveOptions();

		// Assert
		options.MaxNumberOfMessages.ShouldBeNull();
		options.WaitTime.ShouldBeNull();
		options.VisibilityTimeout.ShouldBeNull();
		options.MessageAttributeNames.ShouldBeNull();
		options.AttributeNames.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new ReceiveOptions
		{
			MaxNumberOfMessages = 10,
			WaitTime = TimeSpan.FromSeconds(20),
			VisibilityTimeout = TimeSpan.FromSeconds(30),
			MessageAttributeNames = ["attr1", "attr2"],
			AttributeNames = ["All"],
		};

		// Assert
		options.MaxNumberOfMessages.ShouldBe(10);
		options.WaitTime.ShouldBe(TimeSpan.FromSeconds(20));
		options.VisibilityTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.MessageAttributeNames.ShouldNotBeNull();
		options.MessageAttributeNames!.Count.ShouldBe(2);
		options.AttributeNames.ShouldNotBeNull();
		options.AttributeNames!.Count.ShouldBe(1);
	}
}
