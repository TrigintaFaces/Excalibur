// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.Dispatch.LeaderElection;

namespace Excalibur.LeaderElection.Tests.Diagnostics;

/// <summary>Completion for simple fixtures whose fake has no background delivery or disposal callback.</summary>
internal static class SynchronousElectionFakeNotifications
{
    internal static void Configure(ILeaderElection election)
    {
        var tail = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => election.NotificationsCompleted).Returns(tail.Task);
        A.CallTo(() => election.DisposeAsync()).ReturnsLazily(() =>
        {
            // All explicit synchronous Raises in these fixtures have returned before disposal.
            tail.TrySetResult();
            return ValueTask.CompletedTask;
        });
    }
}
