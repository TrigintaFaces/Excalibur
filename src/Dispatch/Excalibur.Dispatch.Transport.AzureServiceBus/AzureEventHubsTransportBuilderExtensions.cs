// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Azure;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Transport.AzureServiceBus;

/// <summary>
/// Extension methods for <see cref="IAzureEventHubsTransportBuilder"/>.
/// </summary>
public static class AzureEventHubsTransportBuilderExtensions
{
	/// <summary>Sets the consumer group name.</summary>
	public static IAzureEventHubsTransportBuilder ConsumerGroup(this IAzureEventHubsTransportBuilder builder, string consumerGroup)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return ((AzureEventHubsTransportBuilder)builder).ConsumerGroup(consumerGroup);
	}

	/// <summary>Sets the prefetch count for receivers.</summary>
	public static IAzureEventHubsTransportBuilder PrefetchCount(this IAzureEventHubsTransportBuilder builder, int prefetchCount)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return ((AzureEventHubsTransportBuilder)builder).PrefetchCount(prefetchCount);
	}

	/// <summary>Sets the maximum batch size for batch operations.</summary>
	public static IAzureEventHubsTransportBuilder MaxBatchSize(this IAzureEventHubsTransportBuilder builder, int maxBatchSize)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return ((AzureEventHubsTransportBuilder)builder).MaxBatchSize(maxBatchSize);
	}

	/// <summary>Sets the starting position for event processing.</summary>
	public static IAzureEventHubsTransportBuilder StartingPosition(this IAzureEventHubsTransportBuilder builder, EventHubStartingPosition startingPosition)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return ((AzureEventHubsTransportBuilder)builder).StartingPosition(startingPosition);
	}
}
