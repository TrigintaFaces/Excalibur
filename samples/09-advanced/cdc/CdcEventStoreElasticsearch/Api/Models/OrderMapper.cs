// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using CdcEventStoreElasticsearch.Projections;

using Excalibur.EventSourcing.Abstractions;

namespace CdcEventStoreElasticsearch.Api.Models;

/// <summary>
/// Maps between internal projection models and API DTOs.
/// </summary>
/// <remarks>
/// Lightweight manual mapping — no AutoMapper dependency needed for a sample.
/// In production, consider source generators like Mapperly for zero-allocation mapping.
/// </remarks>
internal static class OrderMapper
{
	/// <summary>
	/// Maps an <see cref="OrderSearchProjection"/> to an <see cref="OrderDto"/>.
	/// </summary>
	public static OrderDto ToDto(OrderSearchProjection projection) => new()
	{
		OrderId = projection.OrderId,
		ExternalOrderId = projection.ExternalOrderId,
		CustomerId = projection.CustomerId,
		CustomerExternalId = projection.CustomerExternalId,
		CustomerName = projection.CustomerName,
		Status = projection.Status,
		TotalAmount = projection.TotalAmount,
		ItemCount = projection.ItemCount,
		OrderDate = projection.OrderDate,
		ShippedDate = projection.ShippedDate,
		DeliveredDate = projection.DeliveredDate,
		CreatedAt = projection.CreatedAt,
		LastUpdatedAt = projection.LastUpdatedAt,
		LineItems = projection.LineItems.Select(ToDto).ToList(),
		Tags = projection.Tags
	};

	/// <summary>
	/// Maps an <see cref="OrderLineItemProjection"/> to an <see cref="OrderLineItemDto"/>.
	/// </summary>
	public static OrderLineItemDto ToDto(OrderLineItemProjection projection) => new()
	{
		ItemId = projection.ItemId,
		ExternalItemId = projection.ExternalItemId,
		ProductName = projection.ProductName,
		Quantity = projection.Quantity,
		UnitPrice = projection.UnitPrice,
		LineTotal = projection.LineTotal
	};

	/// <summary>
	/// Maps a <see cref="PagedResult{T}"/> of projections to a <see cref="PagedResult{T}"/> of DTOs.
	/// </summary>
	public static PagedResult<OrderDto> ToDto(PagedResult<OrderSearchProjection> paged) =>
		new(paged.Items.Select(ToDto), paged.PageNumber, paged.PageSize, paged.TotalItems);

	/// <summary>
	/// Maps a <see cref="CursorPagedResult{T}"/> of projections to a <see cref="CursorPagedResult{T}"/> of DTOs.
	/// </summary>
	public static CursorPagedResult<OrderDto> ToDto(CursorPagedResult<OrderSearchProjection> paged) =>
		new(paged.Items.Select(ToDto), paged.PageSize, paged.TotalRecords, paged.NextCursor);

	/// <summary>
	/// Maps a list of projections to a list of DTOs.
	/// </summary>
	public static IReadOnlyList<OrderDto> ToDto(IReadOnlyList<OrderSearchProjection> projections) =>
		projections.Select(ToDto).ToList();
}
