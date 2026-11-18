// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
namespace Excalibur.LeaderElection.Tests.InMemory;
/// <summary>Waits for the exact queued public transition after earlier subscribers have returned.</summary>
internal sealed class InMemoryCallbackBarrier : IDisposable
{
    private readonly InMemoryLeaderElection _election;
    private readonly bool _relinquished;
    private readonly TaskCompletionSource _delivered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal InMemoryCallbackBarrier(InMemoryLeaderElection election, bool relinquished)
    {
        _election = election; _relinquished = relinquished;
        election.LeaderChanged += OnChanged;
    }
    internal Task Completion => _delivered.Task;
    private void OnChanged(object? sender, LeaderChangedEventArgs args)
    {
        if (_relinquished ? args.PreviousLeaderId == _election.CandidateId && args.NewLeaderId is null
            : args.NewLeaderId == _election.CandidateId)
        {
            _delivered.TrySetResult();
        }
    }
    public void Dispose() => _election.LeaderChanged -= OnChanged;
}
