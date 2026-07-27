// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch;

/// <summary>
/// A processing gate that additionally exposes a fencing token for the current processing cycle, so
/// callers can fence downstream mutations to the current tenure.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="FencingToken"/> is independent of <see cref="IProcessingGate.ShouldProcess"/>: the latter
/// is the authorization decision (should this cycle run at all), while the former is the value to stamp
/// on any protected mutation performed during this cycle. A <see langword="null"/> token means no
/// fencing applies to this instance — for example, single-instance deployments or configurations with no
/// leader election — and any consumer of the token must treat <see langword="null"/> as "no fencing",
/// never as "processing is disallowed".
/// </para>
/// </remarks>
public interface ILeaderProcessingGate : IProcessingGate
{
	/// <summary>
	/// Gets the fencing token to stamp on this cycle's protected mutations.
	/// </summary>
	/// <value>
	/// The fencing token for the current tenure, or <see langword="null"/> when no fencing applies
	/// (for example, a single-instance deployment or no leader election configured).
	/// </value>
	long? FencingToken { get; }
}
