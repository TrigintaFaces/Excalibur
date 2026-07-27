// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Compliance;

/// <summary>
/// The outcome state of a key-destruction request, distinguishing a key that is irrecoverable on return
/// from one that is merely scheduled for later irreversible destruction.
/// </summary>
/// <remarks>
/// This tri-state exists because backends differ: some destroy key material immediately (rendering data
/// encrypted under it unrecoverable the instant the call returns), while others only schedule deletion
/// behind a mandatory retention window during which the key — and thus the data — remains recoverable.
/// A completion attestation (e.g. GDPR Right-to-Erasure) may only be issued for the immediate case.
/// </remarks>
public enum KeyDestructionState
{
	/// <summary>The key did not exist; the destruction is an idempotent no-op (already erased or never created).</summary>
	NotFound = 0,

	/// <summary>The key material is destroyed and irrecoverable as of <see cref="KeyDestructionOutcome.IrreversibleAt"/>.</summary>
	Completed = 1,

	/// <summary>
	/// The key is scheduled for irreversible destruction but remains recoverable until
	/// <see cref="KeyDestructionOutcome.IrreversibleAt"/>. No erasure may be attested as complete before then.
	/// </summary>
	ScheduledIrreversible = 2,
}

/// <summary>
/// The result of a key-destruction request: its <see cref="State"/> and, when known, the instant at which
/// the key material is (or becomes) irrecoverable.
/// </summary>
/// <param name="State">The destruction state.</param>
/// <param name="IrreversibleAt">
/// The UTC instant at which the key material is irrecoverable: the call time for <see cref="KeyDestructionState.Completed"/>,
/// the end of the retention window for <see cref="KeyDestructionState.ScheduledIrreversible"/>, and <see langword="null"/>
/// for <see cref="KeyDestructionState.NotFound"/>.
/// </param>
public readonly record struct KeyDestructionOutcome(KeyDestructionState State, DateTimeOffset? IrreversibleAt)
{
	/// <summary>Gets the idempotent no-op outcome for a key that did not exist.</summary>
	public static KeyDestructionOutcome NotFound { get; } = new(KeyDestructionState.NotFound, null);

	/// <summary>Creates a <see cref="KeyDestructionState.Completed"/> outcome irrecoverable as of <paramref name="at"/>.</summary>
	/// <param name="at">The UTC instant at which the key material became irrecoverable.</param>
	public static KeyDestructionOutcome CompletedAt(DateTimeOffset at) => new(KeyDestructionState.Completed, at);

	/// <summary>Creates a <see cref="KeyDestructionState.ScheduledIrreversible"/> outcome irrecoverable at <paramref name="irreversibleAt"/>.</summary>
	/// <param name="irreversibleAt">The UTC instant at which the scheduled key material becomes irrecoverable.</param>
	public static KeyDestructionOutcome ScheduledAt(DateTimeOffset irreversibleAt) => new(KeyDestructionState.ScheduledIrreversible, irreversibleAt);

	/// <summary>Gets a value indicating whether the key material is irrecoverable now (i.e. <see cref="State"/> is <see cref="KeyDestructionState.Completed"/>).</summary>
	public bool IsIrreversibleNow => State == KeyDestructionState.Completed;
}
