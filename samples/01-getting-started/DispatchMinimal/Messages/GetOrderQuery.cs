// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace DispatchMinimal.Messages;

/// <summary>
/// A query to retrieve order details.
/// Queries (documents) request data without changing state.
/// Returns an OrderDto with the order details.
/// </summary>
public record GetOrderQuery(Guid OrderId) : IDispatchDocument;

/// <summary>
/// DTO representing order details returned from GetOrderQuery.
/// </summary>
public record OrderDto(Guid Id, string ProductId, int Quantity, string Status);
