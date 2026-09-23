// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Caching.Distributed;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Places every key it is given under a configured partition, so two applications sharing one cache server
/// cannot address each other's entries.
/// </summary>
/// <remarks>
/// <para>
/// <b>This decorator is deliberately NOT applied by the general cache decoration path, and it does NOT
/// carry that path's <c>MemoryDistributedCache</c> exemption.</b> That exemption exists because an
/// in-memory cache cannot stall on I/O, so bounding its latency buys nothing — reasoning about latency,
/// which says nothing about key isolation. An in-memory distributed cache can absolutely serve two
/// applications in one process; a test fixture is exactly that. Skipping the scope there would remove the
/// partition in the one configuration where two applications are most easily pointed at one cache.
/// </para>
/// <para>
/// Only the <see cref="IDistributedCache"/> surface is forwarded. A consumer resolving the scoped cache
/// therefore does not get the buffer-based surface even when the inner cache implements it; that costs an
/// allocation on the byte-array path and is not a correctness difference. The scoped cache is opt-in by
/// service key, so nothing loses the buffer path without asking for the scoped one.
/// </para>
/// </remarks>
internal sealed class ScopedDistributedCache : IDistributedCache
{
	private readonly IDistributedCache _inner;
	private readonly string _prefix;

	/// <summary>
	/// Initializes a new instance of the <see cref="ScopedDistributedCache"/> class.
	/// </summary>
	/// <param name="inner">The cache being partitioned.</param>
	/// <param name="scope">The partition every key is placed under.</param>
	/// <exception cref="ArgumentNullException"><paramref name="inner"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="scope"/> is empty or whitespace.</exception>
	public ScopedDistributedCache(IDistributedCache inner, string scope)
	{
		ArgumentNullException.ThrowIfNull(inner);

		// Fails closed. An empty scope would forward keys unchanged, which is indistinguishable from having
		// no scoped cache at all -- except that the caller asked for one and would be told it had it.
		ArgumentException.ThrowIfNullOrWhiteSpace(scope);

		_inner = inner;
		_prefix = scope + ":";
	}

	/// <inheritdoc />
	public byte[]? Get(string key) => _inner.Get(Scoped(key));

	/// <inheritdoc />
	public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
		_inner.GetAsync(Scoped(key), token);

	/// <inheritdoc />
	public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
		_inner.Set(Scoped(key), value, options);

	/// <inheritdoc />
	public Task SetAsync(
		string key,
		byte[] value,
		DistributedCacheEntryOptions options,
		CancellationToken token = default) =>
		_inner.SetAsync(Scoped(key), value, options, token);

	/// <inheritdoc />
	public void Refresh(string key) => _inner.Refresh(Scoped(key));

	/// <inheritdoc />
	public Task RefreshAsync(string key, CancellationToken token = default) =>
		_inner.RefreshAsync(Scoped(key), token);

	/// <inheritdoc />
	public void Remove(string key) => _inner.Remove(Scoped(key));

	/// <inheritdoc />
	public Task RemoveAsync(string key, CancellationToken token = default) =>
		_inner.RemoveAsync(Scoped(key), token);

	/// <summary>
	/// Places a caller's key under this cache's partition.
	/// </summary>
	/// <remarks>
	/// Every member routes through this, including <c>Remove</c> and <c>Refresh</c>. A member that forgot to
	/// would not fail — it would operate on the unpartitioned key, so a delete would silently miss its own
	/// entry and could reach another application's.
	/// </remarks>
	private string Scoped(string key)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);

		return _prefix + key;
	}
}
