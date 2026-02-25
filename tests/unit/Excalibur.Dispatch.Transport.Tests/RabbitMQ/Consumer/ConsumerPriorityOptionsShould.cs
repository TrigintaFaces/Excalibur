// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ.Consumer;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class ConsumerPriorityOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new ConsumerPriorityOptions();

		// Assert
		options.Priority.ShouldBe(0);
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new ConsumerPriorityOptions
		{
			Priority = 10,
			Enabled = true,
		};

		// Assert
		options.Priority.ShouldBe(10);
		options.Enabled.ShouldBeTrue();
	}
}
