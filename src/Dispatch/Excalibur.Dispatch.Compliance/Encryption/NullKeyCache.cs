// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// A null implementation of <see cref="IKeyCache"/> that performs no caching.
/// </summary>
/// <remarks>
/// Use this implementation when caching is disabled or for testing scenarios
/// where caching should be bypassed.
/// </remarks>
public sealed class NullKeyCache : IKeyCache
{
	private NullKeyCache()
	{
	}

	/// <summary>
	/// Gets the singleton instance of <see cref="NullKeyCache"/>.
	/// </summary>
	public static NullKeyCache Instance { get; } = new();

	/// <inheritdoc />
	public int Count => 0;

	/// <inheritdoc />
	public async Task<KeyMetadata?> GetOrAddAsync(
		string keyId,
		Func<string, CancellationToken, Task<KeyMetadata?>> factory,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(keyId);
		ArgumentNullException.ThrowIfNull(factory);

		return await factory(keyId, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<KeyMetadata?> GetOrAddAsync(
		string keyId,
		TimeSpan ttl,
		Func<string, CancellationToken, Task<KeyMetadata?>> factory,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(keyId);
		ArgumentNullException.ThrowIfNull(factory);

		return await factory(keyId, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public KeyMetadata? TryGet(string keyId) => null;

	/// <inheritdoc />
	public void Set(KeyMetadata keyMetadata)
	{
		// No-op
	}

	/// <inheritdoc />
	public void Set(KeyMetadata keyMetadata, TimeSpan ttl)
	{
		// No-op
	}

	/// <inheritdoc />
	public void Remove(string keyId)
	{
		// No-op
	}

	/// <inheritdoc />
	public void Invalidate(string keyId)
	{
		// No-op
	}

	/// <inheritdoc />
	public void Clear()
	{
		// No-op
	}
}
