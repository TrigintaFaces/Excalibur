// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.SQS;

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Transport;

/// <summary>
/// The SQS client and message bus were registered with TryAddSingleton BY TYPE, which de-duplicates: a
/// second named SQS transport contributed no registration and both names resolved the first transport's
/// client/bus, silently sending every named transport to the first transport's queue.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class AwsSqsNamedClientsShould
{
	[Fact]
	public async Task ResolveASeparateClientPerTransportName()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddAwsSqsTransport("orders", sqs => sqs.UseRegion("us-east-1"));
		_ = services.AddAwsSqsTransport("payments", sqs => sqs.UseRegion("us-west-2"));

		await using var provider = services.BuildServiceProvider();

		var orders = (AmazonSQSClient)provider.GetRequiredKeyedService<IAmazonSQS>("orders");
		var payments = (AmazonSQSClient)provider.GetRequiredKeyedService<IAmazonSQS>("payments");

		// Pre-fix, TryAddSingleton-by-type meant this second lookup returned the SAME instance as the
		// first, silently pointed at "us-east-1".
		orders.ShouldNotBeSameAs(payments);
		orders.Config.RegionEndpoint.SystemName.ShouldBe("us-east-1");
		payments.Config.RegionEndpoint.SystemName.ShouldBe("us-west-2");
	}

	[Fact]
	public async Task ResolveASeparateMessageBusPerTransportName()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddPluggableSerialization();

		_ = services.AddAwsSqsTransport("orders", sqs => sqs.UseRegion("us-east-1"));
		_ = services.AddAwsSqsTransport("payments", sqs => sqs.UseRegion("us-west-2"));

		await using var provider = services.BuildServiceProvider();

		var orders = provider.GetRequiredKeyedService<AwsSqsMessageBus>("orders");
		var payments = provider.GetRequiredKeyedService<AwsSqsMessageBus>("payments");

		orders.ShouldNotBeSameAs(payments);
	}
}
