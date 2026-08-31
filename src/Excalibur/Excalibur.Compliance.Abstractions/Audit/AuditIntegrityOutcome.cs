// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Compliance;

/// <summary>
/// Describes what an audit log integrity verification was able to establish.
/// </summary>
/// <remarks>
/// Integrity verification has three outcomes, not two. A run over a window that contained no audit events
/// establishes nothing, and is reported as <see cref="NoEventsInScope"/> rather than being collapsed into
/// either a pass or a failure. Consumers producing compliance evidence must report that case distinctly:
/// an unexamined window is not evidence that the audit log is intact.
/// </remarks>
public enum AuditIntegrityOutcome
{
	/// <summary>
	/// The verification window contained no audit events, so integrity was never exercised.
	/// </summary>
	/// <remarks>
	/// This is neither a pass nor a failure. No conclusion about the integrity of the audit log follows from
	/// it, and it must not be reported as one. A window that is unexpectedly empty may itself indicate that
	/// audit events are not reaching the store.
	/// </remarks>
	NoEventsInScope = 0,

	/// <summary>
	/// Every audit event in the verification window was examined and the hash chain was intact.
	/// </summary>
	Verified = 1,

	/// <summary>
	/// At least one audit event in the verification window failed verification.
	/// </summary>
	ViolationsDetected = 2
}
