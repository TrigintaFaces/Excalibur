// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ;

/// <summary>
/// Locks that a RabbitMQ transport's runtime options belong to the transport that configured them.
/// </summary>
/// <remarks>
/// Same defect as the Kafka and Azure Service Bus transports carried: the transport is named, its runtime
/// options were not, so a second named registration overwrote the first's queue, prefetch and connection
/// settings without an error. The MQTT, Pulsar and IBM MQ transports in this repository already used the
/// named overload.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Transport")]
public sealed class RabbitMqNamedOptionsShould
{
	private static RabbitMqOptions Resolve(IServiceProvider provider, string name)
		=> provider.GetRequiredService<IOptionsMonitor<RabbitMqOptions>>().Get(name);

	[Fact]
	public void StillConfigureTheUnnamedOptionsForASingleTransportHost()
	{
		// The liveness arm. Types in this package resolve IOptions<RabbitMqOptions> directly, so naming
		// the options without also configuring the unnamed instance would hand them an empty object.
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddRabbitMQTransport(rmq => rmq
			.ConnectionString("amqp://app:s3cret@localhost:5672/")
			.ConfigureQueue(queue => queue.Name("single-queue")));

		using var provider = services.BuildServiceProvider();

		var unnamed = provider.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
		unnamed.Queue.QueueName.ShouldBe("single-queue");
		unnamed.Connection.ConnectionString.ShouldBe("amqp://app:s3cret@localhost:5672/");

		Resolve(provider, "rabbitmq").Queue.QueueName.ShouldBe("single-queue");
	}
}
