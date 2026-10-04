// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing;

/// <summary>Administrative capability for bounded, resumable archive discovery.</summary>
/// <remarks>
/// Resolve from the same captured hot store as archive reads and tombstoning. Restricting decorators
/// must explicitly mediate or deny this capability. A round captures policy values, an age evaluation
/// instant, and a finite stream-membership horizon. Later pages retain those inputs; policy reloads
/// apply to the next round. The horizon bounds stream membership, not event eligibility: retention
/// and safe-prefix selection inspect each selected stream's entire current history.
/// Pages advance over examined streams, including streams with no eligible work. Under continued
/// successful retrieval and valid, stable identities, every stream in the finite round is eventually examined.
/// This does not guarantee successful archival or progress across repeated process restarts.
/// </remarks>
public interface IEventStoreArchiveScanner
{
	/// <summary>Examines a bounded page of streams in an archive scan round.</summary>
	/// <param name="policy">Policy to copy when starting a round; ignored when continuing a round.</param>
	/// <param name="scanSize">Positive maximum number of stream identities examined.</param>
	/// <param name="continuation">The previous page's continuation, or null to start a round.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A page whose continuation is nonnull until the round is exhausted, even on empty candidate pages.</returns>
	/// <remarks>
	/// Foreign, malformed, or unsupported continuations throw; they must not restart a scan or report
	/// exhaustion. Consumers retain the previous continuation on fetch failure or cancellation, and
	/// accept the next continuation only after attempting every candidate on the page. Individual
	/// candidate failures must not prevent that advancement. Retrying a page can repeat candidates.
	/// </remarks>
	ValueTask<ArchiveScanPage> ScanArchiveCandidatesAsync(
		ArchivePolicy policy, int scanSize, ArchiveScanCursor? continuation, CancellationToken cancellationToken);
}
