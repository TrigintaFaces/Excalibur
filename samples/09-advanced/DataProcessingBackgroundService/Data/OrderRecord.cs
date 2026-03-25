// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace DataProcessingBackgroundService.Data;

/// <summary>
/// Represents an order record to be processed by the data processing pipeline.
/// </summary>
public sealed class OrderRecord
{
	public Guid OrderId { get; init; }

	public string CustomerName { get; init; } = string.Empty;

	public decimal Amount { get; init; }

	public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
