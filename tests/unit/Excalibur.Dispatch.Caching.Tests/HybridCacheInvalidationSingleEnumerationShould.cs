// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections;

using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Caching.Hybrid;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// uep5a7 (xhygd8 S869 coverage edge) — <see cref="HybridCacheInvalidationService"/> must materialize a
/// consumer-supplied tag/key sequence ONCE. The fix reads <c>tags as IReadOnlyCollection&lt;string&gt; ??
/// tags.ToArray()</c> before the emptiness check + the <c>RemoveByTagAsync</c> call; a deferred/lazy or
/// side-effecting <see cref="IEnumerable{T}"/> would otherwise be enumerated twice (the pre-fix
/// <c>if (tags.Any())</c> then <c>RemoveByTagAsync(tags)</c>), which can disagree or run side effects twice.
/// </summary>
/// <remarks>
/// NON-VACUITY: the pre-fix double-enumeration would make <c>EnumerationCount == 2</c> → <c>ShouldBe(1)</c>
/// RED. The counting sequence deliberately does NOT implement <see cref="IReadOnlyCollection{T}"/>, so the
/// <c>as</c> shortcut cannot bypass the materializing <c>ToArray()</c>.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[Trait("Feature", "Caching")]
public sealed class HybridCacheInvalidationSingleEnumerationShould
{
    [Fact]
    public async Task EnumerateTagSequenceExactlyOnce_WhenInvalidatingTags()
    {
        ICacheInvalidationService service = new HybridCacheInvalidationService(A.Fake<HybridCache>());
        var tags = new CountingEnumerable(["tag-a", "tag-b"]);

        await service.InvalidateTagsAsync(tags, CancellationToken.None);

        tags.EnumerationCount.ShouldBe(
            1,
            "uep5a7: a lazy/side-effecting tag sequence must be materialized once, never enumerated twice.");
    }

    [Fact]
    public async Task EnumerateKeySequenceExactlyOnce_WhenInvalidatingKeys()
    {
        ICacheInvalidationService service = new HybridCacheInvalidationService(A.Fake<HybridCache>());
        var keys = new CountingEnumerable(["key-a", "key-b"]);

        await service.InvalidateKeysAsync(keys, CancellationToken.None);

        keys.EnumerationCount.ShouldBe(
            1,
            "uep5a7: a lazy/side-effecting key sequence must be materialized once, never enumerated twice.");
    }

    // IEnumerable<string> ONLY (deliberately NOT IReadOnlyCollection<string>) so the service's
    // `as IReadOnlyCollection<string>` shortcut cannot bypass the materializing ToArray(). Counts each
    // GetEnumerator() call so the test can prove single enumeration.
    private sealed class CountingEnumerable(IReadOnlyList<string> items) : IEnumerable<string>
    {
        public int EnumerationCount { get; private set; }

        public IEnumerator<string> GetEnumerator()
        {
            this.EnumerationCount++;
            return items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}
