// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.A3.Audit.Internal;

/// <summary>
/// Sentinel <see cref="IOutboxDispatcher"/> registered by
/// <see cref="Microsoft.Extensions.DependencyInjection.AuditServiceCollectionExtensions.AddExcaliburAudit(Microsoft.Extensions.DependencyInjection.IServiceCollection)"/>
/// via <c>TryAdd</c> so the Audit composition is wireable without a concrete
/// Outbox persistence stack (S792 bd-drizep / ADR-322 §Decision-3 Shape-1).
/// </summary>
/// <remarks>
/// <para>
/// The real <see cref="IOutboxDispatcher"/> is <c>MessageOutbox</c>, provided
/// by <c>AddExcaliburOutbox</c> together with an <c>IOutboxStore</c> backend
/// (SQL Server / PostgreSQL / ElasticSearch). Because <c>AddExcaliburAudit</c>
/// targets <see cref="MinimalWiringBucket.TryAddDefault"/>-style Bucket-A
/// siblings, the framework-internal dispatcher must be default-registered
/// rather than required as a consumer prerequisite.
/// </para>
/// <para>
/// This sentinel is a deliberate <b>fail-fast</b> no-op: attempting to save or
/// dispatch through it throws
/// <see cref="InvalidOperationException"/> with a curated message instructing
/// the consumer to register a real outbox. It is never reached in practice
/// when <c>AddExcaliburOutbox</c> is present because <c>TryAddSingleton</c>
/// is a no-op against an existing registration — the real
/// <see cref="IOutboxDispatcher"/> wins.
/// </para>
/// </remarks>
internal sealed class DefaultOutboxDispatcher : IOutboxDispatcher
{
	private const string RegisterOutboxMessage =
		"AddExcaliburAudit registered a default IOutboxDispatcher. " +
		"Register a concrete outbox via services.AddExcaliburOutbox(...) " +
		"(or a peer AddOutbox builder extension) before dispatching audited commands.";

	public Task<int> RunOutboxDispatchAsync(string dispatcherId, CancellationToken cancellationToken)
		=> throw new InvalidOperationException(RegisterOutboxMessage);

	public Task SaveEventsAsync(
		IReadOnlyCollection<IIntegrationEvent> integrationEvents,
		IMessageMetadata metadata,
		CancellationToken cancellationToken)
		=> throw new InvalidOperationException(RegisterOutboxMessage);

	public Task<int> SaveMessagesAsync(
		ICollection<IOutboxMessage> outboxMessages,
		CancellationToken cancellationToken)
		=> throw new InvalidOperationException(RegisterOutboxMessage);

	public Task<IEnumerable<IDispatchMessage>> GetPendingMessagesAsync(CancellationToken cancellationToken)
		=> Task.FromResult(Enumerable.Empty<IDispatchMessage>());

	public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
