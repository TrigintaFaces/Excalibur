// ElasticSearch Order Projection Updater

using Elastic.Clients.Elasticsearch;

namespace MultiProviderQueueProcessor.Projections;

/// <summary>
/// ElasticSearch implementation of order projection updates.
/// </summary>
public sealed class ElasticOrderProjectionUpdater(
	ElasticsearchClient client,
	ILogger<ElasticOrderProjectionUpdater> logger) : IOrderProjectionUpdater
{
	private const string IndexName = "orders";

	/// <inheritdoc />
	public async Task CreateOrderProjectionAsync(
		string orderId,
		string customerId,
		decimal totalAmount,
		string currency,
		CancellationToken cancellationToken)
	{
		var projection = new OrderProjection
		{
			Id = orderId,
			CustomerId = customerId,
			TotalAmount = totalAmount,
			Currency = currency,
			Status = "Created",
			CreatedAt = DateTimeOffset.UtcNow,
		};

		var response = await client.IndexAsync(projection, idx => idx
			.Index(IndexName)
			.Id(orderId), cancellationToken);

		if (!response.IsValidResponse)
		{
			logger.LogError(
				"Failed to create order projection {OrderId}: {Error}",
				orderId,
				response.DebugInformation);
			throw new InvalidOperationException($"Failed to create order projection: {response.DebugInformation}");
		}

		logger.LogDebug("Created order projection {OrderId}", orderId);
	}

	/// <inheritdoc />
	public async Task AddOrderItemAsync(
		string orderId,
		string productId,
		string productName,
		int quantity,
		decimal unitPrice,
		CancellationToken cancellationToken)
	{
		var item = new OrderItemProjection
		{
			ProductId = productId,
			ProductName = productName,
			Quantity = quantity,
			UnitPrice = unitPrice,
		};

		var response = await client.UpdateAsync<OrderProjection, object>(
			IndexName,
			orderId,
			u => u.Script(s => s
				.Source(
					"ctx._source.items.add(params.item); ctx._source.totalAmount += params.itemTotal; ctx._source.lastModified = params.now")
				.Params(p => p
					.Add("item", item)
					.Add("itemTotal", quantity * unitPrice)
					.Add("now", DateTimeOffset.UtcNow))),
			cancellationToken);

		if (!response.IsValidResponse)
		{
			logger.LogError(
				"Failed to add item to order {OrderId}: {Error}",
				orderId,
				response.DebugInformation);
		}

		logger.LogDebug("Added item {ProductId} to order {OrderId}", productId, orderId);
	}

	/// <inheritdoc />
	public async Task UpdateOrderStatusAsync(
		string orderId,
		string status,
		CancellationToken cancellationToken)
	{
		var response = await client.UpdateAsync<OrderProjection, object>(
			IndexName,
			orderId,
			u => u.Doc(new { status, lastModified = DateTimeOffset.UtcNow }),
			cancellationToken);

		if (!response.IsValidResponse)
		{
			logger.LogError(
				"Failed to update order status {OrderId}: {Error}",
				orderId,
				response.DebugInformation);
		}

		logger.LogDebug("Updated order {OrderId} status to {Status}", orderId, status);
	}

	/// <inheritdoc />
	public async Task MarkOrderShippedAsync(
		string orderId,
		string trackingNumber,
		string carrier,
		CancellationToken cancellationToken)
	{
		var response = await client.UpdateAsync<OrderProjection, object>(
			IndexName,
			orderId,
			u => u.Doc(new
			{
				status = "Shipped",
				trackingNumber,
				carrier,
				shippedAt = DateTimeOffset.UtcNow,
				lastModified = DateTimeOffset.UtcNow,
			}),
			cancellationToken);

		if (!response.IsValidResponse)
		{
			logger.LogError(
				"Failed to mark order shipped {OrderId}: {Error}",
				orderId,
				response.DebugInformation);
		}

		logger.LogDebug("Marked order {OrderId} as shipped with tracking {TrackingNumber}", orderId, trackingNumber);
	}

	/// <inheritdoc />
	public async Task MarkOrderCancelledAsync(
		string orderId,
		string reason,
		CancellationToken cancellationToken)
	{
		var response = await client.UpdateAsync<OrderProjection, object>(
			IndexName,
			orderId,
			u => u.Doc(new
			{
				status = "Cancelled",
				cancellationReason = reason,
				cancelledAt = DateTimeOffset.UtcNow,
				lastModified = DateTimeOffset.UtcNow,
			}),
			cancellationToken);

		if (!response.IsValidResponse)
		{
			logger.LogError(
				"Failed to mark order cancelled {OrderId}: {Error}",
				orderId,
				response.DebugInformation);
		}

		logger.LogDebug("Marked order {OrderId} as cancelled: {Reason}", orderId, reason);
	}

	/// <inheritdoc />
	public async Task<OrderProjection?> GetOrderAsync(string orderId, CancellationToken cancellationToken)
	{
		var response = await client.GetAsync<OrderProjection>(
			IndexName,
			orderId,
			cancellationToken);

		if (!response.IsValidResponse || !response.Found)
		{
			return null;
		}

		return response.Source;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<OrderProjection>> SearchByCustomerAsync(
		string customerId,
		int skip,
		int take,
		CancellationToken cancellationToken)
	{
		var response = await client.SearchAsync<OrderProjection>(s => s
				.Index(IndexName)
				.Query(q => q.Term(t => t.Field(f => f.CustomerId).Value(customerId)))
				.From(skip)
				.Size(take)
				.Sort(so => so.Field(f => f.CreatedAt, new FieldSort { Order = SortOrder.Desc })),
			cancellationToken);

		if (!response.IsValidResponse)
		{
			logger.LogError(
				"Failed to search orders for customer {CustomerId}: {Error}",
				customerId,
				response.DebugInformation);
			return [];
		}

		return response.Documents.ToList();
	}
}
