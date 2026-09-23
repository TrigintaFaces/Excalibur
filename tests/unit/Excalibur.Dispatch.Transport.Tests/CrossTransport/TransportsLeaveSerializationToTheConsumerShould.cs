// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Hosting;

namespace Excalibur.Dispatch.Transport.Tests.CrossTransport;

/// <summary>
/// Serialization is the consuming application's concern. No transport package may register an
/// <see cref="IPayloadSerializer"/>; a transport states that it needs one and stops the host at start-up
/// when it is missing.
/// </summary>
/// <remarks>
/// <para>
/// The same question used to be answered two ways: AWS SQS and Azure registered nothing, while Kafka and
/// RabbitMQ each called <c>AddPluggableSerialization()</c>. Because that call is <c>TryAdd</c>-based, a
/// host composing RabbitMQ alongside AWS SQS got a serializer only because RabbitMQ happened to be in the
/// container -- removing the RabbitMQ transport would have broken the AWS path, in a different package, on
/// an unrelated edit. Worse, two transports defaulting to different serializers would have made the format
/// on the wire depend on registration order in consumer code the framework does not control.
/// </para>
/// <para>
/// One parameterised arm, not four copies: a fifth transport inherits the contract by being added to the
/// list rather than by someone remembering to write another test.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class TransportsLeaveSerializationToTheConsumerShould
{
	public static TheoryData<string> Transports => new("kafka", "rabbitmq", "aws-sqs", "azure-service-bus");

	[Theory]
	[MemberData(nameof(Transports))]
	public void NotRegisterAPayloadSerializer(string transport)
	{
		// Safety. A bare ServiceCollection, so the only descriptors present are the transport's own.
		var services = new ServiceCollection();
		_ = services.AddLogging();

		AddTransport(services, transport);

		services.Any(d => d.ServiceType == typeof(IPayloadSerializer)).ShouldBeFalse(
			$"the {transport} transport must not register IPayloadSerializer; serialization is a consumer "
			+ "concern, and a transport that seats a process-wide singleton makes the wire format depend on "
			+ "which sibling transport happens to be registered");
	}

	[Theory]
	[MemberData(nameof(Transports))]
	public async Task FailAtStartupNamingTheRemedy_WhenNoSerializerIsRegistered(string transport)
	{
		// Liveness for the safety arm above: not registering a serializer must produce a diagnosable host,
		// not a silent one. Without this arm the suite is satisfied by four transports that register nothing
		// and fail later with a container activation error naming a type the consumer never wrote.
		var services = new ServiceCollection();
		_ = services.AddLogging();

		AddTransport(services, transport);

		await using var provider = services.BuildServiceProvider();

		var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
		{
			foreach (var hostedService in provider.GetServices<IHostedService>())
			{
				await hostedService.StartAsync(CancellationToken.None);
			}
		});

		exception.Message.Contains("AddPluggableSerialization", StringComparison.Ordinal).ShouldBeTrue(
			$"a bare {transport} host must stop at start-up naming the call that fixes it, but the message "
			+ $"was: {exception.Message}");
	}

	[Theory]
	[MemberData(nameof(Transports))]
	public async Task StartCleanly_WhenTheConsumerRegistersASerializer(string transport)
	{
		// Liveness for the start-up check: the guard must be silent on a correctly composed host. A check
		// that rejected everything would satisfy the arm above and break every consumer.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddPluggableSerialization();

		AddTransport(services, transport);

		await using var provider = services.BuildServiceProvider();

		var validators = provider.GetServices<IStartupPrerequisiteValidator>()
			.Where(v => v.GetType().Name == "PayloadSerializerPrerequisiteValidator")
			.ToList();

		validators.Count.ShouldBe(1,
			$"the {transport} transport must contribute the serializer prerequisite check exactly once");

		Should.NotThrow(() => validators[0].Validate());
	}

	private static void AddTransport(IServiceCollection services, string transport)
	{
		switch (transport)
		{
			case "kafka":
				_ = services.AddKafkaTransport(k => k.BootstrapServers("localhost:9092"));
				break;
			case "rabbitmq":
				_ = services.AddRabbitMQTransport(r => r.ConnectionString("amqp://guest:guest@localhost:5672/"));
				break;
			case "aws-sqs":
				_ = services.AddAwsSqsTransport(sqs => sqs.UseRegion("us-east-1"));
				break;
			case "azure-service-bus":
				_ = services.AddAzureServiceBusTransport(
					sb => sb.FullyQualifiedNamespace("excalibur-lock.servicebus.windows.net"));
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(transport), transport, "unknown transport");
		}
	}
}
