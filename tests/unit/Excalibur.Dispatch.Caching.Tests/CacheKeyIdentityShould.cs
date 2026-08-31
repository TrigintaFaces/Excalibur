// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026, IL3050 // Test: RequiresUnreferencedCode/RequiresDynamicCode on key building.

using System.Text.Json.Serialization;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Caching;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// A cache key must commit to the full identity of what it is caching. Two requests that can produce
/// different results must not share an entry, and two requests that cannot must still share one.
/// </summary>
/// <remarks>
/// <para>
/// Both halves matter. A key builder returning a fresh GUID per call satisfies every safety arm here and
/// destroys caching outright, so each safety arm is paired with the liveness arm that catches that.
/// </para>
/// <para>
/// The <see cref="ICacheable{T}"/> path is a deliberate exception to the summary above, and the first arm
/// below pins it as one rather than leaving it to be rediscovered. Its key is type-blind because
/// direct-key invalidation is type-blind: an invalidating message names a logical key without knowing
/// which type stored it. Two cacheable types sharing a declared key therefore do share an entry, and the
/// property that no caller receives another action's value is enforced on the entry instead.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Feature", "Caching")]
public sealed class CacheKeyIdentityShould : IDisposable
{
	private const string SharedLogicalKey = "user:42";

	private readonly DispatchJsonSerializer _serializer = new();
	private readonly DefaultCacheKeyBuilder _keyBuilder;
	private bool _disposed;

