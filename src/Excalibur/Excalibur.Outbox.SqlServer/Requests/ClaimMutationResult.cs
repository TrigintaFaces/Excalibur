// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>
/// The discriminators an unfenced guarded mutation reports, evaluated in the same round trip as the
/// mutation itself.
/// </summary>
/// <remarks>
/// The sibling of <c>FencedClaimMutationResult</c> for statements that present no fencing token. It exists
/// because a row count alone cannot answer why a guarded update applied to nothing: the dead-letter
/// statement refuses both a row that is absent and a row that is already terminal, and those two call for
/// different handling by the caller. Probing afterwards in a second round trip would be answering about a
/// row that may have changed in between, so the probes are part of the same batch.
/// </remarks>
internal sealed class ClaimMutationResult
{
	/// <summary>Gets or sets the number of rows the guarded mutation applied to.</summary>
	public int UpdatedCount { get; set; }

	/// <summary>Gets or sets a value indicating whether the addressed row exists at all.</summary>
	public bool RowExists { get; set; }

	/// <summary>Gets or sets a value indicating whether the addressed row is already sent or dead-lettered.</summary>
	public bool IsTerminal { get; set; }
}
