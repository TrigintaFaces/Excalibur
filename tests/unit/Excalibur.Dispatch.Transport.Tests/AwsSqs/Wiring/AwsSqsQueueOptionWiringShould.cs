// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.SQS;
using Amazon.SQS.Model;

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Wiring;

/// <summary>
/// Locks the queue options a consumer sets through <c>ConfigureQueue</c> onto the calls that carry
/// them to SQS: the long-poll wait and visibility timeout onto every receive, and the delivery delay
/// and retention onto the queue attributes applied at provisioning.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class AwsSqsQueueOptionWiringShould
{
	private const string QueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/orders";

	[Fact]
	public async Task CarryTheConfiguredWaitAndVisibilityOntoEveryReceiveCall()
	{
		ReceiveMessageRequest? captured = null;
		var sqs = A.Fake<IAmazonSQS>();
		_ = A.CallTo(() => sqs.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
			.Invokes((ReceiveMessageRequest r, CancellationToken _) => captured = r)
			.Returns(Task.FromResult(new ReceiveMessageResponse { Messages = [] }));

		var services = new ServiceCollection();

		// Registered first so the transport's TryAdd leaves the fake in place.
		services.TryAddKeyedSingleton<IAmazonSQS>("orders", (_, _) => sqs);
		_ = services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));

		_ = services.AddAwsSqsTransport("orders", transport => transport
			.UseRegion("us-east-1")
			.MapQueue<object>(QueueUrl)
			.ConfigureQueue(q => q
				.ReceiveWaitTimeSeconds(7)
				.VisibilityTimeout(TimeSpan.FromSeconds(120))));

		await using var provider = services.BuildServiceProvider();
		var receiver = provider.GetRequiredKeyedService<ITransportReceiver>("orders");

		_ = await receiver.ReceiveAsync(5, CancellationToken.None).ConfigureAwait(true);

		captured.ShouldNotBeNull();

		// The two arms are independent: a wiring that carried only one of them still reds the other.
		captured!.WaitTimeSeconds.ShouldBe(7);
		captured.VisibilityTimeout.ShouldBe(120);
	}

	[Fact]
	public async Task ApplyTheConfiguredDelayAndRetentionToTheQueueAtProvisioning()
	{
		SetQueueAttributesRequest? captured = null;
		var sqs = A.Fake<IAmazonSQS>();
		_ = A.CallTo(() => sqs.SetQueueAttributesAsync(A<SetQueueAttributesRequest>._, A<CancellationToken>._))
			.Invokes((SetQueueAttributesRequest r, CancellationToken _) =>
			{
				if (r.Attributes.ContainsKey("DelaySeconds"))
				{
					captured = r;
				}
			})
			.Returns(Task.FromResult(new SetQueueAttributesResponse()));

		var options = new AwsSqsTransportAdapterOptions { Name = "orders" };
		options.QueueMappings[typeof(object)] = QueueUrl;
		options.Provisioning.Enabled = true;
		options.QueueOptions = new AwsSqsQueueOptions
		{
			DelaySeconds = 45,
			MessageRetentionPeriod = TimeSpan.FromDays(2),
		};

		var provisioner = new AwsSqsProvisioner(sqs, snsClient: null, NullLogger<AwsSqsProvisioner>.Instance);
		await provisioner.ProvisionAsync(options, CancellationToken.None).ConfigureAwait(true);

		captured.ShouldNotBeNull();
		captured!.QueueUrl.ShouldBe(QueueUrl);
		captured.Attributes["DelaySeconds"].ShouldBe("45");
		captured.Attributes["MessageRetentionPeriod"].ShouldBe("172800");
	}
}
