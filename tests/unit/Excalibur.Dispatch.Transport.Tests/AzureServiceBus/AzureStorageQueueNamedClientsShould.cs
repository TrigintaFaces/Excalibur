// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Storage.Queues;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus;

/// <summary>
/// The Storage Queue client was registered with TryAddSingleton BY TYPE, which de-duplicates: a second
/// named Storage Queue transport contributed no registration and both names resolved the first
/// transport's client, silently sending every named transport to the first transport's queue.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Transport")]
public sealed class AzureStorageQueueNamedClientsShould
{
	[Fact]
	public void ResolveASeparateQueueClientPerTransportName()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddAzureStorageQueueTransport("orders", sq => sq
			.ConnectionString("DefaultEndpointsProtocol=https;AccountName=acct;AccountKey=a2V5;EndpointSuffix=core.windows.net")
			.QueueName("orders-queue"));

		_ = services.AddAzureStorageQueueTransport("notifications", sq => sq
			.ConnectionString("DefaultEndpointsProtocol=https;AccountName=acct;AccountKey=a2V5;EndpointSuffix=core.windows.net")
			.QueueName("notifications-queue"));

		using var provider = services.BuildServiceProvider();

		var orders = provider.GetRequiredKeyedService<QueueClient>("orders");
		var notifications = provider.GetRequiredKeyedService<QueueClient>("notifications");

		// Pre-fix, TryAddSingleton-by-type meant this second lookup returned the SAME instance as the
		// first, silently pointed at "orders-queue".
		orders.ShouldNotBeSameAs(notifications);
		orders.Name.ShouldBe("orders-queue");
		notifications.Name.ShouldBe("notifications-queue");
	}
}
