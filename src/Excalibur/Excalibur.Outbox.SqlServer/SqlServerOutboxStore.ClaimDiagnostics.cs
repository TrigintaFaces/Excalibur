// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace Excalibur.Outbox.SqlServer;

public sealed partial class SqlServerOutboxStore
{
    private static readonly Meter ClaimFenceMeter = new("Excalibur.Outbox.SqlServer");
    private static readonly Counter<long> ClaimFenceRefusals = ClaimFenceMeter.CreateCounter<long>(
        "excalibur.outbox.fence_refusals", "{refusals}", "Confirmed stale-token outbox claim refusals.");

    private void ReportClaimFenceRefusal(long presentedToken, long highWater)
    {
        // Observers are independent and must not turn a nonthrowing refusal into a store failure.
        try
        {
            ClaimFenceRefusals.Add(1,
                new KeyValuePair<string, object?>("backend", "sqlserver"),
                new KeyValuePair<string, object?>("reason", "stale_claim"));
        }
        catch (Exception)
        {
            // A failing metrics listener cannot change the already-decided claim outcome.
        }
        try
        {
            LogClaimFenceRefused(presentedToken, highWater);
        }
        catch (Exception)
        {
            // A failing logger cannot change the already-decided claim outcome.
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, EventName = "OutboxClaimFenceRefused",
        Message = "Outbox claim refused: presented fencing token {PresentedToken} is below high-water {HighWater}.")]
    private partial void LogClaimFenceRefused(long presentedToken, long highWater);
}
