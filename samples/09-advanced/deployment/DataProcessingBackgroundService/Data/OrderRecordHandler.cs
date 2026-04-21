// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing;

namespace DataProcessingBackgroundService.Data;

/// <summary>
/// Handles individual order records as they are processed by the pipeline.
/// </summary>
public sealed class OrderRecordHandler : IRecordHandler<OrderRecord>
{
	private readonly ILogger<OrderRecordHandler> _logger;

	public OrderRecordHandler(ILogger<OrderRecordHandler> logger)
	{
		_logger = logger;
	}

	public Task ProcessAsync(OrderRecord record, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(record);

		_logger.LogInformation(
			"Processed order {OrderId} for {CustomerName}, amount: {Amount:C}",
			record.OrderId,
			record.CustomerName,
			record.Amount);

		return Task.CompletedTask;
	}
}
