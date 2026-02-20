// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Configuration options for RabbitMQ consumer priority.
/// </summary>
/// <remarks>
/// <para>
/// Consumer priority allows higher-priority consumers to receive messages before lower-priority ones
/// when multiple consumers are subscribed to the same queue. This is useful for scenarios where
/// certain consumers (e.g., on faster hardware) should handle messages preferentially.
/// </para>
/// <para>
/// The priority is set via the <c>x-priority</c> argument on the consumer when calling
/// <c>BasicConsumeAsync</c>. Consumers with higher priority values receive messages first.
/// When all higher-priority consumers are busy, messages are delivered to lower-priority consumers.
/// </para>
/// <para>
/// This feature requires RabbitMQ 3.2.0 or later.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddRabbitMQConsumerPriority(options =>
/// {
///     options.Priority = 10;
/// });
/// </code>
/// </example>
public sealed class ConsumerPriorityOptions
{
	/// <summary>
	/// Gets or sets the consumer priority value.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Higher values indicate higher priority. Consumers with higher priority values
	/// receive messages before consumers with lower priority values when multiple
	/// consumers are subscribed to the same queue.
	/// </para>
	/// <para>
	/// The default value is 0 (normal priority). Valid range is -2,147,483,648 to 2,147,483,647.
	/// </para>
	/// </remarks>
	/// <value>The consumer priority. Default is 0.</value>
	[Range(int.MinValue, int.MaxValue)]
	public int Priority { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether consumer priority is enabled.
	/// </summary>
	/// <remarks>
	/// When disabled, the <c>x-priority</c> argument is not added to the consumer declaration,
	/// and standard round-robin delivery is used.
	/// </remarks>
	/// <value><see langword="true"/> to enable consumer priority; otherwise, <see langword="false"/>. Default is <see langword="false"/>.</value>
	public bool Enabled { get; set; }
}
