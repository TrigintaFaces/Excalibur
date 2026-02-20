// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Admin client for managing Kafka topics and cluster configuration.
/// </summary>
/// <remarks>
/// <para>
/// Provides administrative operations for Kafka topic management including
/// creation, deletion, listing, describing, and configuration updates.
/// </para>
/// <para>
/// This interface follows the Microsoft pattern of keeping the surface area minimal
/// (5 methods). It mirrors the Confluent <c>IAdminClient</c> API surface while
/// using async/await patterns and <see cref="CancellationToken"/> throughout.
/// </para>
/// </remarks>
public interface IKafkaAdminClient : IAsyncDisposable
{
	/// <summary>
	/// Creates a new Kafka topic with the specified configuration.
	/// </summary>
	/// <param name="specification">The topic specification including name, partitions, and replication factor.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A <see cref="Task"/> representing the asynchronous create operation.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="specification"/> is null.
	/// </exception>
	Task CreateTopicAsync(TopicSpecification specification, CancellationToken cancellationToken);

	/// <summary>
	/// Deletes the specified Kafka topic.
	/// </summary>
	/// <param name="topicName">The name of the topic to delete.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A <see cref="Task"/> representing the asynchronous delete operation.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="topicName"/> is null.
	/// </exception>
	Task DeleteTopicAsync(string topicName, CancellationToken cancellationToken);

	/// <summary>
	/// Lists all topic names in the Kafka cluster.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// A <see cref="Task{TResult}"/> representing the asynchronous operation,
	/// containing a read-only list of topic names.
	/// </returns>
	Task<IReadOnlyList<string>> ListTopicsAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Describes a Kafka topic including its partitions, replication, and configuration.
	/// </summary>
	/// <param name="topicName">The name of the topic to describe.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// A <see cref="Task{TResult}"/> representing the asynchronous operation,
	/// containing the <see cref="TopicDescription"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="topicName"/> is null.
	/// </exception>
	Task<TopicDescription> DescribeTopicAsync(string topicName, CancellationToken cancellationToken);

	/// <summary>
	/// Alters configuration entries for the specified topic.
	/// </summary>
	/// <param name="topicName">The name of the topic to alter.</param>
	/// <param name="config">The configuration entries to set or update.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A <see cref="Task"/> representing the asynchronous alter operation.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="topicName"/> or <paramref name="config"/> is null.
	/// </exception>
	Task AlterTopicConfigAsync(
		string topicName,
		IReadOnlyDictionary<string, string> config,
		CancellationToken cancellationToken);
}
