// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Configuration options for RabbitMQ quorum queues.
/// </summary>
/// <remarks>
/// <para>
/// Quorum queues are a modern, replicated queue type in RabbitMQ that provide
/// higher data safety guarantees than classic mirrored queues. They use the Raft
/// consensus protocol for replication and are the recommended choice for
/// data safety-critical workloads.
/// </para>
/// <para>
/// Key characteristics:
/// <list type="bullet">
///   <item><description>Replicated across multiple nodes using Raft consensus.</description></item>
///   <item><description>Support delivery limits with automatic dead-lettering.</description></item>
///   <item><description>Provide at-least-once delivery semantics.</description></item>
///   <item><description>Support poison message handling via delivery count tracking.</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddRabbitMqQuorumQueues(options =>
/// {
///     options.DeliveryLimit = 5;
///     options.DeadLetterStrategy = DeadLetterStrategy.AtLeastOnce;
///     options.QuorumSize = 3;
/// });
/// </code>
/// </example>
public sealed class QuorumQueueOptions
{
	/// <summary>
	/// Gets or sets the maximum number of delivery attempts before a message
	/// is dead-lettered or dropped.
	/// </summary>
	/// <remarks>
	/// When a message has been redelivered more than this number of times,
	/// it will be dead-lettered (if a dead letter exchange is configured)
	/// or dropped. Set to <c>null</c> for unlimited redelivery.
	/// </remarks>
	/// <value>The delivery limit. Default is <c>null</c> (unlimited).</value>
	[Range(1, int.MaxValue)]
	public int? DeliveryLimit { get; set; }

	/// <summary>
	/// Gets or sets the dead letter strategy for the quorum queue.
	/// </summary>
	/// <value>The dead letter strategy. Default is <see cref="Quorum.DeadLetterStrategy.AtMostOnce"/>.</value>
	public DeadLetterStrategy DeadLetterStrategy { get; set; } = DeadLetterStrategy.AtMostOnce;

	/// <summary>
	/// Gets or sets the initial quorum group size (number of replicas).
	/// </summary>
	/// <remarks>
	/// Determines how many RabbitMQ nodes will host replicas of this queue.
	/// Must be an odd number for proper Raft consensus. The default is determined
	/// by the cluster size if not specified.
	/// </remarks>
	/// <value>The quorum size. Default is <c>null</c> (cluster default).</value>
	[Range(1, 99)]
	public int? QuorumSize { get; set; }
}
