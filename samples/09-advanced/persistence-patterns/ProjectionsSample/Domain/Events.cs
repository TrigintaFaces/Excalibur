// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace ProjectionsSample.Domain;

// ============================================================================
// Domain Events for Product Catalog
// ============================================================================
// These events represent state changes in our domain that projections will
// listen to and use to build read models.
//
// Each event carries the Category so that multi-stream projections can use
// KeyedBy<TEvent>(e => e.Category) to route events to the correct projection
// instance without out-of-band context.

/// <summary>
/// Event raised when a new product is created.
/// </summary>
public sealed record ProductCreated(
	Guid ProductId,
	string Name,
	string Category,
	decimal Price,
	int InitialStock) : DomainEvent;

/// <summary>
/// Event raised when a product's price changes.
/// </summary>
public sealed record ProductPriceChanged(
	Guid ProductId,
	string Category,
	decimal OldPrice,
	decimal NewPrice) : DomainEvent;

/// <summary>
/// Event raised when stock is added to a product.
/// </summary>
public sealed record ProductStockAdded(
	Guid ProductId,
	string Category,
	int Quantity,
	int NewStockLevel) : DomainEvent;

/// <summary>
/// Event raised when stock is removed from a product (e.g., sale).
/// </summary>
public sealed record ProductStockRemoved(
	Guid ProductId,
	string Category,
	int Quantity,
	int NewStockLevel,
	string Reason) : DomainEvent;

/// <summary>
/// Event raised when a product is discontinued.
/// </summary>
public sealed record ProductDiscontinued(Guid ProductId, string Category, string Reason) : DomainEvent;
