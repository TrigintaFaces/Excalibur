// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
//
// Licensed under multiple licenses:
// - Excalibur License 1.0 (see LICENSE-EXCALIBUR.txt)
// - GNU Affero General Public License v3.0 or later (AGPL-3.0) (see LICENSE-AGPL-3.0.txt)
// - Server Side Public License v1.0 (SSPL-1.0) (see LICENSE-SSPL-1.0.txt)
// - Apache License 2.0 (see LICENSE-APACHE-2.0.txt)
//
// You may not use this file except in compliance with the License terms above. You may obtain copies of the licenses in the project root or online.
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

using Excalibur.Dispatch.Patterns.CQRS.CQRS.Queries;

namespace examples.Excalibur.Patterns.CQRS.Examples;

/// <summary>
///     Example: Get order by ID query with caching.
/// </summary>
public sealed class GetOrderByIdQuery : QueryBase<OrderReadModel?>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="GetOrderByIdQuery" /> class.
	/// </summary>
	public GetOrderByIdQuery(Guid orderId, string? correlationId = null, string? userId = null)
		: base(correlationId, userId) =>
		OrderId = orderId;

	public Guid OrderId { get; }

	/// <summary>
	///     Cache key for this specific order.
	/// </summary>
	public override string CacheKey => $"order:{OrderId}";

	/// <summary>
	///     Cache for 5 minutes.
	/// </summary>
	public override TimeSpan? CacheExpiry => TimeSpan.FromMinutes(5);
}
