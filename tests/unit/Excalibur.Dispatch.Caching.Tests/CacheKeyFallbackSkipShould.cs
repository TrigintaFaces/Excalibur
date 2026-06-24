// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Caching;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Sprint 843 MS-2 (<c>5n1v5n</c>(b)) engage-tests for <see cref="DefaultCacheKeyBuilder"/> — the
/// Option X "no derivable cache identity → return <see langword="null"/> (skip caching)" contract, on
/// BOTH the reflection-failure path and the primary serialization path (fail-open, folds <c>bd-393l2s</c>).
/// <para>
/// Author≠implementer (TestsDeveloper). Pure/deterministic — no timing. The fix replaces the previous
/// identity-hash (<c>object.GetHashCode()</c>) fallback, which produced an unstable per-instance key
/// (cache-useless + churn) and risked a false cross-request hit.
/// </para>
/// <list type="bullet">
/// <item><b>AC-1/AC-3</b> (non-vacuity): a reflection-failing <c>ICacheable&lt;T&gt;</c> action → <c>null</c>
/// (skip). RED on pre-fix (returns a non-null identity-hash key).</item>
/// <item><b>AC-2</b>: two distinct reflection-failing actions both → <c>null</c> — no fabricated key, so a
/// false cross-request hit is structurally impossible.</item>
/// <item><b>AC-4</b> (non-vacuity, fail-open): a non-<c>ICacheable</c> action whose serialization throws →
/// <c>null</c> (skip), no exception escapes. RED on pre-fix (the exception propagates and fails the request).</item>
/// <item><b>AC-5</b>: the reflection-failure skip is logged.</item>
/// <item><b>EC-2/EC-3</b>: a resolvable <c>ICacheable</c> key and a serializable non-cacheable action still
/// produce a (non-null) key.</item>
/// </list>
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class CacheKeyFallbackSkipShould : IDisposable
{
    private readonly DispatchJsonSerializer _serializer = new();

    public void Dispose() => _serializer.Dispose();

    /// <summary>AC-1 + AC-3 (non-vacuity): reflection failure on an <c>ICacheable</c> action → null (skip).</summary>
    [Fact]
    public void ReturnNull_WhenICacheableReflectionFails()
    {
        var sut = new DefaultCacheKeyBuilder(_serializer, NullLogger<DefaultCacheKeyBuilder>.Instance);
        var context = CreateContext("tenant1", "user1");

        var key = sut.CreateKey(new ThrowingCacheableAction(), context);

        key.ShouldBeNull("a reflection failure must skip caching (no fabricated identity-hash key)");
    }

    /// <summary>AC-2: two distinct reflection-failing actions both → null — no false cross-request hit possible.</summary>
    [Fact]
    public void ReturnNullForBoth_WhenTwoDistinctActionsFailReflection_NoFabricatedKey()
    {
        var sut = new DefaultCacheKeyBuilder(_serializer, NullLogger<DefaultCacheKeyBuilder>.Instance);
        var context = CreateContext("tenant1", "user1");

        var keyA = sut.CreateKey(new ThrowingCacheableAction(), context);
        var keyB = sut.CreateKey(new OtherThrowingCacheableAction(), context);

        keyA.ShouldBeNull();
        keyB.ShouldBeNull();
    }

    /// <summary>
    /// AC-4 (non-vacuity, fail-open BOTH paths): a non-<c>ICacheable</c> action whose serialization throws
    /// → null (skip), and NO exception escapes the key builder. RED on pre-fix (the serialize exception
    /// propagates out of <c>CreateKey</c> and would fail the request).
    /// </summary>
    [Fact]
    public void ReturnNullAndNotThrow_WhenPrimarySerializationThrows()
    {
        var sut = new DefaultCacheKeyBuilder(_serializer, NullLogger<DefaultCacheKeyBuilder>.Instance);
        var context = CreateContext("tenant1", "user1");

        // A self-referencing (cyclic) non-ICacheable action: serialization hits STJ cycle detection and
        // throws (JsonException), exercising the primary-path fail-open wrap. It does not implement
        // ICacheable, so it takes the NotCacheable → serialize branch.
        var unserializable = new CyclicAction();

        string? key = null;
        Should.NotThrow(() => key = sut.CreateKey(unserializable, context));
        key.ShouldBeNull("a serialization failure on the primary path must skip caching, not fail the request");
    }

    /// <summary>AC-5: the reflection-failure skip is logged (debug).</summary>
    [Fact]
    public void LogReflectionFallback_WhenReflectionFails()
    {
        var logger = new CapturingLogger();
        var sut = new DefaultCacheKeyBuilder(_serializer, logger);
        var context = CreateContext("tenant1", "user1");

        _ = sut.CreateKey(new ThrowingCacheableAction(), context);

        logger.Events.ShouldContain(e => e.Id == 2550, "reflection-fallback skip should be logged (event 2550)");
    }

    /// <summary>EC-2: a resolvable <c>ICacheable</c> key is used (reflection path succeeds → non-null key).</summary>
    [Fact]
    public void ReturnKey_WhenICacheableKeyResolves()
    {
        var sut = new DefaultCacheKeyBuilder(_serializer, NullLogger<DefaultCacheKeyBuilder>.Instance);
        var context = CreateContext("tenant1", "user1");

        var key = sut.CreateKey(new WorkingCacheableAction("stable-key"), context);

        key.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>EC-3: a serializable non-<c>ICacheable</c> action still produces a content-stable key.</summary>
    [Fact]
    public void ReturnKey_WhenNonCacheableActionSerializes()
    {
        var sut = new DefaultCacheKeyBuilder(_serializer, NullLogger<DefaultCacheKeyBuilder>.Instance);
        var context = CreateContext("tenant1", "user1");

        var key = sut.CreateKey(new SerializableAction(), context);

        key.ShouldNotBeNullOrWhiteSpace();
    }

    private static IMessageContext CreateContext(string? tenantId, string? userId)
    {
        var context = A.Fake<IMessageContext>();
        var features = new Dictionary<Type, object>();
        var items = new Dictionary<string, object>(StringComparer.Ordinal);
        A.CallTo(() => context.Features).Returns(features);
        A.CallTo(() => context.Items).Returns(items);
        features[typeof(IMessageIdentityFeature)] = new MessageIdentityFeature { TenantId = tenantId, UserId = userId };
        return context;
    }

    private sealed class ThrowingCacheableAction : ICacheable<string>
    {
        public string GetCacheKey() => throw new InvalidOperationException("simulated reflection failure");
    }

    private sealed class OtherThrowingCacheableAction : ICacheable<string>
    {
        public string GetCacheKey() => throw new InvalidOperationException("a different reflection failure");
    }

    private sealed class WorkingCacheableAction(string cacheKey) : ICacheable<string>
    {
        public string GetCacheKey() => cacheKey;
    }

    private sealed class SerializableAction : IDispatchAction
    {
        public string Id { get; init; } = "serializable";
    }

    /// <summary>A non-cacheable action that self-references, forcing a serializer cycle failure.</summary>
    private sealed class CyclicAction : IDispatchAction
    {
        public CyclicAction Self => this;
    }

    private sealed class CapturingLogger : ILogger<DefaultCacheKeyBuilder>
    {
        public List<EventId> Events { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Events.Add(eventId);

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
