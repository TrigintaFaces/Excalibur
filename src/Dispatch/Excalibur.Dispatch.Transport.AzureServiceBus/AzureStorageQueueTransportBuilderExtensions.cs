// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Transport.AzureServiceBus;

/// <summary>
/// Extension methods for <see cref="IAzureStorageQueueTransportBuilder"/>.
/// </summary>
public static class AzureStorageQueueTransportBuilderExtensions
{
	/// <summary>Sets the visibility timeout for messages.</summary>
	public static IAzureStorageQueueTransportBuilder VisibilityTimeout(this IAzureStorageQueueTransportBuilder builder, TimeSpan visibilityTimeout)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return ((AzureStorageQueueTransportBuilder)builder).VisibilityTimeout(visibilityTimeout);
	}

	/// <summary>Sets the maximum number of concurrent messages to process.</summary>
	public static IAzureStorageQueueTransportBuilder MaxConcurrentMessages(this IAzureStorageQueueTransportBuilder builder, int maxConcurrentMessages)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return ((AzureStorageQueueTransportBuilder)builder).MaxConcurrentMessages(maxConcurrentMessages);
	}

	/// <summary>Sets the polling interval for checking new messages.</summary>
	public static IAzureStorageQueueTransportBuilder PollingInterval(this IAzureStorageQueueTransportBuilder builder, TimeSpan pollingInterval)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return ((AzureStorageQueueTransportBuilder)builder).PollingInterval(pollingInterval);
	}

	/// <summary>Enables a dead letter queue.</summary>
	public static IAzureStorageQueueTransportBuilder EnableDeadLetterQueue(this IAzureStorageQueueTransportBuilder builder, string deadLetterQueueName, int maxDequeueCount = 5)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return ((AzureStorageQueueTransportBuilder)builder).EnableDeadLetterQueue(deadLetterQueueName, maxDequeueCount);
	}
}
