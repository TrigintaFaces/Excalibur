// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.DataProcessing;

/// <summary>Processes records sequentially while deferring inline projection waits to one final catch-up attempt.</summary>
/// <typeparam name="TRecord">The record type.</typeparam>
/// <remarks>
/// Use only when handlers do not read projections that depend on writes made during the run.
/// Requires an Excalibur.EventSourcing.IProjectionProcessor registration. Durable record checkpoints are
/// unchanged; failure or cancellation during final catch-up can replay the last page on restart, so handlers
/// must remain idempotent. Non-cancelled, non-caught-up outcomes are logged as warnings and do not undo
/// committed records. Successful completion alone does not guarantee projection freshness.
/// </remarks>
public abstract class BulkDataProcessor<TRecord> : DataProcessor<TRecord>
{
    /// <summary>Initializes a new instance of the <see cref="BulkDataProcessor{TRecord}"/> class.</summary>
    /// <param name="appLifetime">Application shutdown notifications.</param>
    /// <param name="configuration">Pipeline configuration.</param>
    /// <param name="serviceProvider">Provider of record handlers and the projection processor.</param>
    /// <param name="logger">Diagnostic logger.</param>
    protected BulkDataProcessor(IHostApplicationLifetime appLifetime, IOptions<DataProcessingOptions> configuration,
        IServiceProvider serviceProvider, ILogger logger)
        : base(appLifetime, configuration, serviceProvider, logger)
    {
    }

    /// <inheritdoc />
    protected sealed override bool DeferInlineProjectionWait => true;
}
