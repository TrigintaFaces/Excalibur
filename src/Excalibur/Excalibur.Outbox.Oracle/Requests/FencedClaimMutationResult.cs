// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox.Oracle.Requests;

/// <summary>
/// The result of a fenced completion write: the effective high-water token after the fence advance, how
/// many rows the guarded mutation applied to, and whether the addressed row exists at all.
/// </summary>
/// <remarks>
/// All three are produced INSIDE the one PL/SQL block, so the classification describes the window the
/// mutation actually ran in rather than a later one. Reading the high-water separately afterwards would
/// answer a question about a different instant, which is the defect the fenced members exist to remove.
/// <para>
/// This carries a row-existence flag where <see cref="FenceMutationResult"/> does not, because the failure
/// path must distinguish a lost claim from a vanished row: the first means keep draining the rest of the
/// batch, the second means this message is simply gone. The delete path has no such distinction to make.
/// </para>
/// </remarks>
internal sealed class FencedClaimMutationResult
{
	/// <summary>Gets or sets the high-water token in force after the fence advance.</summary>
	public long HighWaterToken { get; set; }

	/// <summary>Gets or sets the number of rows the guarded mutation applied to.</summary>
	public int UpdatedCount { get; set; }

	/// <summary>Gets or sets a value indicating whether the addressed row exists at all.</summary>
	public bool RowExists { get; set; }
}
