// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Logging;

namespace Excalibur.Outbox.Postgres;

public sealed partial class PostgresOutboxStore
{
    private void ReportClaimFenceRefusal(long presentedToken, long highWater)
    {
        // The statement and buffered-reader cleanup succeeded before observers see this decision.
        try
        {
            _metrics.RecordClaimFenceRefusal();
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
