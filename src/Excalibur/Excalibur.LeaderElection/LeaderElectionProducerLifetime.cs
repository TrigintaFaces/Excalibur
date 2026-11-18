// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.LeaderElection;

/// <summary>Seals producer admission and joins admitted asynchronous work without joining notification callbacks.</summary>
internal sealed class LeaderElectionProducerLifetime
{
    private readonly Lock _gate = new();
    private bool _sealed;
    private int _active;
    private TaskCompletionSource? _drain;
    private Task? _sealedDrain;

    internal bool TryEnter(out IDisposable lease)
    {
        lock (_gate)
        {
            if (_sealed) { lease = null!; return false; }
            if (_active == 0) { _drain = new(TaskCreationOptions.RunContinuationsAsynchronously); }
            checked { _active++; }
            lease = new ProducerLease(this);
            return true;
        }
    }

    internal Task Seal()
    {
        lock (_gate)
        {
            _sealed = true;
            return _sealedDrain ??= _active == 0 ? Task.CompletedTask : _drain!.Task;
        }
    }

    private void Leave()
    {
        TaskCompletionSource? finished = null;
        lock (_gate)
        {
            if (--_active == 0) { finished = _drain; }
        }
        finished?.TrySetResult();
    }

    private sealed class ProducerLease(LeaderElectionProducerLifetime owner) : IDisposable
    {
        private LeaderElectionProducerLifetime? _owner = owner;
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Leave();
    }
}
