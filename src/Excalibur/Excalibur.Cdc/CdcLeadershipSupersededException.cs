// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc;

/// <summary>
/// Thrown when a CDC checkpoint write is rejected because the writing instance presented a fencing token that
/// has been superseded by a newer leadership tenure — i.e. this instance believes it is the leader but was
/// demoted (a split-brain). The checkpoint's non-decreasing compare-and-swap left the stored LSN unchanged,
/// so no position was regressed or skipped; this exception signals the superseded instance to stop advancing.
/// </summary>
/// <remarks>
/// <para>
/// This is a <em>terminal</em> (non-retryable) condition: retrying the write with the same stale token would
/// be rejected identically. The CDC fatal classifier treats it as fatal so the processing pipeline stops
/// rather than spinning, ceding the change feed to the current leader.
/// </para>
/// <para>
/// This is a <strong>benign</strong> outcome, not a failure to alarm on: another instance has taken
/// leadership and this instance should simply stand down. A fatal-error handler should distinguish it from a
/// genuine fault — stop this processor quietly rather than crash or page. Re-acquiring leadership and
/// restarting the processor after a subsequent election is the host's concern, not this exception's.
/// </para>
/// </remarks>
public sealed class CdcLeadershipSupersededException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CdcLeadershipSupersededException"/> class.
    /// </summary>
    public CdcLeadershipSupersededException()
        : base("The CDC checkpoint write was rejected because this instance's leadership fencing token was superseded by a newer leader. This instance has been demoted and must stop advancing the change feed.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CdcLeadershipSupersededException"/> class with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public CdcLeadershipSupersededException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CdcLeadershipSupersededException"/> class with a message
    /// and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public CdcLeadershipSupersededException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
