// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ.DeadLetter;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class RabbitMqDeadLetterOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new RabbitMqDeadLetterOptions();

		// Assert
		options.Exchange.ShouldBe("dead-letters");
		options.QueueName.ShouldBe("dead-letter-queue");
		options.RoutingKey.ShouldBe("#");
		options.IncludeStackTrace.ShouldBeTrue();
		options.MaxBatchSize.ShouldBe(100);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new RabbitMqDeadLetterOptions
		{
			Exchange = "custom-dlx",
			QueueName = "custom-dlq",
			RoutingKey = "errors.*",
			IncludeStackTrace = false,
			MaxBatchSize = 50,
		};

		// Assert
		options.Exchange.ShouldBe("custom-dlx");
		options.QueueName.ShouldBe("custom-dlq");
		options.RoutingKey.ShouldBe("errors.*");
		options.IncludeStackTrace.ShouldBeFalse();
		options.MaxBatchSize.ShouldBe(50);
	}
}
