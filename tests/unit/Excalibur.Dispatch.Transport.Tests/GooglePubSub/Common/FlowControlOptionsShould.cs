// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.Common;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class FlowControlOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new FlowControlOptions();

		// Assert
		options.MaxOutstandingMessages.ShouldBe(1000);
		options.MaxOutstandingBytes.ShouldBe(100_000_000);
		options.LimitExceededBehavior.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new FlowControlOptions
		{
			MaxOutstandingMessages = 500,
			MaxOutstandingBytes = 50_000_000,
			LimitExceededBehavior = false,
		};

		// Assert
		options.MaxOutstandingMessages.ShouldBe(500);
		options.MaxOutstandingBytes.ShouldBe(50_000_000);
		options.LimitExceededBehavior.ShouldBeFalse();
	}
}
