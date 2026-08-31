// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs;

/// <summary>
/// The AWS SQS transport is registered under a name while its runtime options were registered without
/// one, so two named SQS transports in one container wrote the same options instance and the second
/// silently replaced the first.
/// </summary>
/// <remarks>
/// The consequence here is not only a misread setting. Server-side encryption on SQS is a queue
/// attribute applied at start-up, so a shared options instance meant one transport applied the other
/// transport's KMS key -- or its own key to the other transport's queue. That applier was additionally
/// registered by implementation type through <c>TryAddEnumerable</c>, which de-duplicates, so a second
/// named transport got no applier at all.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Transport")]
public sealed class AwsSqsNamedOptionsShould
{
	private const string OrdersQueue = "https://sqs.us-east-1.amazonaws.com/123456789012/orders";
	private const string AuditQueue = "https://sqs.us-west-2.amazonaws.com/123456789012/audit";

	private static AwsSqsOptions Resolve(IServiceProvider provider, string name)
		=> provider.GetRequiredService<IOptionsMonitor<AwsSqsOptions>>().Get(name);

	[Fact]
	public void KeepTwoNamedTransportsIndependent()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddAwsSqsTransport("orders", sqs => sqs
			.UseRegion("us-east-1")
			.MapQueue<OrdersMessage>(OrdersQueue));

		_ = services.AddAwsSqsTransport("audit", sqs => sqs
			.UseRegion("us-west-2")
			.MapQueue<AuditMessage>(AuditQueue));

		using var provider = services.BuildServiceProvider();

		// Pre-fix both names read the SECOND registration's queue and region.
		Resolve(provider, "orders").QueueUrl.ShouldBe(new Uri(OrdersQueue));
		Resolve(provider, "orders").Region.ShouldBe("us-east-1");

		Resolve(provider, "audit").QueueUrl.ShouldBe(new Uri(AuditQueue));
		Resolve(provider, "audit").Region.ShouldBe("us-west-2");
	}

	[Fact]
	public void GiveEachNamedTransportItsOwnQueueEncryptionApplier()
	{
		// The de-duplication arm. The encryption applier used to be registered by implementation type
		// through TryAddEnumerable, so the second named transport contributed no applier and its queue was
		// never encrypted. Counting the registered hosted services is what detects that: an assertion that
		// merely finds one applier passes on the broken registration too.
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddAwsSqsTransport("orders", sqs => sqs.UseRegion("us-east-1").MapQueue<OrdersMessage>(OrdersQueue));
		_ = services.AddAwsSqsTransport("audit", sqs => sqs.UseRegion("us-west-2").MapQueue<AuditMessage>(AuditQueue));

		using var provider = services.BuildServiceProvider();

		provider.GetServices<IHostedService>()
			.Count(s => s is AwsSqsQueueEncryptionService)
			.ShouldBe(2);
	}

	[Fact]
	public void KeepTheFifoSelectorsOfTwoNamedTransportsIndependent()
	{
		// AwsSqsFifoOptions is the second, easily-forgotten registration on this path: a fix that names
		// only AwsSqsOptions leaves the FIFO selectors overwriting across transports, so one transport
		// groups its messages by the other transport's selector.
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddAwsSqsTransport("orders", sqs => sqs
			.UseRegion("us-east-1")
			.MapQueue<OrdersMessage>(OrdersQueue)
			.ConfigureFifo(fifo => fifo.ContentBasedDeduplication(true)));

		_ = services.AddAwsSqsTransport("audit", sqs => sqs
			.UseRegion("us-west-2")
			.MapQueue<AuditMessage>(AuditQueue)
			.ConfigureFifo(fifo => fifo.ContentBasedDeduplication(false)));

		using var provider = services.BuildServiceProvider();
		var monitor = provider.GetRequiredService<IOptionsMonitor<AwsSqsFifoOptions>>();

		monitor.Get("orders").ContentBasedDeduplication.ShouldBeTrue();
		monitor.Get("audit").ContentBasedDeduplication.ShouldBeFalse();
	}

	[Fact]
	public void StillConfigureTheUnnamedOptionsForASingleTransportHost()
	{
		// Liveness, and the arm a careless fix breaks. AwsSqsMessageBus takes IOptions<AwsSqsOptions> and
		// IOptions<AwsSqsFifoOptions> in its constructor, which resolve the UNNAMED instances. Moving the
		// registration to the named overload alone would hand the message bus empty objects -- a silent
		// failure worse than the overwrite being fixed, and one no assertion about named options detects.
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddAwsSqsTransport(sqs => sqs
			.UseRegion("eu-west-1")
			.MapQueue<OrdersMessage>(OrdersQueue));

		using var provider = services.BuildServiceProvider();

		var unnamed = provider.GetRequiredService<IOptions<AwsSqsOptions>>().Value;
		unnamed.QueueUrl.ShouldBe(new Uri(OrdersQueue));
		unnamed.Region.ShouldBe("eu-west-1");

		provider.GetRequiredService<IOptions<AwsSqsFifoOptions>>().Value.ShouldNotBeNull();

		// And the default name resolves the same configuration, so a host that reaches the options either
		// way sees one answer.
		Resolve(provider, "aws-sqs").QueueUrl.ShouldBe(new Uri(OrdersQueue));
	}

	private sealed class OrdersMessage;

	private sealed class AuditMessage;
}
