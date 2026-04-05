// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// A no-op implementation of <see cref="IKeyCache"/> following the Null Object pattern.
/// </summary>
/// <remarks>
/// <para>
/// This class implements the Null Object design pattern: it fulfills the <see cref="IKeyCache"/>
/// contract but performs no actual caching. Every call to <see cref="GetOrAddAsync(string, Func{string, CancellationToken, Task{KeyMetadata?}}, CancellationToken)"/>
/// delegates directly to the factory, and all mutation methods (<see cref="Set(KeyMetadata)"/>,
/// <see cref="Remove"/>, <see cref="Clear"/>) are intentional no-ops.
/// </para>
/// <para>
/// Use this implementation when:
/// <list type="bullet">
///   <item><description>Encryption key caching is explicitly disabled via configuration.</description></item>
///   <item><description>A test double is needed that guarantees no caching side effects.</description></item>
///   <item><description>A non-null <see cref="IKeyCache"/> is required by a constructor but caching is unwanted.</description></item>
/// </list>
/// </para>
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

	/// <summary>
	/// No-op get-or-add with TTL for cache bypass scenarios.
	/// Always calls the factory since nothing is cached.
	/// </summary>
	/// <param name="keyId">The key identifier.</param>
	/// <param name="ttl">The time-to-live (ignored).</param>
	/// <param name="factory">The factory function to retrieve the key.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The result from the factory.</returns>
	public async Task<KeyMetadata?> GetOrAddAsync(
		string keyId,
		TimeSpan ttl,
		Func<string, CancellationToken, Task<KeyMetadata?>> factory,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(keyId);
		ArgumentNullException.ThrowIfNull(factory);
		_ = ttl;

		return await factory(keyId, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public KeyMetadata? TryGet(string keyId) => null;

	/// <inheritdoc />
	public void Set(KeyMetadata keyMetadata)
	{
		// No-op: Null Object pattern -- caching is intentionally disabled.
	}

	/// <summary>
	/// No-op set with TTL for cache bypass scenarios.
	/// </summary>
	/// <param name="keyMetadata">The key metadata (ignored).</param>
	/// <param name="ttl">The time-to-live (ignored).</param>
	public void Set(KeyMetadata keyMetadata, TimeSpan ttl)
	{
		ArgumentNullException.ThrowIfNull(keyMetadata);
		_ = ttl;
	}

	/// <inheritdoc />
	public void Remove(string keyId)
	{
		// No-op: Null Object pattern -- nothing to remove.
	}

	/// <summary>
	/// No-op invalidation for cache bypass scenarios.
	/// </summary>
	/// <param name="keyId">The key identifier (ignored).</param>
	public void Invalidate(string keyId)
	{
		ArgumentNullException.ThrowIfNull(keyId);
		// No-op: Null Object pattern -- nothing to invalidate.
	}

	/// <summary>
	/// No-op clear for cache bypass scenarios.
	/// </summary>
	public void Clear()
	{
		// No-op: Null Object pattern -- nothing to clear.
	}
}
