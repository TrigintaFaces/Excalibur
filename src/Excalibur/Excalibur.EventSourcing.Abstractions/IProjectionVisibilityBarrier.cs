// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing;

/// <summary>Establishes search visibility for projection writes observed by a processing page.</summary>
/// <remarks>
/// Resolve this optional capability through the projection store's service-provider interface.
/// A processor must await the barrier after successful folds, including skipped duplicate or
/// superseded writes, and before acknowledging the page in its checkpoint. Write-level refresh
/// settings alone cannot cover a competing writer whose state was observed before its refresh finished.
/// Failure or cancellation must prevent acknowledgment; this barrier does not make external effects atomic.
/// </remarks>
public interface IProjectionVisibilityBarrier
{
	/// <summary>Makes writes already observed by this processing page visible to subsequent searches.</summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task completing only after the store's search visibility barrier succeeds.</returns>
	Task EnsureVisibleAsync(CancellationToken cancellationToken);
}
