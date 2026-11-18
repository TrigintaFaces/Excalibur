// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// Records THIS instance's retention declarations in the deployment-wide record, once per process, and
/// only reports success when every one of them is recorded.
/// </summary>
/// <remarks>
/// <para>
/// <b>The advance does not protect the advancing instance. It protects OTHER instances FROM it.</b> This
/// instance's own safety comes from reading the record; what the write buys is that an instance which
/// does NOT hold these declarations can discover that it is behind. So an unrecorded pair is a pair for
/// which no stale instance can ever detect its own staleness — and nothing anywhere reports that.
/// </para>
/// <para>
/// <b>A PREFIX IS THEREFORE NOT A WEAKER SUCCESS. IT IS A HOLE WITH NO TELL.</b> The latch closes only on
/// COMPLETE success. Anything less leaves it open, so the next caller tries again.
/// </para>
/// <para>
/// <b>A <see cref="Lazy{T}"/> of a task would be the wrong primitive, and the reason is specific.</b> A
/// lazy task caches the FAULTED task, so one transient store fault would mean this process never retries
/// the advance for its whole lifetime — and because erasure depends on the advance, never erases again
/// either. A semaphore and a flag set only after full success are re-entrant on failure by construction.
/// </para>
/// <para>
/// <b>Registered as a SINGLETON and fired from the scheduler's poll, not from the executor.</b> Firing it
/// before this instance's first ERASURE would mean protection begins at that erasure, which in a
/// low-volume deployment is days after the deploy that created the hazard — and the first post-deploy
/// erasure would be decided by poll timing. Firing it from the poll shrinks that window to one polling
/// interval after the instance boots. It is deliberately NOT a readiness check: a compliance-store
/// outage must not stop the application serving, it must only withhold that instance's erasures.
/// </para>
/// </remarks>
internal sealed partial class ErasureRetentionAdvanceLatch : IDisposable
{
	private readonly IErasureRetentionRegistry _retentions;
	private readonly ILogger<ErasureRetentionAdvanceLatch> _logger;
	private readonly SemaphoreSlim _gate = new(1, 1);

	private bool _advanced;

	// Volatile because the flag is read outside the gate: without the barrier a caller can observe this
	// latch as live while it is already torn down. The sibling above needs no modifier because every
	// access to it goes through Volatile.Read/Write explicitly.
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="ErasureRetentionAdvanceLatch"/> class.
	/// </summary>
	/// <param name="retentions">This instance's own declarations.</param>
	/// <param name="logger">The logger.</param>
	public ErasureRetentionAdvanceLatch(
		IErasureRetentionRegistry retentions,
		ILogger<ErasureRetentionAdvanceLatch> logger)
	{
		_retentions = retentions ?? throw new ArgumentNullException(nameof(retentions));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Records this instance's declarations if it has not already done so successfully.
	/// </summary>
	/// <param name="ledger">
	/// The deployment-wide record, or <see langword="null"/> when the configured store provides none — an
	/// in-memory deployment is one process, so there is nothing for another instance to be behind.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A task that completes when this instance's declarations are recorded in full.</returns>
	/// <remarks>
	/// Propagates whatever the record threw. The caller treats that as "not advanced", which is what
	/// keeps the latch open for the next attempt — and what makes erasure wait, since an instance whose
	/// declarations are unrecorded is one no other instance can detect itself as behind.
	/// </remarks>
	public async Task EnsureAdvancedAsync(
		IErasureRetentionDeclarationLedger? ledger,
		CancellationToken cancellationToken)
	{
		if (ledger is null || Volatile.Read(ref _advanced))
		{
			return;
		}

		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (_advanced)
			{
				return;
			}

			var declared = _retentions.Declared
				.Select(static r => new DeclaredRetentionKey(
					ErasureRetentionStalenessGate.TenantKeyOf(r.Tenant.TenantId), r.AggregateType))
				.Distinct()
				.ToArray();

			// Throws on anything short of full success, which leaves the flag false so the next attempt
			// retries. The record's own contract requires it never to report a partial advance as done.
			await ledger.RecordDeclaredRetentionsAsync(declared, cancellationToken).ConfigureAwait(false);

			Volatile.Write(ref _advanced, true);
			LogRetentionDeclarationsRecorded(declared.Length);
		}
		finally
		{
			_ = _gate.Release();
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_gate.Dispose();
	}

	[LoggerMessage(
		ComplianceEventId.ErasureRetentionDeclarationsRecorded,
		LogLevel.Information,
		"Recorded {Count} retention declaration(s) in the deployment-wide record, so other instances can detect when they hold fewer than this one")]
	private partial void LogRetentionDeclarationsRecorded(int count);
}
