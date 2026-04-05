// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Provides administrative key cache operations.
/// Implementations should implement this alongside <see cref="IKeyCache"/>.
/// </summary>
public interface IKeyCacheAdmin
{
	/// <summary>Gets or adds key metadata with a custom TTL.</summary>
	Task<KeyMetadata?> GetOrAddAsync(string keyId, TimeSpan ttl, Func<string, CancellationToken, Task<KeyMetadata?>> factory, CancellationToken cancellationToken);

	/// <summary>Adds or updates key metadata with a custom TTL.</summary>
	void Set(KeyMetadata keyMetadata, TimeSpan ttl);

	/// <summary>Invalidates all cached entries for a specific key.</summary>
	void Invalidate(string keyId);

	/// <summary>Clears all cached key metadata.</summary>
	void Clear();
}
