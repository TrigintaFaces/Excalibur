// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>
/// The single-row result of a fenced completion write: the effective high-water token after the fence
/// compare-and-swap, how many rows the guarded mutation applied to, and whether the row existed at all.
/// </summary>
/// <remarks>
/// All three are read INSIDE the statement's own transaction, so the classification describes the window the
/// mutation actually ran in rather than a later one. Reading the high-water separately afterwards would
/// answer a question about a different instant -- which is the defect this type exists to avoid.
/// </remarks>
internal sealed class FencedClaimMutationResult
{
	/// <summary>Gets or sets the high-water token in force after the fence compare-and-swap.</summary>
	public long HighWaterToken { get; set; }

	/// <summary>Gets or sets the number of rows the guarded mutation applied to.</summary>
	public int UpdatedCount { get; set; }

	/// <summary>Gets or sets a value indicating whether the addressed row exists at all.</summary>
	public bool RowExists { get; set; }

	/// <summary>Gets or sets a value indicating whether the addressed row exists in a terminal status.</summary>
	public bool IsTerminal { get; set; }
}
