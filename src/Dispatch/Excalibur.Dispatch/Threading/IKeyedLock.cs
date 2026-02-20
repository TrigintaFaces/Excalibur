// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Threading;

/// <summary>
/// Provides a mechanism for managing locks based on keys.
/// </summary>
public interface IKeyedLock
{
	/// <summary>
	/// Acquires a lock for the specified key.
	/// </summary>
	/// <param name="key"> The key to lock on. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A disposable lock handle. </returns>
	Task<IDisposable> AcquireAsync(string key, CancellationToken cancellationToken);
}
