// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.AzureServiceBus;

/// <summary>
/// Configuration options for Azure Service Bus dead letter queue management.
/// </summary>
/// <remarks>
/// <para>
/// Azure Service Bus provides a native <c>$DeadLetterQueue</c> subqueue per entity (queue or subscription).
/// This manager uses <c>ServiceBusReceiverOptions.SubQueue = SubQueue.DeadLetter</c> to access it.
/// </para>
/// </remarks>
public sealed class ServiceBusDeadLetterOptions
{
	/// <summary>
	/// Gets or sets the entity path (queue or subscription) whose dead letter subqueue is managed.
	/// </summary>
	/// <value>The entity path. Required.</value>
	public string EntityPath { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the maximum number of messages to receive in a single batch during purge or retrieve operations.
	/// </summary>
	/// <value>The batch size. Defaults to 100.</value>
	public int MaxBatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the maximum wait time when peeking or receiving messages.
	/// </summary>
	/// <value>The receive wait time. Defaults to 5 seconds.</value>
	public TimeSpan ReceiveWaitTime { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets the maximum number of messages to peek for statistics.
	/// </summary>
	/// <value>The statistics peek count. Defaults to 1000.</value>
	public int StatisticsPeekCount { get; set; } = 1000;

	/// <summary>
	/// Gets or sets a value indicating whether to include exception stack traces in dead letter reason.
	/// </summary>
	/// <value><see langword="true"/> to include stack traces; otherwise, <see langword="false"/>. Defaults to <see langword="true"/>.</value>
	public bool IncludeStackTrace { get; set; } = true;
}
