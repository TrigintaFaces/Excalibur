// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CdcAntiCorruption.Commands;
using CdcAntiCorruption.Models;

using Excalibur.Data.DataProcessing;
using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Logging;

namespace CdcAntiCorruption.Backfill;

/// <summary>
/// Converts historical snapshot records into domain sync commands.
/// </summary>
public sealed class CustomerHistoryRecordHandler : IRecordHandler<LegacyCustomerSnapshot>
{
	private readonly IDispatcher _dispatcher;
	private readonly ILogger<CustomerHistoryRecordHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="CustomerHistoryRecordHandler"/> class.
	/// </summary>
	public CustomerHistoryRecordHandler(
		IDispatcher dispatcher,
		ILogger<CustomerHistoryRecordHandler> logger)
	{
		_dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public async Task ProcessAsync(LegacyCustomerSnapshot record, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(record);

		var command = new SyncCustomerCommand(new AdaptedCustomerData
		{
			ExternalId = record.ExternalId,
			Name = record.Name,
			Email = record.Email,
			Phone = record.Phone,
			IsActive = true,
			ChangedAt = record.ChangedAtUtc,
		});

		_ = await _dispatcher.DispatchAsync(command, cancellationToken).ConfigureAwait(false);
		_logger.LogInformation("   → Backfill SyncCustomerCommand: Replayed historical customer {ExternalId}", record.ExternalId);
	}
}
