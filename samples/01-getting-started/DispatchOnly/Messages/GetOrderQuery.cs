// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace DispatchMinimal.Messages;

/// <summary>
/// A query to retrieve order details.
/// Queries (documents) request data without changing state.
/// The document handler reads the order and records the observed OrderDto in this sample's store.
/// </summary>
public record GetOrderQuery(Guid OrderId) : IDispatchDocument;

/// <summary>
/// DTO representing order details returned from GetOrderQuery.
/// </summary>
public record OrderDto(Guid Id, string ProductId, int Quantity, string Status);
