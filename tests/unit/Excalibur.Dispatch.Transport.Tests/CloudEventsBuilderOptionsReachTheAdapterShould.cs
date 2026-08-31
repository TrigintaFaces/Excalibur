// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;
using Excalibur.Dispatch.Transport.Google;
using Excalibur.Dispatch.Transport.Kafka;
using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests;

/// <summary>
/// A value set through a transport builder's <c>ConfigureCloudEvents</c> must reach the options the
/// CloudEvents adapter actually resolves.
/// </summary>
/// <remarks>
/// Each builder wrote the value onto its transport options object, while every adapter binds
/// <c>IOptions&lt;XCloudEventOptions&gt;</c> from DI — a different type on a different object. A consumer
/// configuring CloudEvents through the builder therefore got no effect and no error. These arms assert the
/// OPTIONS THE ADAPTER READS, not that a registration exists.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class CloudEventsBuilderOptionsReachTheAdapterShould
{
	[Fact]
	public void CarryTheConfiguredValue_ForAwsSqs()
	{
		var services = new ServiceCollection();
		_ = services.AddAwsSqsTransport("t", sqs => sqs.ConfigureCloudEvents(ce => ce.MaxBatchSize = 7));

		using var provider = services.BuildServiceProvider();

		provider.GetRequiredService<IOptions<AwsSqsCloudEventOptions>>().Value.MaxBatchSize.ShouldBe(7);
	}

	[Fact]
	public void CarryTheConfiguredValue_ForKafka()
	{
		var services = new ServiceCollection();
		_ = services.AddKafkaTransport("t", kafka => kafka.ConfigureCloudEvents(ce => ce.DefaultTopic = "sentinel"));

		using var provider = services.BuildServiceProvider();

		provider.GetRequiredService<IOptions<KafkaCloudEventOptions>>().Value.DefaultTopic.ShouldBe("sentinel");
	}

	[Fact]
	public void CarryTheConfiguredValue_ForRabbitMQ()
	{
		var services = new ServiceCollection();
		_ = services.AddRabbitMQTransport("t", rabbit => rabbit.ConfigureCloudEvents(ce => ce.DefaultQueue = "sentinel"));

		using var provider = services.BuildServiceProvider();

		provider.GetRequiredService<IOptions<RabbitMqCloudEventOptions>>().Value.DefaultQueue.ShouldBe("sentinel");
	}

	[Fact]
	public void CarryTheConfiguredValue_ForGooglePubSub()
	{
		var services = new ServiceCollection();
		_ = services.AddGooglePubSubTransport("t", pubsub => pubsub.ConfigureCloudEvents(ce => ce.Transport.CompressionThreshold = 7));

		using var provider = services.BuildServiceProvider();

		provider.GetRequiredService<IOptions<GooglePubSubCloudEventOptions>>().Value.Transport.CompressionThreshold.ShouldBe(7);
	}
}