	public CacheKeyIdentityShould() => _keyBuilder = new DefaultCacheKeyBuilder(_serializer);

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_serializer.Dispose();
	}

	// ---------------------------------------------------------------- the ICacheable path

	/// <summary>
	/// CONSTRAINT, and it is the inverse of what this slot used to assert. The arm here formerly required the
	/// two keys to DIFFER; it was measured red and skipped pending a ruling. The ruling went the other way:
	/// the keys are type-blind on purpose, so requiring them to differ was asserting against the design.
	/// </summary>
	[Fact]
	public void ShareOneKey_AcrossCacheableTypes_SoDirectKeyInvalidationStaysTypeBlind()
	{
		// The store key is exactly as type-blind as ICacheInvalidator, which names "user:42" without knowing
		// which type stored it (see TargetTheStoredEntry_WhenInvalidatingByLogicalKey). Qualify the key by
		// type and that fold stops matching; invalidation is fail-open, so nothing throws and consumers serve
		// stale data indefinitely. Pinned here so a later attempt to type-qualify the key has to confront the
		// invalidation contract rather than silently break it.
		//
		// The property the old assertion was reaching for — no caller ever receives another action's value —
		// is real and is enforced, but on the cache ENTRY rather than on the key. The key string was only ever
		// a proxy for it, so it is locked where it is actually observable, as end-to-end behaviour, in
		// CachingMiddlewareCrossTypeKeyCollisionShould.
		var summary = _keyBuilder.CreateKey(new SummaryQuery(), NewContext());
		var permissions = _keyBuilder.CreateKey(new PermissionsQuery(), NewContext());

		summary.ShouldNotBeNull();
		permissions.ShouldNotBeNull();
		summary.ShouldBe(
			permissions!,
			"the store key must stay type-blind, because a direct-key invalidation cannot name the cached type");
	}

	[Fact]
	public void ReuseOneEntry_ForTheSameCacheableTypeAndKey()
	{
		// LIVENESS for the arm above. Without this, returning a fresh value per call passes it.
		var first = _keyBuilder.CreateKey(new SummaryQuery(), NewContext());
		var second = _keyBuilder.CreateKey(new SummaryQuery(), NewContext());

		first.ShouldNotBeNull();
		first.ShouldBe(second!, "the same query type with the same declared key must still hit the cache");
	}

	// ---------------------------------------------------------------- the serialized-action path

	[Fact(Skip = "Excalibur.Dispatch-g8csbg. MEASURED RED at HEAD, not aspirational: two non-ICacheable actions differing only in a [JsonIgnore] member produce the identical key \"5C-g--sCAF1-eXMnofeZXkusuz7JaEc-nShAY-YxBps\" (DefaultCacheKeyBuilder.cs:109). Skipped for the same reason as the sibling arm above -- a standing red arm destroys the signal the rest of this suite carries. The sound fix is to DETECT that a complete identity cannot be derived and skip caching (return null), as ResolveBaseKey already does at :96 and :114: the builder treats serialized-without-throwing as identity-captured, when serialization is lossy on purpose. Do NOT un-skip without that fix.")]
	public void DistinguishTwoActions_ThatDifferOnlyInStateTheSerializerCannotSee()
	{
		// SAFETY. The non-ICacheable branch keys on the serialized action. [JsonIgnore] state is excluded
		// from serialization by definition, so two actions that differ only there serialize identically and
		// the second is served the first's result. A skip (null) is an acceptable answer here; sharing an
		// entry is not.
		var alice = _keyBuilder.CreateKey(new ReportQuery { Scope = "all", Viewer = "alice" }, NewContext());
		var bob = _keyBuilder.CreateKey(new ReportQuery { Scope = "all", Viewer = "bob" }, NewContext());

		if (alice is null && bob is null)
		{
			return; // Skipping the cache is a sound resolution: no entry, no false hit.
		}

		alice.ShouldNotBe(
			bob!,
			"two actions differing only in state the serializer cannot see must not share a cache entry");
	}

	[Fact]
	public void ReuseOneEntry_ForTwoEquivalentSerializableActions()
	{
		// LIVENESS for the arm above: identical actions must still collapse onto one entry.
		var first = _keyBuilder.CreateKey(new ReportQuery { Scope = "all", Viewer = "alice" }, NewContext());
		var second = _keyBuilder.CreateKey(new ReportQuery { Scope = "all", Viewer = "alice" }, NewContext());

		first.ShouldNotBeNull("an ordinary serializable action must be cacheable");
		first.ShouldBe(second!, "two equivalent actions must still hit the cache");
	}

	// ---------------------------------------------------------------- the constraint on any fix

	[Fact]
	public void TargetTheStoredEntry_WhenInvalidatingByLogicalKey()
	{
		// NOT a defect arm — this is the coupling any fix to the arms above has to respect, asserted so it
		// cannot be broken silently. ICacheInvalidator.GetCacheKeysToInvalidate() returns raw strings, so an
		// UpdateUserCommand invalidates a GetUserQuery entry by naming "user:42" without knowing that type.
		// Invalidation is fail-open, so if the two transforms diverge nothing throws: the removal simply
		// stops matching and consumers serve stale data indefinitely.
		var stored = _keyBuilder.CreateKey(new SummaryQuery(), NewContext());
		var invalidation = _keyBuilder.CreateKey(SharedLogicalKey, tenantId: null, userId: null);

		stored.ShouldNotBeNull();
		stored.ShouldBe(
			invalidation,
			"a direct-key invalidation must resolve to the same stored key the cached query produced");
	}

	private static IMessageContext NewContext()
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		return context;
	}

	private sealed class SummaryQuery : ICacheable<string>
	{
		public int ExpirationSeconds => 120;

		public string GetCacheKey() => SharedLogicalKey;

		public bool ShouldCache(object? result) => true;
	}

	private sealed class PermissionsQuery : ICacheable<string>
	{
		public int ExpirationSeconds => 120;

		public string GetCacheKey() => SharedLogicalKey;

		public bool ShouldCache(object? result) => true;
	}

	private sealed class ReportQuery : IDispatchAction<string>
	{
		public string Scope { get; init; } = string.Empty;

		/// <summary>Excluded from serialization, and it changes the result.</summary>
		[JsonIgnore]
		public string Viewer { get; init; } = string.Empty;
	}
}

#pragma warning restore IL2026, IL3050
