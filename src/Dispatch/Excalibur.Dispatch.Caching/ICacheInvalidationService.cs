// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Service for removing cached entries.
/// </summary>
public interface ICacheInvalidationService
{
	/// <summary>
	/// Invalidates cached items associated with the provided tags.
	/// </summary>
	/// <param name="tags"> Tags to remove. </param>
	/// <param name="cancellationToken"> Token used to cancel the operation. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task InvalidateTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken);

	/// <summary>
	/// Invalidates cached items associated with the provided keys.
	/// </summary>
	/// <param name="keys"> Cache keys to remove. </param>
	/// <param name="cancellationToken"> Token used to cancel the operation. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task InvalidateKeysAsync(IEnumerable<string> keys, CancellationToken cancellationToken);
}
