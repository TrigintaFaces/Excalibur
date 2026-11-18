// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Runtime.ExceptionServices;

namespace Excalibur.LeaderElection.Kubernetes;

// Preserve single original failures and distinct multi-operation shutdown failures.
internal static class KubernetesElectionFailures
{
    // Preserve both the SDK outcome and its independently joined cleanup failure.
    // Do not flatten or deduplicate here: each position represents a distinct operation.
    internal static Exception Combine(Exception? operationFailure, Exception cleanupFailure) =>
        operationFailure is null ? cleanupFailure : new AggregateException(operationFailure, cleanupFailure);

    internal static void Throw(IEnumerable<Exception> failures, string message)
    {
        var unique = failures.Distinct<Exception>(ReferenceEqualityComparer.Instance).ToArray();
        if (unique.Length == 1) { ExceptionDispatchInfo.Capture(unique[0]).Throw(); }
        if (unique.Length > 1) { throw new AggregateException(message, unique); }
    }
}
