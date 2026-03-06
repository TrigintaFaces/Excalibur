// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CdcAntiCorruption.Backfill;

/// <summary>
/// Data processor that replays historical customer snapshots to close CDC history gaps.
/// </summary>
[DataTaskRecordType("LegacyCustomerSnapshot")]
public sealed class CustomerHistoryBackfillProcessor : DataProcessor<LegacyCustomerSnapshot>
{
	private readonly ILegacyCustomerSnapshotSource _snapshotSource;

	/// <summary>
	/// Initializes a new instance of the <see cref="CustomerHistoryBackfillProcessor"/> class.
	/// </summary>
	public CustomerHistoryBackfillProcessor(
		IHostApplicationLifetime appLifetime,
		IOptions<DataProcessingConfiguration> configuration,
		IServiceProvider serviceProvider,
		ILogger<CustomerHistoryBackfillProcessor> logger,
		ILegacyCustomerSnapshotSource snapshotSource)
		: base(appLifetime, configuration, serviceProvider, logger)
	{
		_snapshotSource = snapshotSource ?? throw new ArgumentNullException(nameof(snapshotSource));
	}

	/// <inheritdoc />
	public override Task<IEnumerable<LegacyCustomerSnapshot>> FetchBatchAsync(long skip, int batchSize, CancellationToken cancellationToken)
	{
		return _snapshotSource.FetchBatchAsync(skip, batchSize, cancellationToken);
	}
}
