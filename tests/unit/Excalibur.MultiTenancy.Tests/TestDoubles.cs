// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.MultiTenancy.Tests;

/// <summary>
/// Shared, minimal test doubles for the multi-tenancy composition/decorator tests. These are the same shapes
/// a real provider registers: a tenant-unaware store plus an <see cref="ITenantScopingCapability{TContract}"/>
/// marker declaring the store honors the ambient tenant. Registering the marker is what a tenant-aware
/// provider's <c>Add*</c> extension does; omitting it models a tenant-unaware provider.
/// </summary>
internal static class TestDoubles
{
    /// <summary>A reference-type projection used to close <see cref="IProjectionStore{TProjection}"/>.</summary>
    internal sealed class TestProjection
    {
        public string? Value { get; init; }
    }

    /// <summary>A concrete <see cref="SagaState"/> for the saga-store decorator tests.</summary>
    internal sealed class TestSagaState : SagaState;

    /// <summary>
    /// An ambient tenant context for wiring the dep-gated tenant-scoping seams. The capability marker can no
    /// longer be faked (its member is internal), so a tenant-aware provider is modeled by wiring the store
    /// through the real seam, which requires a resolvable <see cref="ITenantContext"/>.
    /// </summary>
    internal sealed class TestTenantContext(string? tenantId) : ITenantContext
    {
        public string? TenantId { get; } = tenantId;

        public bool HasTenant => !string.IsNullOrEmpty(TenantId);
    }

    /// <summary>A minimal event store that ignores the ambient tenant — used as a registerable inner store.</summary>
    internal sealed class NoopEventStore : IEventStore
    {
        public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
            string aggregateId, string aggregateType, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<StoredEvent>>([]);

        public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
            string aggregateId, string aggregateType, long fromVersion, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<StoredEvent>>([]);

        public ValueTask<AppendResult> AppendAsync(
            string aggregateId, string aggregateType, IEnumerable<IDomainEvent> events,
            long expectedVersion, CancellationToken cancellationToken) =>
            ValueTask.FromResult(AppendResult.CreateSuccess(0, null));
    }

    /// <summary>A minimal saga store that ignores the ambient tenant — used as a registerable inner store.</summary>
    internal sealed class NoopSagaStore : ISagaStore
    {
        public Task<TSagaState?> LoadAsync<TSagaState>(Guid sagaId, CancellationToken cancellationToken)
            where TSagaState : SagaState => Task.FromResult<TSagaState?>(null);

        public Task SaveAsync<TSagaState>(TSagaState sagaState, CancellationToken cancellationToken)
            where TSagaState : SagaState => Task.CompletedTask;
    }

    /// <summary>A minimal inbox store that ignores the ambient tenant — used as a registerable inner store.</summary>
    internal sealed class NoopInboxStore : IInboxStore
    {
        public ValueTask<InboxEntry> CreateEntryAsync(
            string messageId, string handlerType, string messageType, byte[] payload,
            IDictionary<string, object> metadata, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new InboxEntry(messageId, handlerType, messageType, payload, metadata));

        public ValueTask MarkProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask<bool> TryMarkAsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken) =>
            ValueTask.FromResult(true);

        public ValueTask<bool> IsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken) =>
            ValueTask.FromResult(false);

        public ValueTask<InboxEntry?> GetEntryAsync(string messageId, string handlerType, CancellationToken cancellationToken) =>
            ValueTask.FromResult<InboxEntry?>(null);

        public ValueTask MarkFailedAsync(
            string messageId, string handlerType, string errorMessage, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
    }

    /// <summary>A minimal projection store that ignores the ambient tenant — used as a registerable inner store.</summary>
    internal sealed class NoopProjectionStore<TProjection> : IProjectionStore<TProjection>
        where TProjection : class
    {
        public Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult<TProjection?>(null);

        public Task UpsertAsync(string id, TProjection projection, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task DeleteAsync(string id, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<TProjection>> QueryAsync(
            IDictionary<string, object>? filters, QueryOptions? options, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TProjection>>([]);

        public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken) =>
            Task.FromResult(0L);
    }
}
