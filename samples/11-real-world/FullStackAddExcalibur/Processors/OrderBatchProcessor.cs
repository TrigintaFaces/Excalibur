// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Data.DataProcessing;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FullStackAddExcalibur.Processors;

/// <summary>
/// Sample order record flowing through the DataProcessing pipeline.
/// </summary>
public sealed class OrderBatchRecord
{
	/// <summary>Gets or sets the order identifier.</summary>
	public Guid OrderId { get; init; }

	/// <summary>Gets or sets the customer name.</summary>
	public string CustomerName { get; init; } = string.Empty;

	/// <summary>Gets or sets the amount.</summary>
	public decimal Amount { get; init; }
}

/// <summary>
/// Record handler invoked by the DataProcessing pipeline per batch record.
/// </summary>
public sealed class OrderBatchHandler : IRecordHandler<OrderBatchRecord>
{
	private readonly ILogger<OrderBatchHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="OrderBatchHandler"/> class.
	/// </summary>
	public OrderBatchHandler(ILogger<OrderBatchHandler> logger)
	{
		_logger = logger;
	}

	/// <inheritdoc />
	public Task ProcessAsync(OrderBatchRecord record, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(record);

		_logger.LogInformation(
			"Processed batch-record order {OrderId} ({CustomerName}, {Amount:C})",
			record.OrderId,
			record.CustomerName,
			record.Amount);

		return Task.CompletedTask;
	}
}

/// <summary>
/// Data processor that pages order rows in batches and hands them off to record handlers.
/// </summary>
[DataTaskRecordType("OrderBatchRecord")]
public sealed class OrderBatchProcessor : DataProcessor<OrderBatchRecord>
{
	private static readonly List<OrderBatchRecord> SampleOrders =
	[
		new() { OrderId = Guid.NewGuid(), CustomerName = "Alice", Amount = 99.99m },
		new() { OrderId = Guid.NewGuid(), CustomerName = "Bob", Amount = 149.50m },
		new() { OrderId = Guid.NewGuid(), CustomerName = "Charlie", Amount = 250.00m },
	];

	/// <summary>
	/// Initializes a new instance of the <see cref="OrderBatchProcessor"/> class.
	/// </summary>
	public OrderBatchProcessor(
		IHostApplicationLifetime appLifetime,
		IOptions<DataProcessingOptions> configuration,
		IServiceProvider serviceProvider,
		ILogger<OrderBatchProcessor> logger)
		: base(appLifetime, configuration, serviceProvider, logger)
	{
	}

	/// <inheritdoc />
	public override Task<IEnumerable<OrderBatchRecord>> FetchBatchAsync(
		long skip,
		int batchSize,
		CancellationToken cancellationToken)
	{
		var batch = SampleOrders.Skip((int)skip).Take(batchSize);
		return Task.FromResult(batch);
	}
}
