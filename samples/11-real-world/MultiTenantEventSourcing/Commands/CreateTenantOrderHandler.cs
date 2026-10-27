// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Application.Requests.Commands;
using Excalibur.Data.Sharding;
using Excalibur.Dispatch;
using Excalibur.EventSourcing;

using Microsoft.Extensions.Logging;

using MultiTenantEventSourcing.Domain;

namespace MultiTenantEventSourcing.Commands;

/// <summary>
/// Handles <see cref="CreateTenantOrderCommand"/> by creating a new
/// <see cref="TenantScopedOrder"/> and persisting it through
/// <see cref="IEventSourcedRepository{TAggregate, TKey}"/>.
/// </summary>
/// <remarks>
/// This is the scenario that exercises the tenant-routing decorator:
/// <see cref="IEventSourcedRepository{TAggregate, TKey}.SaveAsync(TAggregate, CancellationToken)"/>
/// resolves the decorated <c>IEventStore</c>, which reads
/// <see cref="ITenantContext"/> and dispatches the append to the correct shard.
/// The handler logs the tenant → shard selection so the effect is observable
/// in sample output.
/// </remarks>
public sealed class CreateTenantOrderHandler : ICommandHandler<CreateTenantOrderCommand, Guid>
{
	private readonly IEventSourcedRepository<TenantScopedOrder, Guid> _repository;
	private readonly ITenantContext _tenantContext;
	private readonly ITenantShardMap _shardMap;
	private readonly ILogger<CreateTenantOrderHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="CreateTenantOrderHandler"/> class.
	/// </summary>
	public CreateTenantOrderHandler(
		IEventSourcedRepository<TenantScopedOrder, Guid> repository,
		ITenantContext tenantContext,
		ITenantShardMap shardMap,
		ILogger<CreateTenantOrderHandler> logger)
	{
		_repository = repository;
		_tenantContext = tenantContext;
		_shardMap = shardMap;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<Guid> HandleAsync(CreateTenantOrderCommand action, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);

		var tenantId = _tenantContext.TenantId;
		if (string.IsNullOrWhiteSpace(tenantId))
		{
			throw new InvalidOperationException(
				"No tenant in scope. Provide an X-Tenant-Id request header (e.g., tenant-acme).");
		}

		// Log the shard the routing decorator will pick (pure introspection; the
		// decorator performs the same lookup internally per operation).
		var shard = _shardMap.GetShardInfo(tenantId);

		var orderId = Guid.NewGuid();
		var order = TenantScopedOrder.Create(orderId, action.Total);

		_logger.LogInformation(
			"Saving TenantScopedOrder {OrderId} for tenant {TenantId} (total {Total:C}) via shard {ShardId} ({Region})",
			orderId,
			tenantId,
			action.Total,
			shard.ShardId,
			shard.Region);

		await _repository.SaveAsync(order, cancellationToken).ConfigureAwait(false);

		return orderId;
	}
}
