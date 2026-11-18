// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.Compliance.CryptoShredding;

/// <summary>
/// A mint ledger for a deployment that performs no erasure: recording an intent is a no-op, and
/// enumerating is refused.
/// </summary>
/// <remarks>
/// <para>
/// This exists for one real scenario: encrypting personal data at rest — in the event store, the inbox, the
/// outbox — <b>without</b> running the GDPR erasure subsystem. The mint ledger exists so that an erasure can
/// enumerate the handles it must decide about; in a deployment with no erasure, <b>nothing ever
/// enumerates</b>, so the completeness obligation the ledger carries is vacuous rather than unmet. The
/// ledger is ABSENT here, not falsified.
/// </para>
/// <para>
/// <b>The asymmetry between the two members is the whole design, and it is not an oversight.</b> Recording
/// is a no-op because the row has no reader; enumerating THROWS because a caller that enumerates in a
/// deployment declared to perform no erasure has contradicted its own registration. <b>Returning an empty
/// list instead would be the catastrophic direction</b>: an erasure would conclude that the subject has no
/// handles, destroy only what it can derive, and certify. The throw makes a mis-composition loud at the
/// first erasure rather than silent at every erasure.
/// </para>
/// <para>
/// <b>Do not register this alongside an erasure store.</b> Every ledger registration is a
/// <c>TryAdd</c>, so in one of the two registration orders this no-op would stand in front of a real
/// erasure store — and then the erasure's enumeration is answered by a type that holds no rows. Startup
/// validation refuses that composition rather than letting registration order decide it.
/// </para>
/// </remarks>
internal sealed class NoErasureKeyMintLedger : IKeyMintLedger
{
	/// <inheritdoc />
	/// <remarks>
	/// <b>TRUE, vacuously, and the reasoning matters because FALSE would be the wrong answer here.</b> The
	/// property asks whether an enumeration can silently UNDER-REPORT. This ledger never reports: it keeps
	/// no rows and <see cref="ListHandlesAsync"/> throws. It therefore cannot under-report, and a startup
	/// refusal aimed at volatile ledgers must not fire for it -- a deployment performing no erasure is a
	/// composition this framework supports, and it is refused by a different check if it registers an
	/// erasure store beside this.
	/// </remarks>
	public bool IsDurable => true;

	/// <inheritdoc />
	/// <remarks>
	/// A no-op, and that is the true answer rather than a placeholder: the row exists so an erasure can
	/// enumerate it, and this deployment has no erasure. It deliberately does NOT throw — the write path
	/// records an intent on every first write per subject and obligation class, so throwing here would fail
	/// ordinary personal-data writes in a composition this framework supports.
	/// </remarks>
	public Task RecordMintIntentAsync(
		string keyHandle,
		TenantId tenantId,
		string dataSubjectIdHash,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(keyHandle);
		ArgumentNullException.ThrowIfNull(tenantId);
		ArgumentException.ThrowIfNullOrWhiteSpace(dataSubjectIdHash);
		cancellationToken.ThrowIfCancellationRequested();

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	/// <remarks>
	/// A no-op, matching <see cref="RecordMintIntentAsync"/> and for the same reason: there is no row to
	/// confirm, because nothing here keeps one. It deliberately does NOT throw the no-row exception the
	/// contract allows -- that exception exists to catch a caller who skipped the intent step, and in this
	/// deployment the intent step is itself a no-op, so raising it would fail ordinary personal-data writes
	/// in a composition this framework supports.
	/// </remarks>
	public Task MarkMintedAsync(string keyHandle, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(keyHandle);
		cancellationToken.ThrowIfCancellationRequested();

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	/// <remarks>
	/// Refused, loudly, and NOT answered with an empty list. An empty list is indistinguishable from "this
	/// subject has no handles", which would let an erasure destroy only what it can derive and then certify
	/// — the silent catastrophic direction this whole subsystem exists to remove. A caller reaching this
	/// member has composed an erasure against a deployment registered as performing none, and that is a
	/// wiring defect to be reported rather than absorbed.
	/// </remarks>
	public Task<MintLedgerSnapshot> ListHandlesAsync(
		TenantId tenantId,
		string dataSubjectIdHash,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(tenantId);
		ArgumentException.ThrowIfNullOrWhiteSpace(dataSubjectIdHash);

		throw new InvalidOperationException(
			"This deployment is registered as performing no erasure, so no mint rows are kept and the "
			+ "handles holding a data subject's personal fields cannot be enumerated. An empty list is NOT "
			+ "returned, deliberately: an erasure would read it as 'this subject has no handles', destroy "
			+ "only the handles it can derive, and report a completed erasure. Register an erasure store "
			+ "instead -- AddInMemoryErasureStore(), AddPostgresErasureStore() or "
			+ "AddSqlServerErasureStore() -- which supplies a ledger that persists the rows an erasure "
			+ "enumerates.");
	}
}
