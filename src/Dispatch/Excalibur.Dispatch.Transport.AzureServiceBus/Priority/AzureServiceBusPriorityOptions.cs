// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Configuration options for Azure Service Bus priority queue routing.
/// </summary>
/// <remarks>
/// <para>
/// Azure Service Bus does not natively support message priority. This implementation
/// emulates priority queues by routing messages to priority-specific queues or
/// subscriptions based on a priority level. Higher-priority queues are processed
/// first by consumers.
/// </para>
/// <para>
/// For example, with <see cref="PriorityLevels"/> set to 3 and <see cref="QueueNameTemplate"/>
/// set to <c>orders-priority-{0}</c>, messages are routed to:
/// <list type="bullet">
///   <item><c>orders-priority-0</c> (highest priority)</item>
///   <item><c>orders-priority-1</c></item>
///   <item><c>orders-priority-2</c> (lowest priority)</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddAzureServiceBusPriorityQueues(options =>
/// {
///     options.PriorityLevels = 3;
///     options.QueueNameTemplate = "orders-priority-{0}";
///     options.DefaultPriority = 1;
/// });
/// </code>
/// </example>
public sealed class AzureServiceBusPriorityOptions
{
	/// <summary>
	/// Gets or sets the number of priority levels.
	/// </summary>
	/// <remarks>
	/// Determines how many separate queues or subscriptions are created.
	/// Priority 0 is the highest, and <c>PriorityLevels - 1</c> is the lowest.
	/// </remarks>
	/// <value>The number of priority levels. Default is 3.</value>
	[Range(2, 10)]
	public int PriorityLevels { get; set; } = 3;

	/// <summary>
	/// Gets or sets the queue name template with a <c>{0}</c> placeholder for the priority level.
	/// </summary>
	/// <remarks>
	/// The <c>{0}</c> placeholder is replaced with the priority level (0 to PriorityLevels-1).
	/// </remarks>
	/// <value>The queue name template. Default is <c>dispatch-priority-{0}</c>.</value>
	[Required]
	public string QueueNameTemplate { get; set; } = "dispatch-priority-{0}";

	/// <summary>
	/// Gets or sets the default priority level for messages that do not specify a priority.
	/// </summary>
	/// <remarks>
	/// Must be between 0 (highest) and <see cref="PriorityLevels"/> - 1 (lowest).
	/// </remarks>
	/// <value>The default priority. Default is 1 (medium).</value>
	[Range(0, 9)]
	public int DefaultPriority { get; set; } = 1;
}
