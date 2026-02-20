// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.Filtering;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class PubSubFilterOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new PubSubFilterOptions();

		// Assert
		options.FilterExpression.ShouldBeNull();
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new PubSubFilterOptions
		{
			FilterExpression = "attributes.type = \"order.created\"",
			Enabled = true,
		};

		// Assert
		options.FilterExpression.ShouldBe("attributes.type = \"order.created\"");
		options.Enabled.ShouldBeTrue();
	}
}
