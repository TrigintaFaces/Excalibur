// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Outbox;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Delivery;

public sealed partial class OutboxProcessor
{
	[LoggerMessage(131005, LogLevel.Warning,
		"Outbox drain stopped admitting work for captured tenure {FencingToken}. Already admitted operations have been joined; unattempted claims remain recoverable. The accepted dispatch count does not report queue exhaustion or durable completion.")]
	private partial void LogTenureStopped(long? fencingToken);

	/// <summary>Owns one captured authority and the channel-only stop signal for a complete drain.</summary>
	internal sealed class ProcessorTenure : IDisposable
	{
		private readonly OutboxTenureAdmission _admission;
		private readonly CancellationToken _runToken;
		private readonly CancellationTokenSource _stop;
		private readonly CancellationTokenSource _queueCancellation;
		private int _stopped;

		internal ProcessorTenure(bool fencingActive, ILeaderProcessingGate? gate, CancellationToken runToken)
		{
			_admission = new OutboxTenureAdmission(fencingActive, gate);
			Token = _admission.Capture();
			_runToken = runToken;
			_stop = new CancellationTokenSource();
			try { _queueCancellation = CancellationTokenSource.CreateLinkedTokenSource(runToken, _stop.Token); }
			catch { _stop.Dispose(); throw; }
		}

		internal long? Token { get; }
		internal CancellationToken QueueToken => _queueCancellation.Token;
		internal bool IsStopped => Volatile.Read(ref _stopped) != 0;

		internal bool TryAdmit()
		{
			_runToken.ThrowIfCancellationRequested();
			if (!IsStopped && _admission.TryAdmit(Token)) { return true; }
			Stop();
			return false;
		}

		internal void Refuse(Exception? failure = null)
		{
			if (failure is not OutboxFencingTokenUnavailableException) { _admission.Reject(Token); }
			Stop();
		}

		internal void Stop()
		{
			if (Interlocked.Exchange(ref _stopped, 1) == 0)
			{
				// Only channel operations receive this token; actual store/transport calls retain
				// the caller's run token and must settle before their owner returns.
				_stop.Cancel();
			}
		}

		public void Dispose()
		{
			_queueCancellation.Dispose();
			_stop.Dispose();
		}
	}
}
