// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Compliance;

/// <summary>
/// One data subject's mint-ledger rows as they stood at a single instant, split by whether the key behind
/// each handle is known to have been created.
/// </summary>
/// <remarks>
/// <para>
/// <b>ONE READ, ONE INSTANT, and that is why the two sets arrive together rather than from two members.</b>
/// Reading "which handles exist" and "is anything in flight" separately opens a window between them, and
/// the whole purpose of the second set is to close a window. A caller holding this value holds a coherent
/// snapshot.
/// </para>
/// <para>
/// <b>Why PendingMint has to exist at all.</b> A mint-ledger row is written BEFORE the key is created, so
/// that a crash can never leave live key material that nothing enumerates. That ordering is correct and it
/// has a consequence: between the row and the key, a write is invisible to both observables an erasure has.
/// The ledger's handle set has not changed since the erasure read it, and the key material reads absent
/// because it genuinely has not been created yet. <b>That state is observationally identical to a writer
/// that crashed between its row and its mint</b> — which is safe, and which the erasure correctly ignores.
/// The two diverge only after the erasure has signed its certificate.
/// </para>
/// <para>
/// So no amount of re-reading the handle set, and no number of probes of the key material, can separate the
/// safe case from the unsafe one. The row itself has to say which side of the mint it is on. That
/// information already exists — the write-ahead ordering creates it — and recording it is what makes the
/// distinction observable.
/// </para>
/// <para>
/// <b>This DETECTS a concurrent write; it does not EXCLUDE one.</b> An erasure that finds a pending row
/// reports a completed-except-concurrent-writes outcome naming the handles, which is loud and recoverable,
/// instead of signing a clean completion over personal data that is about to become readable. Excluding the
/// write would require mutual exclusion between the write path and the erasure, which this contract does
/// not impose.
/// </para>
/// </remarks>
public sealed class MintLedgerSnapshot
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MintLedgerSnapshot"/> class.
	/// </summary>
	/// <param name="minted">Handles whose key creation was confirmed.</param>
	/// <param name="pendingMint">Handles whose key creation was NOT confirmed.</param>
	/// <exception cref="ArgumentNullException">Thrown when either argument is <see langword="null"/>.</exception>
	public MintLedgerSnapshot(IReadOnlyList<string> minted, IReadOnlyList<string> pendingMint)
	{
		Minted = minted ?? throw new ArgumentNullException(nameof(minted));
		PendingMint = pendingMint ?? throw new ArgumentNullException(nameof(pendingMint));
	}

	/// <summary>
	/// Gets the handles whose key creation was confirmed.
	/// </summary>
	/// <value>
	/// An erasure must destroy or deliberately spare every one of these before it may attest a completed
	/// erasure.
	/// </value>
	public IReadOnlyList<string> Minted { get; }

	/// <summary>
	/// Gets the handles whose row exists but whose key creation was NOT confirmed.
	/// </summary>
	/// <value>
	/// A non-empty set means an erasure cannot honestly certify: a write is in flight and its key may come
	/// into existence after the erasure finishes.
	/// </value>
	public IReadOnlyList<string> PendingMint { get; }

	/// <summary>
	/// Gets every handle in this snapshot, whether or not its key creation was confirmed.
	/// </summary>
	/// <value>
	/// The union of <see cref="Minted"/> and <see cref="PendingMint"/> — the set an erasure ATTEMPTS to
	/// destroy. A pending handle is attempted because the key may already exist: the confirmation write can
	/// be lost while the key behind it survives, so treating a pending row as "no key" would leave exactly
	/// the handle this contract exists to reach. Destroying a handle that holds nothing is harmless — the
	/// provider answers not-found and nothing is attested from the absence.
	/// </value>
	public IEnumerable<string> All => Minted.Concat(PendingMint);
}
