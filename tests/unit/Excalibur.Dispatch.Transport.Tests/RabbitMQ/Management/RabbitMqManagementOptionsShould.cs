// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ.Management;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class RabbitMqManagementOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new RabbitMqManagementOptions();

		// Assert
		options.BaseUrl.ShouldBe("http://localhost:15672");
		options.Username.ShouldBe("guest");
		options.Password.ShouldBe("guest");
		options.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new RabbitMqManagementOptions
		{
			BaseUrl = "https://rabbitmq.example.com:15672",
			Username = "admin",
			Password = "secret",
			RequestTimeout = TimeSpan.FromMinutes(1),
		};

		// Assert
		options.BaseUrl.ShouldBe("https://rabbitmq.example.com:15672");
		options.Username.ShouldBe("admin");
		options.Password.ShouldBe("secret");
		options.RequestTimeout.ShouldBe(TimeSpan.FromMinutes(1));
	}
}
