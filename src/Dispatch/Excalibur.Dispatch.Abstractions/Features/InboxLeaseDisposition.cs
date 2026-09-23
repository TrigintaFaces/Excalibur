// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Features;

/// <summary>
/// What the inbox deduplication stage did with a dispatch, reported back to the caller that owns
/// finalization of the corresponding inbox entry.
/// </summary>
/// <remarks>
/// A caller that finalizes an entry — marks it processed, or records its external identifier as
/// deduplicated — MUST first confirm the stage reported <see cref="Admitted"/>. Treating
/// <see cref="Declined"/> as a completion records a handler invocation that never happened, and
/// because the external identifier is then remembered as already handled, a later redelivery of the
/// same message is finalized without reaching a handler either: the message becomes unrecoverable by
/// retry rather than merely lost once.
/// </remarks>
public enum InboxLeaseDisposition
{
	/// <summary>
	/// The inbox stage did not evaluate this dispatch — it is absent from the pipeline, or the
	/// dispatch did not reach it. Carries no information about whether a handler ran.
	/// </summary>
	NotEvaluated = 0,

	/// <summary>
	/// The stage admitted the invocation and the handler ran. The entry is safe to finalize.
	/// </summary>
	Admitted = 1,

	/// <summary>
	/// A live lease on the entry is held elsewhere, so the stage did not run the handler. The entry
	/// MUST NOT be finalized on this dispatch; the holder of the live lease finalizes it.
	/// </summary>
	Declined = 2,
}
