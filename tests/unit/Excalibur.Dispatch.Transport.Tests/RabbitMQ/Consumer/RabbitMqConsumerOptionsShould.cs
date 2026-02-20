// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

using RabbitMqRetryPolicy = Excalibur.Dispatch.Transport.RabbitMQ.RetryPolicy;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ.Consumer;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class RabbitMqConsumerOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new RabbitMqConsumerOptions();

		// Assert
		options.AckMode.ShouldBe(AckMode.Manual);
		options.RetryPolicy.ShouldNotBeNull();
		options.DeadLetterExchange.ShouldBeNull();
		options.DeadLetterRoutingKey.ShouldBeNull();
		options.RequeueOnReject.ShouldBeFalse();
		options.PrefetchCount.ShouldBe((ushort)100);
		options.PrefetchGlobal.ShouldBeFalse();
		options.BatchAckSize.ShouldBe(100);
		options.BatchAckTimeout.ShouldBe(TimeSpan.FromMilliseconds(100));
		options.ConsumerTag.ShouldBe("dispatch-consumer");
		options.IncludeDeathHeaders.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new RabbitMqConsumerOptions
		{
			AckMode = AckMode.Batch,
			RetryPolicy = RabbitMqRetryPolicy.Fixed(maxRetries: 5, delay: TimeSpan.FromSeconds(2)),
			DeadLetterExchange = "dlx",
			DeadLetterRoutingKey = "dlq-key",
			RequeueOnReject = true,
			PrefetchCount = 50,
			PrefetchGlobal = true,
			BatchAckSize = 200,
			BatchAckTimeout = TimeSpan.FromMilliseconds(500),
			ConsumerTag = "my-consumer",
			IncludeDeathHeaders = false,
		};

		// Assert
		options.AckMode.ShouldBe(AckMode.Batch);
		options.DeadLetterExchange.ShouldBe("dlx");
		options.DeadLetterRoutingKey.ShouldBe("dlq-key");
		options.RequeueOnReject.ShouldBeTrue();
		options.PrefetchCount.ShouldBe((ushort)50);
		options.PrefetchGlobal.ShouldBeTrue();
		options.BatchAckSize.ShouldBe(200);
		options.BatchAckTimeout.ShouldBe(TimeSpan.FromMilliseconds(500));
		options.ConsumerTag.ShouldBe("my-consumer");
		options.IncludeDeathHeaders.ShouldBeFalse();
	}
}
