// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing;

namespace Excalibur.Examples.TransactionalInline;

/// <summary>Consumer application code: business operations use the framework's registered repository.</summary>
public sealed class OrderApplicationService(IEventSourcedRepository<Order> repository, IProjectionStore<OrderSummary> models)
{
    public async Task<OrderWriteResult> CreateAsync(string id, decimal amount, RequestMetadata request, CancellationToken token)
    {
        // No check-then-start race: a fresh aggregate's expected version rejects an existing stream.
        var order = new Order();
        order.Add(id, amount);
        EnrichPendingEvents(order, request);
        var saved = await repository.SaveAsync(order, token);
        return Receipt(order, saved);
    }

    public async Task<OrderWriteResult> AddAmountAsync(string id, decimal amount, string expectedETag,
        RequestMetadata request, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedETag);
        var order = await repository.GetByIdAsync(id, token)
            ?? throw new KeyNotFoundException($"Order '{id}' was not found.");
        order.Add(id, amount);
        EnrichPendingEvents(order, request);
        // The client's opaque ETag protects against an outdated request; the store also checks
        // the loaded stream version atomically against writers racing this save.
        var saved = await repository.SaveAsync(order, expectedETag, token);
        return Receipt(order, saved);
    }

    public async Task<OrderDetails?> ReadAsync(string id, CancellationToken token)
    {
        var order = await repository.GetByIdAsync(id, token);
        return order is null ? null : new OrderDetails(order.Id, order.Total, order.Version, order.ETag!);
    }

    public Task<bool> ExistsAsync(string id, CancellationToken token) => repository.ExistsAsync(id, token);

    public Task<OrderSummary?> ReadModelAsync(string id, CancellationToken token) => models.GetByIdAsync(id, token);

    private static OrderWriteResult Receipt(Order order, SaveResult saved) =>
        new(order.Id, order.ETag!, saved.CommittedVersion, saved.SessionToken);

    private static void EnrichPendingEvents(Order order, RequestMetadata request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ActorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CorrelationId);
        foreach (var domainEvent in order.GetUncommittedEvents())
        {
            // These are application audit identifiers; no bearer/access token is persisted.
            domainEvent.Metadata!["actor-id"] = request.ActorId;
            domainEvent.Metadata["correlation-id"] = request.CorrelationId;
        }
    }
}

public sealed record RequestMetadata(string ActorId, string CorrelationId);
public sealed record OrderWriteResult(string Id, string ETag, long CommittedVersion, long? SessionToken);
public sealed record OrderDetails(string Id, decimal Total, long Version, string ETag);
