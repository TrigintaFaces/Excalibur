// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Cdc;

/// <summary>
/// The decision for one transient failure of a CDC consume loop.
/// </summary>
/// <param name="Delay">How long to wait before the next attempt.</param>
/// <param name="ConsecutiveFailures">The consecutive-failure count including this failure.</param>
/// <param name="Exhausted">
/// <see langword="true"/> when the configured limit has been reached and the loop must stop instead of retrying.
/// </param>
internal readonly record struct CdcTransientFailureOutcome(TimeSpan Delay, int ConsecutiveFailures, bool Exhausted);
