// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Tracks per-tag version stamps used to invalidate tagged cache entries without an atomic
/// key-set backend.
/// </summary>
/// <remarks>
/// <para>
/// A tag is not a set of keys. It is a single opaque version stamp. A cache entry written under a
/// tag records that tag's stamp, at write time, alongside its value. A read compares the entry's own
/// recorded stamp against the tag's current stamp: equal means the entry has not been invalidated
/// since it was written; different means it has. Invalidating a tag simply replaces its stamp — every
/// entry that recorded the old stamp stops matching on its next read and is treated as invalidated,
/// without the tracker ever having to enumerate or delete the entries themselves.
/// </para>
/// <para>
/// This design requires only a single-key read and a single-key write per tag, both of which are
/// atomic on every <c>IDistributedCache</c> backend. It replaces an older, key-set-based design that
/// needed a non-atomic read-modify-write to add a key to a tag's key set, and was vulnerable to losing
/// concurrent registrations under that backend.
/// </para>
/// <para>
/// <b>Two different kinds of "no stamp" exist and must not be treated as the same condition.</b> A tag
/// that has no stamp yet in the backend (a tag nothing has ever invalidated) is a normal, healthy
/// state — the caller should proceed as if the entry were valid. A cache <em>entry</em> that has no
/// recorded stamp for a tag it claims to be written under (for example, an entry serialized before
/// this per-tag-stamp mechanism existed) is not healthy — the entry has nothing to compare, so it
/// cannot be proven valid and must be treated as invalidated. Collapsing these two into a single
/// "absent" branch would let a pre-upgrade entry with no recorded stamp be served as valid forever.
/// </para>
/// </remarks>
public interface ICacheTagTracker
{
	/// <summary>
	/// Gets the current version stamp for a tag, creating one if the tag has never been observed
	/// before.
	/// </summary>
	/// <param name="tag">The tag to resolve a version stamp for.</param>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>
	/// A task that resolves to the tag's current version stamp: an opaque string that changes only
	/// when the tag is invalidated via <see cref="BumpStampAsync"/>. The returned value carries no
	/// meaning beyond equality comparison and must never be parsed, ordered, or compared for
	/// recency — only for equality against a previously recorded stamp.
	/// </returns>
	/// <remarks>
	/// Called both when writing a new cache entry (to embed the tag's stamp at write time) and when
	/// reading an existing entry (to compare against the stamp it recorded at write time). A well
	/// behaved implementation memoizes the in-flight resolution per tag so that concurrent callers
	/// for the same tag collapse onto a single backend round trip rather than each starting their own.
	/// </remarks>
	Task<string> GetOrCreateStampAsync(string tag, CancellationToken cancellationToken);

	/// <summary>
	/// Invalidates a tag by replacing its current version stamp with a new one.
	/// </summary>
	/// <param name="tag">The tag to invalidate.</param>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>A task that completes once the tag's stamp has been replaced.</returns>
	/// <remarks>
	/// Every cache entry that recorded the tag's previous stamp stops matching on its next read and is
	/// treated as invalidated. This does not delete or enumerate any cache entry; stale entries are
	/// discovered lazily, on their next read, and evicted at that point.
	/// </remarks>
	Task BumpStampAsync(string tag, CancellationToken cancellationToken);
}
