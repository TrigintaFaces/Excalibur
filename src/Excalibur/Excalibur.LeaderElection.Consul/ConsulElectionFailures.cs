// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.LeaderElection.Consul;

/// <summary>Preserves distinct cleanup failures without losing their original stack traces.</summary>
internal static class ConsulElectionFailures
{
    // A definitive server session-expiry response precedes optional classifier policy.
    internal static bool IsSessionExpired(Exception error) => error is global::Consul.SessionExpiredException;

    internal static bool IsPermanent(Excalibur.Dispatch.IMessageFailureClassifier? classifier, Exception error) =>
        classifier?.Classify(error) == Excalibur.Dispatch.MessageFailureKind.Permanent;

    internal static void Throw(List<Exception> failures)
    {
        var distinct = failures.Distinct<Exception>(ReferenceEqualityComparer.Instance).ToArray();
        if (distinct.Length == 1) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(distinct[0]).Throw(); }
        if (distinct.Length > 1) { throw new AggregateException("Consul ownership cleanup failed.", distinct); }
    }
}
