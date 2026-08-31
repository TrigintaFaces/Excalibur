// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// Extends <see cref="IOutboxStore"/> with per-transport delivery tracking for multi-transport scenarios.
/// </summary>
/// <remarks>
/// <para>
/// <strong>When to use:</strong> Implement this interface when messages must be delivered to multiple
/// transports independently (e.g., publishing the same order event to both RabbitMQ for internal consumers
/// and Kafka for analytics). If all messages go to a single transport, use <see cref="IOutboxStore"/> alone.
/// </para>
/// <para>
/// This interface provides core methods for tracking message delivery to multiple transports independently.
/// When a message needs to be published to multiple transports (e.g., RabbitMQ and Kafka), each transport
/// delivery can be tracked and retried independently.
/// </para>
/// <para>
/// Implementations should maintain both the aggregate message status and individual transport delivery records.
/// The aggregate status should be updated based on the combined state of all transport deliveries.
/// </para>
/// <para>
/// This interface contains 5 core methods following the ISP pattern.
/// For administrative and query operations (pending/failed retrieval, statistics, aggregate status),
/// implement <see cref="IMultiTransportOutboxStoreAdmin"/>.
/// This mirrors the <see cref="IOutboxStoreAdmin"/> ISP pattern established for <see cref="IOutboxStore"/>.
/// </para>
/// </remarks>
public interface IMultiTransportOutboxStore : IOutboxStore
{
	/// <summary>
	/// Stages a message with multiple transport delivery records.
	/// </summary>
	/// <param name="message">The outbound message to stage.</param>
	/// <param name="transports">The transport delivery records to create.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task representing the asynchronous stage operation.</returns>
	/// <exception cref="ArgumentNullException">Thrown when message or transports is null.</exception>
	Task StageMessageWithTransportsAsync(
		OutboundMessage message,
		IEnumerable<OutboundMessageTransport> transports,
		CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves the transport delivery records for a message, confined to the caller's own tenant.
	/// </summary>
	/// <remarks>
	/// <para>
	/// TENANT-CONFINED — the tenant is an explicit argument, never inferred from ambient state. A caller
	/// that supplies its own <paramref name="tenantId" /> together with another tenant's
	/// <paramref name="messageId" /> receives an empty result: the message exists, but not inside the
	/// partition the caller named. Supplying <see langword="null" /> confines the read to the untenanted
	/// partition (the partition a single-tenant host, or any host that never resolved a tenant for this
	/// operation, operates in).
	/// </para>
	/// <para>
	/// <paramref name="tenantId" /> is a <strong>confinement the caller opts into, not an authorization
	/// boundary the store enforces</strong>. This store reads no ambient tenant, so it has nothing to check
	/// <paramref name="tenantId" /> against: it trusts the value supplied and confines the query to it.
	/// A caller that passes a tenant id it is not entitled to receives that tenant's rows, exactly as if it
	/// had called <see cref="IMultiTransportOutboxStoreAdmin.GetAllTenantsTransportDeliveriesAsync" />
	/// scoped by hand. Establishing that a caller is actually entitled to the tenant it names is the
	/// caller's own responsibility, at whatever layer resolves the caller's identity.
	/// </para>
	/// <para>
	/// This is the consumer-facing read. The estate-wide read that spans every tenant — the one the
	/// delivery drain needs to make per-transport decisions on messages it claimed across tenants — is a
	/// separate, explicitly named operation on the store's administrative surface, not reachable through
	/// this method.
	/// </para>
	/// </remarks>
	/// <param name="messageId">The message ID.</param>
	/// <param name="tenantId">
	/// The tenant to confine this read to, or <see langword="null" /> to confine it to the untenanted
	/// partition. Trusted as supplied — see the authorization note above.
	/// </param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// Collection of transport delivery records for the message, or an empty collection when the message
	/// does not belong to <paramref name="tenantId" />'s partition.
	/// </returns>
	/// <exception cref="ArgumentException">Thrown when messageId is null or empty.</exception>
	Task<IEnumerable<OutboundMessageTransport>> GetTransportDeliveriesAsync(
		string messageId,
		string? tenantId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Marks a specific transport delivery as successfully sent.
	/// </summary>
	/// <param name="messageId">The message ID.</param>
	/// <param name="transportName">The transport name.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <exception cref="ArgumentException">Thrown when messageId or transportName is null or empty.</exception>
	/// <remarks>
	/// This method should update the individual transport delivery record and recalculate
	/// the aggregate message status based on all transport delivery states.
	/// </remarks>
	Task MarkTransportSentAsync(
		string messageId,
		string transportName,
		CancellationToken cancellationToken);

	/// <summary>
	/// Marks a specific transport delivery as failed.
	/// </summary>
	/// <param name="messageId">The message ID.</param>
	/// <param name="transportName">The transport name.</param>
	/// <param name="errorMessage">The error description.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <exception cref="ArgumentException">Thrown when messageId or transportName is null or empty.</exception>
	/// <exception cref="ArgumentNullException">Thrown when errorMessage is null.</exception>
	/// <remarks>
	/// This method should update the individual transport delivery record, increment its retry count,
	/// and recalculate the aggregate message status based on all transport delivery states.
	/// </remarks>
	Task MarkTransportFailedAsync(
		string messageId,
		string transportName,
		string errorMessage,
		CancellationToken cancellationToken);

	/// <summary>
	/// Marks a specific transport delivery as skipped.
	/// </summary>
	/// <param name="messageId">The message ID.</param>
	/// <param name="transportName">The transport name.</param>
	/// <param name="reason">Optional reason for skipping.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <exception cref="ArgumentException">Thrown when messageId or transportName is null or empty.</exception>
	/// <remarks>
	/// A transport delivery may be skipped due to routing rules, transport unavailability,
	/// or other configuration-based decisions. Skipped deliveries are considered complete
	/// for aggregate status calculation purposes.
	/// </remarks>
	Task MarkTransportSkippedAsync(
		string messageId,
		string transportName,
		string? reason,
		CancellationToken cancellationToken);
}

/// <summary>
/// Provides administrative and query operations for multi-transport outbox store management.
/// </summary>
/// <remarks>
/// <para>
/// These operations are used by background services, health checks, and administrative tooling
/// for managing multi-transport delivery tracking. They are NOT needed for normal message flow
/// (stage/send/fail/skip).
/// </para>
/// <para>
/// This follows the same ISP pattern as <see cref="IOutboxStoreAdmin"/> for the base
/// <see cref="IOutboxStore"/>. Implementations should access this sub-interface via
/// <c>GetService(typeof(IMultiTransportOutboxStoreAdmin))</c> or direct DI registration.
/// </para>
/// </remarks>
public interface IMultiTransportOutboxStoreAdmin
{
	/// <summary>
	/// Gets the transport delivery records for a message, spanning every tenant.
	/// </summary>
	/// <remarks>
	/// <para>
	/// DELIBERATELY ESTATE-WIDE — not tenant-confined, and named explicitly rather than left implicit. The
	/// delivery drain claims outbox rows across every tenant by design (one dispatcher serves every
	/// tenant, and each claimed row carries its own tenant so the handler re-establishes the owning
	/// partition before the message is handled); confining this read to an ambient tenant would return
	/// nothing for every tenanted message and stall multi-transport delivery entirely.
	/// </para>
	/// <para>
	/// Every row this method can return belongs to the one message named by <paramref name="messageId" />,
	/// and therefore to one tenant — this is not a set that mixes tenants — but the caller receives that
	/// tenant's rows regardless of which tenant, if any, the caller itself is scoped to. Reserved for the
	/// drain and other estate-wide operator paths; a tenant-facing consumer read is
	/// <see cref="IMultiTransportOutboxStore.GetTransportDeliveriesAsync" />.
	/// </para>
	/// </remarks>
	/// <param name="messageId">The message ID.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>Collection of transport delivery records for the message, across every tenant.</returns>
	/// <exception cref="ArgumentException">Thrown when messageId is null or empty.</exception>
	Task<IEnumerable<OutboundMessageTransport>> GetAllTenantsTransportDeliveriesAsync(
		string messageId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets pending transport deliveries for a specific transport.
	/// </summary>
	/// <param name="transportName">The transport name to query.</param>
	/// <param name="batchSize">Maximum number of deliveries to retrieve.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>Collection of pending transport deliveries with their parent messages.</returns>
	/// <exception cref="ArgumentException">Thrown when transportName is null or empty.</exception>
	/// <remarks>
	/// This method enables transport-specific workers to retrieve only the messages
	/// that need to be delivered to their specific transport, improving efficiency
	/// in multi-transport scenarios.
	/// </remarks>
	Task<IEnumerable<(OutboundMessage Message, OutboundMessageTransport Transport)>> GetPendingTransportDeliveriesAsync(
		string transportName,
		int batchSize,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets failed transport deliveries that are eligible for retry.
	/// </summary>
	/// <param name="transportName">The transport name to query.</param>
	/// <param name="maxRetries">Maximum number of retry attempts to consider.</param>
	/// <param name="olderThan">Only return deliveries that failed before this timestamp.</param>
	/// <param name="batchSize">Maximum number of deliveries to retrieve.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>Collection of failed transport deliveries eligible for retry.</returns>
	/// <exception cref="ArgumentException">Thrown when transportName is null or empty.</exception>
	Task<IEnumerable<(OutboundMessage Message, OutboundMessageTransport Transport)>> GetFailedTransportDeliveriesAsync(
		string transportName,
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken);

	/// <summary>
	/// Updates the aggregate message status based on transport delivery states.
	/// </summary>
	/// <param name="messageId">The message ID.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <exception cref="ArgumentException">Thrown when messageId is null or empty.</exception>
	/// <remarks>
	/// <para>
	/// The aggregate status is determined as follows:
	/// </para>
	/// <list type="bullet">
	/// <item><description><see cref="OutboxStatus.Sent"/> - All transports are Sent or Skipped</description></item>
	/// <item><description><see cref="OutboxStatus.Sending"/> - Any transport is currently Sending</description></item>
	/// <item><description><see cref="OutboxStatus.Failed"/> - All transports have Failed</description></item>
	/// <item><description><see cref="OutboxStatus.PartiallyFailed"/> - Some transports Sent/Skipped, some Failed</description></item>
	/// <item><description><see cref="OutboxStatus.Staged"/> - All transports are still Pending</description></item>
	/// </list>
	/// </remarks>
	Task UpdateAggregateStatusAsync(
		string messageId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets statistics about transport deliveries.
	/// </summary>
	/// <param name="transportName">Optional transport name to filter statistics.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>Transport-specific delivery statistics.</returns>
	Task<TransportDeliveryStatistics> GetTransportStatisticsAsync(
		string? transportName,
		CancellationToken cancellationToken);
}

/// <summary>
/// Statistics about transport delivery status.
/// </summary>
public sealed class TransportDeliveryStatistics
{
	/// <summary>
	/// Gets or sets the number of pending deliveries.
	/// </summary>
	public int PendingCount { get; set; }

	/// <summary>
	/// Gets or sets the number of successful deliveries.
	/// </summary>
	public int SentCount { get; set; }

	/// <summary>
	/// Gets or sets the number of failed deliveries.
	/// </summary>
	public int FailedCount { get; set; }

	/// <summary>
	/// Gets or sets the number of skipped deliveries.
	/// </summary>
	public int SkippedCount { get; set; }

	/// <summary>
	/// Gets or sets the age of the oldest pending delivery.
	/// </summary>
	public TimeSpan? OldestPendingAge { get; set; }

	/// <summary>
	/// Gets or sets the transport name these statistics are for, or null for all transports.
	/// </summary>
	public string? TransportName { get; set; }
}
