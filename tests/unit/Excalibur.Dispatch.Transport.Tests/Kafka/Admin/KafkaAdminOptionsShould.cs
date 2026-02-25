// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.Admin;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class KafkaAdminOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new KafkaAdminOptions();

		// Assert
		options.BootstrapServers.ShouldBe(string.Empty);
		options.OperationTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void AllowSettingProperties()
	{
		// Arrange & Act
		var options = new KafkaAdminOptions
		{
			BootstrapServers = "broker1:9092,broker2:9092",
			OperationTimeout = TimeSpan.FromMinutes(1),
		};

		// Assert
		options.BootstrapServers.ShouldBe("broker1:9092,broker2:9092");
		options.OperationTimeout.ShouldBe(TimeSpan.FromMinutes(1));
	}
}
