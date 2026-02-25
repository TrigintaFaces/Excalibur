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

namespace examples.Excalibur.Patterns.MaterializedViews.Examples;

/// <summary>
///     Repository interface for order data access.
/// </summary>
public interface IOrderRepository
{
	/// <summary>
	///     Gets orders within a date range.
	/// </summary>
	Task<List<Order>> GetOrdersAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

	/// <summary>
	///     Gets all pending orders.
	/// </summary>
	Task<List<Order>> GetPendingOrdersAsync(CancellationToken cancellationToken = default);

	/// <summary>
	///     Gets all processing orders.
	/// </summary>
	Task<List<Order>> GetProcessingOrdersAsync(CancellationToken cancellationToken = default);

	/// <summary>
	///     Gets orders updated since a given date.
	/// </summary>
	Task<List<Order>> GetUpdatedOrdersAsync(DateTime since, CancellationToken cancellationToken = default);
}
