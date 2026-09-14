// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.SQS;
using Amazon.SQS.Model;

using Microsoft.Extensions.Hosting;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Provisioning;

/// <summary>
/// Two named SQS transports with provisioning enabled must each provision through their OWN SQS client.
/// The provisioner was registered unkeyed, so a second named transport added a second unkeyed descriptor
/// and every hosted service resolved the last one -- both then applied their queue attributes through one
/// transport's client, against an account and region the other transport never configured.
/// </summary>
/// <remarks>
/// The assertion is on the EMITTED AWS call, not on the registration: each named transport's fake client
/// must receive a <c>SetQueueAttributes</c> for its own queue URL and for no other. A registration-shape
/// assertion would pass against a provisioner that never called AWS at all.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class NamedSqsTransportsProvisionTheirOwnQueueShould
{
	private const string OrdersQueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/orders";
	private const string PaymentsQueueUrl = "https://sqs.eu-west-1.amazonaws.com/210987654321/payments";

	[Fact]
	public async Task EachNamedTransportProvisionsThroughItsOwnSqsClient()
	{
		var ordersSqs = A.Fake<IAmazonSQS>();
		var paymentsSqs = A.Fake<IAmazonSQS>();

		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Seated before AddAwsSqsTransport so the transport's own TryAddKeyedSingleton defers to these.
		_ = services.AddKeyedSingleton(ServiceKeys.Orders, ordersSqs);
		_ = services.AddKeyedSingleton(ServiceKeys.Payments, paymentsSqs);

		_ = services.AddAwsSqsTransport(ServiceKeys.Orders, sqs => sqs
			.UseRegion("us-east-1")
			.MapQueue<OrderPlaced>(OrdersQueueUrl)
			.ConfigureQueue(queue => queue.VisibilityTimeout(TimeSpan.FromSeconds(30)))
			.ConfigureProvisioning(p => p.Enabled = true));

		_ = services.AddAwsSqsTransport(ServiceKeys.Payments, sqs => sqs
			.UseRegion("eu-west-1")
			.MapQueue<PaymentReceived>(PaymentsQueueUrl)
			.ConfigureQueue(queue => queue.VisibilityTimeout(TimeSpan.FromSeconds(60)))
			.ConfigureProvisioning(p => p.Enabled = true));

		await using var provider = services.BuildServiceProvider();

		// Liveness: both transports' provisioning hosted services must actually be present and run. A
		// keyed IHostedService would resolve to nothing here, and every safety assertion below would
		// then hold vacuously.
		// Matched by type name because the hosted service is internal to the transport package; the
		// transport registers other hosted services too, and only the provisioning ones are started here.
		var hostedServices = provider.GetServices<IHostedService>()
			.Where(h => h.GetType().Name == "AwsSqsProvisioningHostedService")
			.ToList();
		hostedServices.Count.ShouldBe(2, "each named transport contributes its own provisioning hosted service");

		foreach (var hostedService in hostedServices)
		{
			await hostedService.StartAsync(CancellationToken.None);
		}

		// Liveness: provisioning ran at all.
		A.CallTo(() => ordersSqs.SetQueueAttributesAsync(
				A<SetQueueAttributesRequest>.That.Matches(r => r.QueueUrl == OrdersQueueUrl),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();

		A.CallTo(() => paymentsSqs.SetQueueAttributesAsync(
				A<SetQueueAttributesRequest>.That.Matches(r => r.QueueUrl == PaymentsQueueUrl),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();

		// Safety: neither client provisioned the other's queue. This is the arm the shared unkeyed
		// provisioner breaks -- one client receives BOTH queue URLs and the other receives none.
		A.CallTo(() => ordersSqs.SetQueueAttributesAsync(
				A<SetQueueAttributesRequest>.That.Matches(r => r.QueueUrl == PaymentsQueueUrl),
				A<CancellationToken>._))
			.MustNotHaveHappened();

		A.CallTo(() => paymentsSqs.SetQueueAttributesAsync(
				A<SetQueueAttributesRequest>.That.Matches(r => r.QueueUrl == OrdersQueueUrl),
				A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	private static class ServiceKeys
	{
		public const string Orders = "orders";
		public const string Payments = "payments";
	}

	private sealed record OrderPlaced;

	private sealed record PaymentReceived;
}
