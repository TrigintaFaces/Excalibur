// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Outbox;

/// <summary>Shared local admission ordering; the store's atomic fence remains authoritative.</summary>
internal sealed class OutboxTenureAdmission
{
	private static readonly ConditionalWeakTable<object, RefusalState> Domains = new();
	private readonly bool _active;
	private readonly ILeaderProcessingGate? _gate;
	private readonly object? _domain;
	private readonly RefusalState? _state;

	internal OutboxTenureAdmission(bool active, ILeaderProcessingGate? gate)
	{
		_active = active;
		_gate = gate;
		if (!active) { return; }
		ArgumentNullException.ThrowIfNull(gate);
		// A custom getter is foreign code: resolve once, outside any registry/state lock.
		_domain = gate is ILeaderProcessingAuthorityDomain explicitDomain
			? explicitDomain.AuthorityDomain ?? throw new ArgumentException(
				"A leader processing authority domain must be a stable nonnull object.", nameof(gate))
			: gate;
		_state = Domains.GetValue(_domain, static _ => new RefusalState());
	}

	internal long? Capture()
	{
		if (!_active) { return null; }
		try
		{
			return _gate?.FencingToken ?? throw new OutboxFencingTokenUnavailableException(
				"The active outbox leader gate has no fencing token; refusing an unfenced drain.");
		}
		finally { GC.KeepAlive(_domain); }
	}

	internal bool TryAdmit(long? capturedToken)
	{
		if (!_active) { return true; }
		// The separate external observations are not an atomic authority snapshot.
		try
		{
			var authorized = capturedToken is { } token && _gate?.ShouldProcess == true && _gate.FencingToken == token;
			return _state!.TryAdmit(capturedToken, authorized);
		}
		finally { GC.KeepAlive(_domain); }
	}

	internal void Reject(long? capturedToken)
	{
		if (!_active || capturedToken is not { } token) { return; }
		try { _state!.Reject(token); }
		finally { GC.KeepAlive(_domain); }
	}

	// Values own no SDK/client/gate/domain/callback/task: only constant-size local state.
	private sealed class RefusalState
	{
		private readonly Lock _sync = new();
		private long? _rejectedThrough;

		internal bool TryAdmit(long? token, bool authorized)
		{
			lock (_sync)
			{
				return authorized && (!_rejectedThrough.HasValue || token.GetValueOrDefault() > _rejectedThrough.Value);
			}
		}

		internal void Reject(long token)
		{
			lock (_sync)
			{
				if (!_rejectedThrough.HasValue || token > _rejectedThrough.Value) { _rejectedThrough = token; }
			}
		}
	}
}
