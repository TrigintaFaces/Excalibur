// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing;

/// <summary>
/// What an append actually did, as a single named outcome rather than a combination of flags.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a discriminator and not another flag.</b> An append has five reported outcomes and exactly one of them
/// holds at a time. Expressing that as independent booleans leaves three quarters of the combinations
/// unreachable but still constructible, and each new flag doubles them — a named constructor per legal
/// combination is this enumeration written longhand, with the illegal ones left representable.
/// </para>
/// <para>
/// <b>The state this exists to add is <see cref="AlreadyCommitted"/>.</b> A store that recognises its own
/// durably-present events after a lost acknowledgement previously had no way to say so: it reported plain
/// success, which is true about the append and silently false about the call. A caller that must behave
/// differently on a recognised retry — because it holds live in-memory payloads whose retrievability it
/// cannot assume — could not distinguish the two, and no care at the call site recovers a distinction the
/// type does not carry.
/// </para>
/// </remarks>
public enum AppendOutcome
{
	/// <summary>
	/// This call wrote the events and the store acknowledged the write.
	/// </summary>
	Committed = 0,

	/// <summary>
	/// The events were already durably present when this call ran, and the store recognised them as its
	/// own by identity rather than writing them a second time.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is a <b>success</b> — the append the caller asked for is durable — so
	/// <see cref="AppendResult.Success"/> is <see langword="true"/> and a caller that only asks "did it
	/// work" is answered correctly without change.
	/// </para>
	/// <para>
	/// It is nevertheless distinct from <see cref="Committed"/>, because the rows were written by an
	/// earlier call whose acknowledgement was lost, and <em>present</em> does not imply
	/// <em>retrievable</em>: those rows may have been acted on in between. A caller still holding the live
	/// payloads must not assume they are what the store would now return.
	/// </para>
	/// </remarks>
	AlreadyCommitted = 1,

	/// <summary>
	/// The append lost its version precondition to another writer. Nothing was written.
	/// </summary>
	ConcurrencyConflict = 2,

	/// <summary>
	/// The append failed for its own reasons. Nothing was written.
	/// </summary>
	Failed = 3,

	/// <summary>The provider cannot determine whether the complete atomic append committed.</summary>
	/// <remarks>
	/// Neither a failed acknowledgement nor an empty recovery read proves rollback. Preserve the
	/// original operation identity, payloads and expected version for reconciliation. Do not regenerate
	/// or rebase the operation merely because this outcome is not success.
	/// </remarks>
	Unknown = 4
}
