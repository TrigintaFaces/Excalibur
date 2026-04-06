// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using DataProcessingBackgroundService.Data;

using Excalibur.Data.DataProcessing;

using Microsoft.Extensions.Options;

namespace DataProcessingBackgroundService.Processing;

/// <summary>
/// Data processor for order records. Fetches order batches from the database
/// and feeds them into the producer/consumer pipeline.
/// </summary>
[DataTaskRecordType("OrderRecord")]
public sealed class OrderDataProcessor : DataProcessor<OrderRecord>
{
	// In a real application, this would fetch from a database.
	// For this sample, we use an in-memory list to demonstrate the pipeline.
	private static readonly List<OrderRecord> SampleOrders =
	[
		new() { OrderId = Guid.NewGuid(), CustomerName = "Alice", Amount = 99.99m },
		new() { OrderId = Guid.NewGuid(), CustomerName = "Bob", Amount = 149.50m },
		new() { OrderId = Guid.NewGuid(), CustomerName = "Charlie", Amount = 250.00m },
		new() { OrderId = Guid.NewGuid(), CustomerName = "Diana", Amount = 75.25m },
		new() { OrderId = Guid.NewGuid(), CustomerName = "Eve", Amount = 320.00m },
	];

	private readonly ILogger<OrderDataProcessor> _logger;

	public OrderDataProcessor(
		IHostApplicationLifetime appLifetime,
		IOptions<DataProcessingOptions> configuration,
		IServiceProvider serviceProvider,
		ILogger<OrderDataProcessor> logger)
		: base(appLifetime, configuration, serviceProvider, logger)
	{
		_logger = logger;
	}

	public override Task<IEnumerable<OrderRecord>> FetchBatchAsync(
		long skip,
		int batchSize,
		CancellationToken cancellationToken)
	{
		// Simulate fetching a batch from the database with skip/take
		var batch = SampleOrders
			.Skip((int)skip)
			.Take(batchSize);

		_logger.LogDebug("Fetched batch: skip={Skip}, batchSize={BatchSize}", skip, batchSize);

		return Task.FromResult(batch);
	}
}
